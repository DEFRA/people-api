using System.Text.Json;
using Defra_People_API.Cache;
using Defra_People_API.Entities;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Defra_People_API.Services.Logger;

public class ApiLoggerFilter : IAsyncResultFilter
{
    private readonly ILogQueue _logQueue;
    private readonly ILogger<ApiLoggerFilter> _logger;
    private readonly IApiKeyCacheService _apiKeyCacheService;
    
    public ApiLoggerFilter(ILogQueue logQueue, ILogger<ApiLoggerFilter> logger, IApiKeyCacheService apiKeyCacheService)
    {
        _logQueue = logQueue;
        _logger = logger;
        _apiKeyCacheService = apiKeyCacheService;
    }
    
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        var resultContext = await next();
        
        try
        {
            // Get the endpoint display name
            var endpoint = context.HttpContext.GetEndpoint();
            var endpointName = endpoint?.DisplayName ?? context.HttpContext.Request.Path;
            
            // Get consumer from API key
            var consumer = ExtractConsumer(context.HttpContext);
            
            // Get user info from header or use http context user info
            string user = string.Empty;
            if (context.HttpContext.Request.Headers.TryGetValue("X-User", out var userHeaderValues) && 
                !string.IsNullOrEmpty(userHeaderValues.FirstOrDefault()))
            {
                user = userHeaderValues.FirstOrDefault() ?? string.Empty;
            }
            else
            {
                user = context.HttpContext.User?.Identity?.Name ?? "Anonymous";
            }
            
            var parameters = GetRequestParameters(context.HttpContext);
            var responseContent = endpointName.Contains("generate-api-key") ? "New API Key Generated" : GetResponseContent(resultContext.Result);
            
            var logLevel = DetermineLogLevel(resultContext.Result);
            
            var logItem = new LogItem
            {
                Consumer = consumer,
                Method = context.HttpContext.Request.Method,
                Endpoint = endpointName,
                User = user,
                Params = parameters,
                Response = responseContent,
                LogLevel = logLevel,
                Timestamp = DateTime.UtcNow,
                Runtime = DateTime.UtcNow - (context.HttpContext.Items["StartTime"] is DateTime startTime ? startTime : DateTime.UtcNow)
            };
            
            // Add to queue instead of directly to database
            _logQueue.Enqueue(logItem);
            
            _logger.LogInformation("API Call queued: {Endpoint} by {Consumer}", endpointName, consumer);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging API call");
        }
    }
    
    private string ExtractConsumer(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-API-Key", out var apiKeyValues))
        {
            var apiKey = apiKeyValues.FirstOrDefault();
            if (!string.IsNullOrEmpty(apiKey) && _apiKeyCacheService.IsValidKey(apiKey))
            {
                // Get all keys and find the one that matches
                var keyInfo = _apiKeyCacheService.GetAllKeys().Result.FirstOrDefault(k => k.Key == apiKey);
                if (keyInfo != null)
                {
                    return keyInfo.Consumer;
                }
            }
        }
        
        return "Unknown";
    }
    
    private string GetRequestParameters(HttpContext context)
    {
        var parameters = new Dictionary<string, object>();
        
        // Add route values
        var routeData = context.GetRouteData();
        if (routeData != null)
        {
            foreach (var routeValue in routeData.Values)
            {
                parameters[routeValue.Key] = routeValue.Value ?? string.Empty;
            }
        }
        
        // Add query values
        foreach (var param in context.Request.Query)
        {
            parameters[param.Key] = param.Value.ToString();
        }
        
        // Add form data
        if (context.Request.HasFormContentType)
        {
            foreach (var param in context.Request.Form)
            {
                parameters[param.Key] = param.Value.ToString();
            }
        }
        
        // Get request body for POST/PUT requests
        if (context.Items.TryGetValue("RequestBody", out var requestBody) && requestBody != null)
        {
            parameters["body"] = requestBody;
        }
        
        return JsonSerializer.Serialize(parameters);
    }
    
    private string GetResponseContent(IActionResult result)
    {
        if (result is ObjectResult objectResult)
        {
            try
            {
                return JsonSerializer.Serialize(objectResult.Value);
            }
            catch
            {
                return $"{{\"statusCode\": {objectResult.StatusCode}, \"type\": \"{objectResult.Value?.GetType().Name}\"}}";
            }
        }
        
        if (result is StatusCodeResult statusCodeResult)
        {
            return $"{{\"statusCode\": {statusCodeResult.StatusCode}}}";
        }
        
        return $"{{\"resultType\": \"{result.GetType().Name}\"}}";
    }
    
    private Entities.LogLevel DetermineLogLevel(IActionResult result)
    {
        // First check for problem results and return Warning immediately
        
        // Check if result is ObjectResult with ProblemDetails
        if (result is ObjectResult objectResult && objectResult.Value is ProblemHttpResult)
        {
            return Entities.LogLevel.Warning;
        }

        // For non-problem results, determine log level based on status code
        int statusCode = 200;
        
        if (result is ObjectResult nonProblemDetailsResult)
        {
            statusCode = nonProblemDetailsResult.StatusCode ?? 200;
        }
        else if (result is StatusCodeResult statusCodeResult)
        {
            statusCode = statusCodeResult.StatusCode;
        }
        
        return statusCode >= 400 ? Entities.LogLevel.Error : Entities.LogLevel.Information;
    }
}

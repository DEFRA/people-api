using System.Text.Json;
using Defra_People_API.Cache;
using Defra_People_API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace Defra_People_API.Endpoints.POST;

public static class GenerateApiKeyEndpoint
{
    public static WebApplication RegisterEndpoint(this WebApplication app)
    {
        app.MapPost("/admin/generate-api-key", async ([FromServices] IApiKeyCacheService apiKeyCacheService, HttpRequest request, HttpResponse response, HttpContext httpContext) => 
        {
            httpContext.Items["StartTime"] = DateTime.UtcNow;

            request.Headers.TryGetValue("X-State", out var state);

            if(state == StringValues.Empty)
            {
                return Results.BadRequest("State is required");
            }
            
            using var reader = new StreamReader(request.Body);
            var body = await reader.ReadToEndAsync();
            var requestData = JsonSerializer.Deserialize<JsonElement>(body);
            
            var consumer = requestData.GetProperty("consumer").GetString() ?? "Unknown";
            var issuer = requestData.TryGetProperty("issuer", out var issuerElement) ? issuerElement.GetString() : "Defra";
            var description = requestData.TryGetProperty("description", out var descElement) ? descElement.GetString() : $"API key for {consumer}";
            var owner = requestData.TryGetProperty("owner", out var ownerElement) ? ownerElement.GetString() : "Admin";
            
            var apiKey = ApiKeyGenerator.GenerateApiKeyWithPrefix(consumer);
            
            var keyInfo = new Entities.AccessKey
            {
                Key = apiKey,
                Consumer = consumer,
                Issuer = issuer ?? "Defra",
                Description = description ?? $"API key for {consumer}",
                Owner = owner ?? "Admin",
                Created = DateTime.UtcNow,
                Expires = DateTime.UtcNow.AddYears(1),
                Active = true
            };
            
            await apiKeyCacheService.SaveKeyToFile(keyInfo);
            
            response.Headers.Append("X-State", state);
            return Results.Ok(new { apiKey, message = "This key will never be shown again and there is no way to retrieve it. Save it now!" });
        })
        .RequireAuthorization("Admin")
        .WithApiLogging();
        
        return app;
    }
}

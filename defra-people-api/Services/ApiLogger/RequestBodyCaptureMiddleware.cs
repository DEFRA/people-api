using System.Text;

namespace Defra_People_API.Services.Logger;

public class RequestBodyCaptureMiddleware
{
    private readonly RequestDelegate _next;
    
    public RequestBodyCaptureMiddleware(RequestDelegate next)
    {
        _next = next;
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        // Only capture for POST, PUT, PATCH methods
        if (ShouldCaptureBody(context.Request.Method))
        {
            context.Request.EnableBuffering();
            
            using var reader = new StreamReader(
                context.Request.Body,
                encoding: Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                leaveOpen: true);
            
            var body = await reader.ReadToEndAsync();
            
            context.Items["RequestBody"] = body;
            context.Request.Body.Position = 0;
        }
        
        await _next(context);
    }
    
    private bool ShouldCaptureBody(string method)
    {
        return method.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
                method.Equals("PUT", StringComparison.OrdinalIgnoreCase) ||
                method.Equals("PATCH", StringComparison.OrdinalIgnoreCase);
    }
}

// Extension method
public static class RequestBodyCaptureMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestBodyCapture(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestBodyCaptureMiddleware>();
    }
}

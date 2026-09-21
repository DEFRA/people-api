using Defra_People_API.Cache;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace Defra_People_API.Endpoints.POST;

public static class RefreshApiKeysEndpoint
{
    public static WebApplication RegisterEndpoint(this WebApplication app)
    {
        app.MapPost("/admin/refresh-api-keys", ([FromServices] IApiKeyCacheService apiKeyCacheService, HttpRequest request, HttpResponse response, HttpContext httpContext) => 
        {
            httpContext.Items["StartTime"] = DateTime.UtcNow;

            request.Headers.TryGetValue("X-State", out var state);

            if(state == StringValues.Empty)
            {
                return Results.BadRequest("State is required");
            }

            apiKeyCacheService.RefreshCache();
            response.Headers.Append("X-State", state);
            return Results.Ok(new { message = "API key cache refreshed successfully" });
            
        })
        .RequireAuthorization("Admin")
        .WithApiLogging();
        
        return app;
    }
}

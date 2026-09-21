using Defra_People_API.Cache;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace Defra_People_API.Endpoints.GET;

public static class AdminListApiKeysEndpoint
{
    public static WebApplication RegisterEndpoint(this WebApplication app)
    {
        app.MapGet("/admin/list-api-keys", async ([FromServices] IApiKeyCacheService apiKeyCacheService, HttpRequest request, HttpResponse response, HttpContext httpContext) => 
        {
            httpContext.Items["StartTime"] = DateTime.UtcNow;
            
            request.Headers.TryGetValue("X-State", out var state);

            if(state == StringValues.Empty)
            {
                return Results.BadRequest("State is required");
            }

            var apiKeyCache = await apiKeyCacheService.GetAllKeys().ContinueWith(x =>
            {
                if (x.IsFaulted)
                {
                    return Results.Problem("Problem retrieving API keys");
                }

                var keys = x.Result?.Select(key => new
                {
                    key.Consumer,
                    key.Description,
                    key.Owner,
                    key.Expires,
                    key.Active
                });

                return Results.Ok(new { apiKeys = x.Result });
            });

            response.Headers.Append("X-State", state);
            return apiKeyCache;
        })
        .RequireAuthorization("Admin")
        .WithApiLogging();
        
        return app;
    }
}

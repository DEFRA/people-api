using Defra_People_API.Cache;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace Defra_People_API.Endpoints.POST;

public static class AdminPurgeCacheEndpoint
{
    public static WebApplication RegisterEndpoint(this WebApplication app)
    {
        app.MapPost("/admin/purge-cache", async ([FromServices] IUserCache userCache, HttpRequest request, HttpResponse response, HttpContext httpContext) => 
        {
            httpContext.Items["StartTime"] = DateTime.UtcNow;

            request.Headers.TryGetValue("X-State", out var state);

            if(state == StringValues.Empty)
            {
                return Results.BadRequest("State is required");
            }

            var purgeCache = await userCache.PurgeCache().ContinueWith(x =>
            {
                if (x.IsFaulted)
                {
                    return Results.Problem("Problem purging cache");
                }

                return Results.Ok(new { message = "Cache purged successfully" });
            });

            response.Headers.Append("X-State", state);
            return purgeCache;
            
        })
        .RequireAuthorization("Admin")
        .WithApiLogging();
        
        return app;
    }
}

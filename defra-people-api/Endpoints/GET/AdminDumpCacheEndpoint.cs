using Defra_People_API.Cache;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace Defra_People_API.Endpoints.GET;

public static class AdminDumpCacheEndpoint
{
    public static WebApplication RegisterEndpoint(this WebApplication app)
    {
        app.MapGet("/admin/dump-cache", async ([FromServices] IUserCache userCache, HttpRequest request, HttpResponse response, HttpContext httpContext) => 
        {
            httpContext.Items["StartTime"] = DateTime.UtcNow;
            
            request.Headers.TryGetValue("X-State", out var state);

            if(state == StringValues.Empty)
            {
                return Results.BadRequest("State is required");
            }

            var cache = await userCache.DumpCache().ContinueWith(x =>
            {
                if (x.IsFaulted)
                {
                    return Results.Problem("Problem dumping cache");
                }

                var users = x.Result?.Select(user => new
                {
                    user.Id,
                    user.DisplayName,
                    user.EmployeeId,
                    user.OnPremisesSamAccountName,
                    user.MailNickname,
                    user.UserPrincipalName
                });

                return Results.Ok(users);
            });
            
            response.Headers.Append("X-State", state);
            return cache;
        })
        .RequireAuthorization("Admin")
        .WithApiLogging();
        
        return app;
    }
}

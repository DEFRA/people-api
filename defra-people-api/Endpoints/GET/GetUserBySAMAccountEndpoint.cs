using Defra_People_API.Services.Graph;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace Defra_People_API.Endpoints.GET;

public static class GetUserBySAMAccountEndpoint
{
    public static WebApplication RegisterEndpoint(this WebApplication app)
    {
        app.MapGet("/getusers/by-samaccount/{id}", async (string id, [FromServices] IGraphService graphService, HttpRequest request, HttpResponse response, HttpContext httpContext) => 
        {
            httpContext.Items["StartTime"] = DateTime.UtcNow;
            
            request.Headers.TryGetValue("X-State", out var state);

            if(state == StringValues.Empty)
            {
                return Results.BadRequest("State is required");
            }        

            request.Headers.TryGetValue("X-GetManager", out var getManager);
            bool.TryParse(getManager, out var getManagerBool);

            var user = await graphService.GetUserBySAMAccount(id, getManagerBool);
            
            response.Headers.Append("X-State", state);
            return Results.Ok(user);
        })
        .WithApiLogging();
        
        return app;
    }
}

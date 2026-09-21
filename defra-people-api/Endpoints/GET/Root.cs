using Microsoft.AspNetCore.Mvc;

namespace Defra_People_API.Endpoints.GET;

public static class RootEndpoint
{
    public static WebApplication RegisterEndpoint(this WebApplication app)
    {
        app.MapGet("/", () => "Hello World!").AllowAnonymous();
        
        return app;
    }
}

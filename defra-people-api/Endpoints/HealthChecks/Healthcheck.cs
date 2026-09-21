using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Defra_People_API.Endpoints;

public static partial class Endpoints
{
    public static WebApplication RegisterHealthCheck(this WebApplication app)
    {       
        // Detailed - checks all dependencies
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
            AllowCachingResponses = false
        }).AllowAnonymous();
        
        // just checks if the application is running
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
            AllowCachingResponses = false
        }).AllowAnonymous();
        
        // checks if the api is ready to accept requests
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("self") || check.Tags.Contains("data_store"),
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
            AllowCachingResponses = false
        }).AllowAnonymous();
        
        return app;
    }
}

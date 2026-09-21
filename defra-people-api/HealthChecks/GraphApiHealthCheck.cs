using Microsoft.Extensions.Diagnostics.HealthChecks;
using Defra_People_API.Services;

namespace Defra_People_API.HealthChecks;

public class GraphApiHealthCheck : IHealthCheck
{
    private readonly IGraphClientFactory _graphClientFactory;

    public GraphApiHealthCheck(IGraphClientFactory graphClientFactory)
    {
        _graphClientFactory = graphClientFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _graphClientFactory.CreateClient();
            if (client == null)
            {
                return HealthCheckResult.Unhealthy("Could not create Graph API client");
            }
            
            // Make a lightweight API call to verify the connection is working
            var response = await client.Users
                .GetAsync(requestConfig => 
                {
                    requestConfig.QueryParameters.Top = 1;
                    requestConfig.QueryParameters.Select = new[] { "id" };
                }, cancellationToken);
                
            return HealthCheckResult.Healthy("Graph API connection is healthy");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"Graph API health check failed: {ex.Message}");
        }
    }
}

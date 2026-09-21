using Microsoft.Extensions.Diagnostics.HealthChecks;
using Defra_People_API.Repository;

namespace Defra_People_API.HealthChecks;

public class DatabaseHealthCheck : IHealthCheck
{
    private readonly LogDbContext _dbContext;

    public DatabaseHealthCheck(LogDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
            
            return canConnect
                ? HealthCheckResult.Healthy("Database connection is healthy")
                : HealthCheckResult.Unhealthy("Could not connect to database");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"Database health check failed: {ex.Message}");
        }
    }
}

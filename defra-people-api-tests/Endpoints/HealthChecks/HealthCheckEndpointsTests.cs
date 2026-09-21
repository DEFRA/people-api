using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Moq;
using Xunit;

namespace defra.people_api_tests.Endpoints.HealthChecks;

public class HealthCheckEndpointsTests
{
    [Fact(Skip = "WebApplication cannot be mocked as it's a sealed class")]
    public void RegisterHealthCheck_RegistersHealthCheckEndpoints()
    {
        // This test would verify that the health check endpoints are registered correctly
    }
}

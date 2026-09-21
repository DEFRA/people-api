using System;
using System.Threading.Tasks;
using Defra_People_API.HealthChecks;
using Defra_People_API.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Graph;
using Moq;
using Xunit;

namespace defra.people_api_tests.HealthChecks;

public class GraphApiHealthCheckTests
{
    private readonly Mock<IGraphClientFactory> _mockGraphClientFactory;
    private readonly GraphApiHealthCheck _healthCheck;

    public GraphApiHealthCheckTests()
    {
        _mockGraphClientFactory = new Mock<IGraphClientFactory>();
        _healthCheck = new GraphApiHealthCheck(_mockGraphClientFactory.Object);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenGraphClientCannotBeCreated_ReturnsUnhealthy()
    {
        // Arrange
        _mockGraphClientFactory.Setup(x => x.CreateClient()).Returns((GraphServiceClient)null);

        // Act
        var result = await _healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("could not create", result.Description.ToLower());
    }
}

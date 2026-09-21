using System;
using System.Threading;
using System.Threading.Tasks;
using Defra_People_API.HealthChecks;
using Defra_People_API.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using Xunit;

namespace defra.people_api_tests.HealthChecks;

public class DatabaseHealthCheckTests
{
    private readonly Mock<LogDbContext> _mockDbContext;
    private readonly Mock<DatabaseFacade> _mockDatabase;
    private readonly DatabaseHealthCheck _healthCheck;

    public DatabaseHealthCheckTests()
    {
        _mockDbContext = new Mock<LogDbContext>(new DbContextOptions<LogDbContext>());
        _mockDatabase = new Mock<DatabaseFacade>(_mockDbContext.Object);
        
        _mockDbContext.Setup(x => x.Database).Returns(_mockDatabase.Object);
        
        _healthCheck = new DatabaseHealthCheck(_mockDbContext.Object);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenDatabaseIsConnected_ReturnsHealthy()
    {
        // Arrange
        _mockDatabase.Setup(x => x.CanConnectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("healthy", result.Description.ToLower());
    }

    [Fact]
    public async Task CheckHealthAsync_WhenDatabaseCannotConnect_ReturnsUnhealthy()
    {
        // Arrange
        _mockDatabase.Setup(x => x.CanConnectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("could not connect", result.Description.ToLower());
    }

    [Fact]
    public async Task CheckHealthAsync_WhenExceptionOccurs_ReturnsUnhealthy()
    {
        // Arrange
        var exceptionMessage = "Test exception";
        _mockDatabase.Setup(x => x.CanConnectAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception(exceptionMessage));

        // Act
        var result = await _healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains(exceptionMessage, result.Description);
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using Defra_People_API.Entities;
using Defra_People_API.Repository;
using Defra_People_API.Services.Logger;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace defra.people_api_tests.Services.ApiLogger;

public class LogProcessingServiceTests
{
    private readonly Mock<ILogQueue> _mockLogQueue;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<ILogger<LogProcessingService>> _mockLogger;
    private readonly LogProcessingService _service;
    
    public LogProcessingServiceTests()
    {
        _mockLogQueue = new Mock<ILogQueue>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockLogger = new Mock<ILogger<LogProcessingService>>();
        
        _service = new LogProcessingService(
            _mockLogQueue.Object,
            _mockServiceProvider.Object,
            _mockLogger.Object);
    }
    
    [Fact]
    public async Task StartAsync_StartsProcessingLogs()
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource();
        
        // Act
        await _service.StartAsync(cancellationTokenSource.Token);
        
        // Wait a bit for the service to start
        await Task.Delay(100);
        
        // Stop the service
        await _service.StopAsync(cancellationTokenSource.Token);
        
        // Assert
        _mockLogger.Verify(
            x => x.Log(
                Microsoft.Extensions.Logging.LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("starting")),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()),
            Times.Once);
    }
    
    [Fact]
    public async Task StopAsync_StopsProcessingLogs()
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource();
        
        // Act
        await _service.StartAsync(cancellationTokenSource.Token);
        await _service.StopAsync(cancellationTokenSource.Token);
        
        // Assert
        _mockLogger.Verify(
            x => x.Log(
                Microsoft.Extensions.Logging.LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("stopping")),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()),
            Times.Once);
    }
}

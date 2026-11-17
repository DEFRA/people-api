using System;
using System.Threading;
using System.Threading.Tasks;
using Defra_People_API.Entities;
using Defra_People_API.Services.Logger;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace defra.people_api_tests.Services.ApiLogger;

public class LogProcessingServiceTests
{
    private readonly Mock<ILogQueue> _mockLogQueue;
    private readonly Mock<ILogger<LogProcessingService>> _mockLogger;
    private readonly LogProcessingService _service;
    
    public LogProcessingServiceTests()
    {
        _mockLogQueue = new Mock<ILogQueue>();
        _mockLogger = new Mock<ILogger<LogProcessingService>>();
        
        _service = new LogProcessingService(
            _mockLogQueue.Object,
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
                (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
            Times.Once);
    }
    
    [Fact]
    public async Task StopAsync_StopsProcessingLogs()
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource();
        
        // Act
        await _service.StartAsync(cancellationTokenSource.Token);
        // Give ExecuteAsync time to start; on .NET 10 stopping before it starts skips it entirely
        await Task.Delay(100);
        await _service.StopAsync(cancellationTokenSource.Token);
        // Wait for ExecuteAsync to finish so the "stopping" log has been written
        await _service.ExecuteTask!;
        
        // Assert
        _mockLogger.Verify(
            x => x.Log(
                Microsoft.Extensions.Logging.LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("stopping")),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WritesQueuedLogItem_ViaLogger()
    {
        // Arrange - a real queue (not mocked) so TryDequeue behaves naturally once an item is enqueued
        var queue = new LogQueue();
        queue.Enqueue(new LogItem
        {
            Consumer = "test-consumer",
            Method = "GET",
            Endpoint = "/getusers/by-sid/x",
            User = "Anonymous",
            Params = "{}",
            Response = "{}",
            LogLevel = Defra_People_API.Entities.LogLevel.Error,
            Runtime = TimeSpan.FromMilliseconds(42)
        });
        var logger = new Mock<ILogger<LogProcessingService>>();
        var service = new LogProcessingService(queue, logger.Object);
        var cancellationTokenSource = new CancellationTokenSource();

        // Act
        await service.StartAsync(cancellationTokenSource.Token);
        await Task.Delay(200);
        await service.StopAsync(cancellationTokenSource.Token);

        // Assert - the item is written via ILogger (no SQL/database involved), at the mapped log level
        logger.Verify(
            x => x.Log(
                Microsoft.Extensions.Logging.LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("test-consumer") && v.ToString().Contains("API call audit")),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
            Times.Once);
    }
}

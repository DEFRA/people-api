using System;
using System.Collections.Generic;
using Defra_People_API.Entities;
using Defra_People_API.Services.Logger;
using Xunit;

namespace defra.people_api_tests.Services.ApiLogger;

public class LogQueueTests
{
    [Fact]
    public void Enqueue_AddsItemToQueue()
    {
        // Arrange
        var queue = new LogQueue();
        var logItem = new LogItem { Endpoint = "test-endpoint" };

        // Act
        queue.Enqueue(logItem);

        // Assert
        Assert.Equal(1, queue.Count);
    }

    [Fact]
    public void TryDequeue_WhenQueueIsEmpty_ReturnsFalse()
    {
        // Arrange
        var queue = new LogQueue();

        // Act
        var result = queue.TryDequeue(out var item);

        // Assert
        Assert.False(result);
        Assert.Null(item);
    }

    [Fact]
    public void TryDequeue_WhenQueueHasItems_ReturnsTrueAndItem()
    {
        // Arrange
        var queue = new LogQueue();
        var logItem = new LogItem { Endpoint = "test-endpoint" };
        queue.Enqueue(logItem);

        // Act
        var result = queue.TryDequeue(out var dequeuedItem);

        // Assert
        Assert.True(result);
        Assert.Equal(logItem.Endpoint, dequeuedItem.Endpoint);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void Count_ReturnsNumberOfItemsInQueue()
    {
        // Arrange
        var queue = new LogQueue();
        
        // Act & Assert - Empty queue
        Assert.Equal(0, queue.Count);
        
        // Add items
        queue.Enqueue(new LogItem { Endpoint = "endpoint1" });
        queue.Enqueue(new LogItem { Endpoint = "endpoint2" });
        
        // Check count after adding
        Assert.Equal(2, queue.Count);
        
        // Remove an item
        queue.TryDequeue(out _);
        
        // Check count after removing
        Assert.Equal(1, queue.Count);
    }
}

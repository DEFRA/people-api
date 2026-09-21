using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Defra_People_API.Cache;
using Defra_People_API.Entities;
using Defra_People_API.Services.Logger;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace defra.people_api_tests.Services.ApiLogger;

public class ApiLoggerFilterTests
{
    private readonly Mock<ILogQueue> _mockLogQueue;
    private readonly Mock<ILogger<ApiLoggerFilter>> _mockLogger;
    private readonly Mock<IApiKeyCacheService> _mockApiKeyCacheService;
    private readonly ApiLoggerFilter _filter;
    
    public ApiLoggerFilterTests()
    {
        _mockLogQueue = new Mock<ILogQueue>();
        _mockLogger = new Mock<ILogger<ApiLoggerFilter>>();
        _mockApiKeyCacheService = new Mock<IApiKeyCacheService>();
        
        _filter = new ApiLoggerFilter(_mockLogQueue.Object, _mockLogger.Object, _mockApiKeyCacheService.Object);
    }
    
    [Fact]
    public async Task OnResultExecutionAsync_LogsApiCall()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";
        httpContext.Request.Path = "/api/test";
        httpContext.Items["StartTime"] = DateTime.UtcNow.AddSeconds(-1); // 1 second ago
        
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor());
            
        var resultExecutingContext = new ResultExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new OkObjectResult(new { message = "Test" }),
            new Mock<Controller>().Object);
            
        var resultExecutedContext = new ResultExecutedContext(
            actionContext,
            new List<IFilterMetadata>(),
            new OkObjectResult(new { message = "Test" }),
            new Mock<Controller>().Object);
            
        var nextCallback = new ResultExecutionDelegate(() => Task.FromResult(resultExecutedContext));
        
        // Act
        await _filter.OnResultExecutionAsync(resultExecutingContext, nextCallback);
        
        // Assert
        _mockLogQueue.Verify(x => x.Enqueue(It.IsAny<LogItem>()), Times.Once);
    }
    
    [Fact]
    public async Task OnResultExecutionAsync_WithApiKey_ExtractsConsumer()
    {
        // Arrange
        var apiKey = "test-api-key";
        var consumer = "test-consumer";
        
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";
        httpContext.Request.Path = "/api/test";
        httpContext.Request.Headers["X-API-Key"] = apiKey;
        httpContext.Items["StartTime"] = DateTime.UtcNow;
        
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor());
            
        var resultExecutingContext = new ResultExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new OkObjectResult(new { message = "Test" }),
            new Mock<Controller>().Object);
            
        var resultExecutedContext = new ResultExecutedContext(
            actionContext,
            new List<IFilterMetadata>(),
            new OkObjectResult(new { message = "Test" }),
            new Mock<Controller>().Object);
            
        var nextCallback = new ResultExecutionDelegate(() => Task.FromResult(resultExecutedContext));
        
        // Set up API key validation
        _mockApiKeyCacheService.Setup(x => x.IsValidKey(apiKey)).Returns(true);
        _mockApiKeyCacheService.Setup(x => x.GetAllKeys())
            .ReturnsAsync(new List<AccessKey> { new AccessKey { Key = apiKey, Consumer = consumer } });
        
        // Act
        await _filter.OnResultExecutionAsync(resultExecutingContext, nextCallback);
        
        // Assert
        _mockLogQueue.Verify(x => x.Enqueue(It.Is<LogItem>(item => item.Consumer == consumer)), Times.Once);
    }
    
    [Fact]
    public async Task OnResultExecutionAsync_WithUserHeader_ExtractsUser()
    {
        // Arrange
        var user = "test-user";
        
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";
        httpContext.Request.Path = "/api/test";
        httpContext.Request.Headers["X-User"] = user;
        httpContext.Items["StartTime"] = DateTime.UtcNow;
        
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor());
            
        var resultExecutingContext = new ResultExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new OkObjectResult(new { message = "Test" }),
            new Mock<Controller>().Object);
            
        var resultExecutedContext = new ResultExecutedContext(
            actionContext,
            new List<IFilterMetadata>(),
            new OkObjectResult(new { message = "Test" }),
            new Mock<Controller>().Object);
            
        var nextCallback = new ResultExecutionDelegate(() => Task.FromResult(resultExecutedContext));
        
        // Act
        await _filter.OnResultExecutionAsync(resultExecutingContext, nextCallback);
        
        // Assert
        _mockLogQueue.Verify(x => x.Enqueue(It.Is<LogItem>(item => item.User == user)), Times.Once);
    }
    
    [Fact]
    public async Task OnResultExecutionAsync_WithErrorResult_SetsErrorLogLevel()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";
        httpContext.Request.Path = "/api/test";
        httpContext.Items["StartTime"] = DateTime.UtcNow;
        
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor());
            
        var resultExecutingContext = new ResultExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new StatusCodeResult(500),
            new Mock<Controller>().Object);
            
        var resultExecutedContext = new ResultExecutedContext(
            actionContext,
            new List<IFilterMetadata>(),
            new StatusCodeResult(500),
            new Mock<Controller>().Object);
            
        var nextCallback = new ResultExecutionDelegate(() => Task.FromResult(resultExecutedContext));
        
        // Act
        await _filter.OnResultExecutionAsync(resultExecutingContext, nextCallback);
        
        // Assert
        _mockLogQueue.Verify(x => x.Enqueue(It.Is<LogItem>(item => item.LogLevel == Defra_People_API.Entities.LogLevel.Error)), Times.Once);
    }
    
    [Fact]
    public async Task OnResultExecutionAsync_WithException_LogsError()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";
        httpContext.Request.Path = "/api/test";
        httpContext.Items["StartTime"] = DateTime.UtcNow;
        
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor());
            
        var resultExecutingContext = new ResultExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new OkObjectResult(new { message = "Test" }),
            new Mock<Controller>().Object);
            
        var resultExecutedContext = new ResultExecutedContext(
            actionContext,
            new List<IFilterMetadata>(),
            new OkObjectResult(new { message = "Test" }),
            new Mock<Controller>().Object);
            
        var nextCallback = new ResultExecutionDelegate(() => Task.FromResult(resultExecutedContext));
        
        // Set up log queue to throw exception
        _mockLogQueue.Setup(x => x.Enqueue(It.IsAny<LogItem>())).Throws(new Exception("Test exception"));
        
        // Act
        await _filter.OnResultExecutionAsync(resultExecutingContext, nextCallback);
        
        // Assert
        _mockLogger.Verify(
            x => x.Log(
                Microsoft.Extensions.Logging.LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()),
            Times.Once);
    }
}

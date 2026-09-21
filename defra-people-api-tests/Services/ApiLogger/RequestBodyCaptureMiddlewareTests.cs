using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Defra_People_API.Services.Logger;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace defra.people_api_tests.Services.ApiLogger;

public class RequestBodyCaptureMiddlewareTests
{
    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    public async Task InvokeAsync_WithCaptureableMethod_CapturesRequestBody(string method)
    {
        // Arrange
        var requestBody = "test request body";
        var requestBodyBytes = Encoding.UTF8.GetBytes(requestBody);
        var requestStream = new MemoryStream(requestBodyBytes);
        
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = method;
        httpContext.Request.Body = requestStream;
        
        var middleware = new RequestBodyCaptureMiddleware(next: (innerHttpContext) => Task.CompletedTask);
        
        // Act
        await middleware.InvokeAsync(httpContext);
        
        // Assert
        Assert.True(httpContext.Items.ContainsKey("RequestBody"));
        Assert.Equal(requestBody, httpContext.Items["RequestBody"]);
        
        // Verify that the stream position was reset to 0
        Assert.Equal(0, httpContext.Request.Body.Position);
    }
    
    [Theory]
    [InlineData("GET")]
    [InlineData("DELETE")]
    [InlineData("HEAD")]
    public async Task InvokeAsync_WithNonCaptureableMethod_DoesNotCaptureRequestBody(string method)
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = method;
        httpContext.Request.Body = new MemoryStream();
        
        var middleware = new RequestBodyCaptureMiddleware(next: (innerHttpContext) => Task.CompletedTask);
        
        // Act
        await middleware.InvokeAsync(httpContext);
        
        // Assert
        Assert.False(httpContext.Items.ContainsKey("RequestBody"));
    }
    
    [Fact]
    public void UseRequestBodyCapture_ReturnsApplicationBuilder()
    {
        // Arrange
        var appBuilderMock = new Mock<IApplicationBuilder>();
        
        // Setup the mock to return itself for any method call
        appBuilderMock.Setup(x => x.Use(It.IsAny<Func<RequestDelegate, RequestDelegate>>()))
            .Returns(appBuilderMock.Object);
        
        // Act
        var result = RequestBodyCaptureMiddlewareExtensions.UseRequestBodyCapture(appBuilderMock.Object);
        
        // Assert
        Assert.Same(appBuilderMock.Object, result);
    }
}

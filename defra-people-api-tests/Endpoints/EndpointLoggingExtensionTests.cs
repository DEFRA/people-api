using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Defra_People_API.Endpoints;
using Defra_People_API.Services.Logger;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace defra.people_api_tests.Endpoints;

public class EndpointLoggingExtensionTests
{
    [Fact]
    public void WithApiLogging_ReturnsRouteHandlerBuilder()
    {
        // Arrange
        var mockEndpointConventionBuilder = new Mock<IEndpointConventionBuilder>();
        var routeHandlerBuilder = new RouteHandlerBuilder(new[] { mockEndpointConventionBuilder.Object });
        
        // Act
        var result = EndpointLoggingExtension.WithApiLogging(routeHandlerBuilder);
        
        // Assert
        Assert.NotNull(result);
        Assert.Same(routeHandlerBuilder, result);
    }
}

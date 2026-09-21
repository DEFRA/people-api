using System;
using Defra_People_API.Endpoints.GET;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Moq;
using Xunit;

namespace defra.people_api_tests.Endpoints.GET;

public class GetEndpointsTests
{
    [Fact(Skip = "WebApplication cannot be mocked as it's a sealed class")]
    public void RootEndpoint_RegistersEndpoint()
    {
        // This test would verify that the root endpoint is registered correctly
    }
    
    [Fact(Skip = "WebApplication cannot be mocked as it's a sealed class")]
    public void GetUserBySidEndpoint_RegistersEndpoint()
    {
        // This test would verify that the GetUserBySid endpoint is registered correctly
    }
    
    [Fact(Skip = "WebApplication cannot be mocked as it's a sealed class")]
    public void AdminDumpCacheEndpoint_RegistersEndpoint()
    {
        // This test would verify that the AdminDumpCache endpoint is registered correctly
    }
}

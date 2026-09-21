using System;
using System.Collections.Generic;
using Defra_People_API.Entities;
using Defra_People_API.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph;
using Moq;
using Xunit;

namespace defra.people_api_tests.Services.Graph;

public class GraphClientFactoryTests
{
    [Fact]
    public void CreateClient_WithValidConfig_ReturnsGraphServiceClient()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                {"GraphApi:ClientId", "test-client-id"},
                {"GraphApi:ClientSecret", "test-client-secret"},
                {"GraphApi:TenantId", "test-tenant-id"},
                {"GraphApi:Scopes:0", "User.Read.All"}
            })
            .Build();
        
        var factory = new Defra_People_API.Services.GraphClientFactory(configuration);

        // Act
        var client = factory.CreateClient();

        // Assert
        Assert.NotNull(client);
        Assert.IsType<GraphServiceClient>(client);
    }

    [Fact]
    public void CreateClient_WithNullScopes_UsesDefaultScope()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                {"GraphApi:ClientId", "test-client-id"},
                {"GraphApi:ClientSecret", "test-client-secret"},
                {"GraphApi:TenantId", "test-tenant-id"}
                // No scopes defined, should use default
            })
            .Build();
        
        var factory = new Defra_People_API.Services.GraphClientFactory(configuration);

        // Act
        var client = factory.CreateClient();

        // Assert
        Assert.NotNull(client);
        Assert.IsType<GraphServiceClient>(client);
        // Note: We can't directly verify the scopes used as they're not exposed by the GraphServiceClient
    }

    [Fact]
    public void CreateClient_WithCustomScopes_UsesProvidedScopes()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                {"GraphApi:ClientId", "test-client-id"},
                {"GraphApi:ClientSecret", "test-client-secret"},
                {"GraphApi:TenantId", "test-tenant-id"},
                {"GraphApi:Scopes:0", "User.Read.All"},
                {"GraphApi:Scopes:1", "Directory.Read.All"}
            })
            .Build();
        
        var factory = new Defra_People_API.Services.GraphClientFactory(configuration);

        // Act
        var client = factory.CreateClient();

        // Assert
        Assert.NotNull(client);
        Assert.IsType<GraphServiceClient>(client);
        // Note: We can't directly verify the scopes used as they're not exposed by the GraphServiceClient
    }
}

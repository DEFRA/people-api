using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Defra_People_API.Cache;
using Defra_People_API.Services;
using Defra_People_API.Services.Graph;
using Microsoft.Graph.Models;
using Moq;
using Xunit;

namespace defra.people_api_tests.Services.Graph;

public class GraphServiceTests
{
    private readonly Mock<IGraphClientFactory> _mockGraphClientFactory;
    private readonly Mock<IUserCache> _mockUserCache;
    private readonly GraphService _graphService;
    private readonly MethodInfo _addManagerPropertiesToAdditionalDataMethod;

    public GraphServiceTests()
    {
        _mockGraphClientFactory = new Mock<IGraphClientFactory>();
        _mockUserCache = new Mock<IUserCache>();
        _graphService = new GraphService(_mockGraphClientFactory.Object, _mockUserCache.Object);
        
        // Get the private method using reflection
        _addManagerPropertiesToAdditionalDataMethod = typeof(GraphService).GetMethod(
            "AddManagerPropertiesToAdditionalData", 
            BindingFlags.NonPublic | BindingFlags.Instance);
    }

    #region AddManagerPropertiesToAdditionalData Tests

    [Fact]
    public void AddManagerPropertiesToAdditionalData_WithNullAdditionalData_CreatesNewDictionary()
    {
        // Arrange
        var manager = new DirectoryObject
        {
            AdditionalData = null
        };
        
        var managerDetails = new User();
        // Set the DisplayName property explicitly
        typeof(User).GetProperty("DisplayName").SetValue(managerDetails, "Manager Name");

        // Act
        _addManagerPropertiesToAdditionalDataMethod.Invoke(_graphService, new object[] { manager, managerDetails });

        // Assert
        Assert.NotNull(manager.AdditionalData);
    }

    [Fact]
    public void AddManagerPropertiesToAdditionalData_WithManagerDetails_AddsAllProperties()
    {
        // Arrange
        var manager = new DirectoryObject
        {
            AdditionalData = new Dictionary<string, object>()
        };
        
        var managerDetails = new User
        {
            DisplayName = "Manager Name",
            GivenName = "Manager",
            Surname = "Name",
            UserPrincipalName = "manager@example.com",
            Mail = "manager@example.com"
        };

        // Act
        _addManagerPropertiesToAdditionalDataMethod.Invoke(_graphService, new object[] { manager, managerDetails });

        // Assert
        Assert.Equal("Manager Name", manager.AdditionalData["displayName"]);
        Assert.Equal("Manager", manager.AdditionalData["givenName"]);
        Assert.Equal("Name", manager.AdditionalData["surname"]);
        Assert.Equal("manager@example.com", manager.AdditionalData["userPrincipalName"]);
        Assert.Equal("manager@example.com", manager.AdditionalData["mail"]);
    }

    [Fact]
    public void AddManagerPropertiesToAdditionalData_WithNullManagerProperties_DoesNotAddNullProperties()
    {
        // Arrange
        var manager = new DirectoryObject
        {
            AdditionalData = new Dictionary<string, object>()
        };
        
        var managerDetails = new User
        {
            DisplayName = "Manager Name",
            GivenName = null,
            Surname = "Name",
            UserPrincipalName = null,
            Mail = "manager@example.com"
        };

        // Act
        _addManagerPropertiesToAdditionalDataMethod.Invoke(_graphService, new object[] { manager, managerDetails });

        // Assert
        Assert.Equal("Manager Name", manager.AdditionalData["displayName"]);
        Assert.Equal("Name", manager.AdditionalData["surname"]);
        Assert.Equal("manager@example.com", manager.AdditionalData["mail"]);
        Assert.False(manager.AdditionalData.ContainsKey("givenName"));
        Assert.False(manager.AdditionalData.ContainsKey("userPrincipalName"));
    }

    #endregion
}

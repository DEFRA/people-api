using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Defra_People_API.Cache;
using Defra_People_API.Services;
using Defra_People_API.Services.Graph;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Moq;
using Xunit;

namespace defra.people_api_tests.Services.Graph;

public class GraphServiceQueriesTests
{
    private readonly Mock<IGraphClientFactory> _mockGraphClientFactory;
    private readonly Mock<IUserCache> _mockUserCache;
    private readonly GraphService _graphService;

    public GraphServiceQueriesTests()
    {
        _mockGraphClientFactory = new Mock<IGraphClientFactory>();
        _mockUserCache = new Mock<IUserCache>();
        _graphService = new GraphService(_mockGraphClientFactory.Object, _mockUserCache.Object);
    }

    #region GetUserByMailNickname Tests

    [Fact]
    public async Task GetUserByMailNickname_WhenUserInCache_ReturnsCachedUser()
    {
        // Arrange
        var mailNickname = "test-user";
        var cachedUsers = new List<User> { new User { DisplayName = "Test User" } };
        
        _mockUserCache.Setup(x => x.GetUserFromCache(mailNickname))
            .ReturnsAsync(cachedUsers);

        // Act
        var result = await _graphService.GetUserByMailNickname(mailNickname, false);

        // Assert
        Assert.Same(cachedUsers, result);
        _mockGraphClientFactory.Verify(x => x.CreateClient(), Times.Never);
    }

    #endregion

    #region GetUserBySAMAccount Tests

    [Fact]
    public async Task GetUserBySAMAccount_WhenUserInCache_ReturnsCachedUser()
    {
        // Arrange
        var samAccountName = "test-user";
        var cachedUsers = new List<User> { new User { DisplayName = "Test User" } };
        
        _mockUserCache.Setup(x => x.GetUserFromCache(samAccountName))
            .ReturnsAsync(cachedUsers);

        // Act
        var result = await _graphService.GetUserBySAMAccount(samAccountName, false);

        // Assert
        Assert.Same(cachedUsers, result);
        _mockGraphClientFactory.Verify(x => x.CreateClient(), Times.Never);
    }

    #endregion

    #region GetUserBySID Tests

    [Fact]
    public async Task GetUserBySID_WhenUserInCache_ReturnsCachedUser()
    {
        // Arrange
        var sid = "test-sid";
        var cachedUsers = new List<User> { new User { DisplayName = "Test User" } };
        
        _mockUserCache.Setup(x => x.GetUserFromCache(sid))
            .ReturnsAsync(cachedUsers);

        // Act
        var result = await _graphService.GetUserBySID(sid, false);

        // Assert
        Assert.Same(cachedUsers, result);
        _mockGraphClientFactory.Verify(x => x.CreateClient(), Times.Never);
    }

    #endregion
}

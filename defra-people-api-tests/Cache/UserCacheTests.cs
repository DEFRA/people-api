using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Defra_People_API.Cache;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Models;
using Moq;
using Xunit;

namespace defra.people_api_tests.Cache;

public class UserCacheTests
{
    private readonly Mock<ILogger<UserCache>> _mockLogger;
    private readonly UserCache _userCache;

    public UserCacheTests()
    {
        _mockLogger = new Mock<ILogger<UserCache>>();
        _userCache = new UserCache(_mockLogger.Object);
    }

    [Fact]
    public async Task AddUserToCache_AddsUserToCache()
    {
        // Arrange
        var user = new User
        {
            EmployeeId = "123",
            OnPremisesSamAccountName = "user123",
            MailNickname = "user.123"
        };

        // Act
        await _userCache.AddUserToCache(new[] { user });
        var result = await _userCache.GetUserFromCache("123");

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("123", result.First().EmployeeId);
    }

    [Fact]
    public async Task GetUserFromCache_WhenUserExists_ReturnsUser()
    {
        // Arrange
        var user1 = new User
        {
            EmployeeId = "123",
            OnPremisesSamAccountName = "user123",
            MailNickname = "user.123"
        };
        
        var user2 = new User
        {
            EmployeeId = "456",
            OnPremisesSamAccountName = "user456",
            MailNickname = "user.456"
        };

        await _userCache.AddUserToCache(new[] { user1, user2 });

        // Act
        var result = await _userCache.GetUserFromCache("456");

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("456", result.First().EmployeeId);
    }

    [Fact]
    public async Task GetUserFromCache_WhenUserDoesNotExist_ReturnsEmptyCollection()
    {
        // Arrange
        var user = new User
        {
            EmployeeId = "123",
            OnPremisesSamAccountName = "user123",
            MailNickname = "user.123"
        };

        await _userCache.AddUserToCache(new[] { user });

        // Act
        var result = await _userCache.GetUserFromCache("999");

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task DumpCache_ReturnsAllCachedUsers()
    {
        // Arrange
        var user1 = new User
        {
            EmployeeId = "123",
            OnPremisesSamAccountName = "user123",
            MailNickname = "user.123"
        };
        
        var user2 = new User
        {
            EmployeeId = "456",
            OnPremisesSamAccountName = "user456",
            MailNickname = "user.456"
        };

        await _userCache.AddUserToCache(new[] { user1, user2 });

        // Act
        var result = await _userCache.DumpCache();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count());
        Assert.Contains(result, u => u.EmployeeId == "123");
        Assert.Contains(result, u => u.EmployeeId == "456");
    }

    [Fact]
    public async Task PurgeCache_ClearsAllCachedUsers()
    {
        // Arrange
        var user1 = new User
        {
            EmployeeId = "123",
            OnPremisesSamAccountName = "user123",
            MailNickname = "user.123"
        };
        
        var user2 = new User
        {
            EmployeeId = "456",
            OnPremisesSamAccountName = "user456",
            MailNickname = "user.456"
        };

        await _userCache.AddUserToCache(new[] { user1, user2 });

        // Act
        await _userCache.PurgeCache();
        var result = await _userCache.DumpCache();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }
}

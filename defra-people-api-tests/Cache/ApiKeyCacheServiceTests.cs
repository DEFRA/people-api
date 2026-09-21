using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Defra_People_API.Cache;
using Defra_People_API.Entities;
using Defra_People_API.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace defra.people_api_tests.Cache;

public class ApiKeyCacheServiceTests
{
    private readonly Mock<ILogger<ApiKeyCacheService>> _mockLogger;
    private readonly Mock<IEncryptionService> _mockEncryptionService;
    private readonly string _testKeysDirectory;
    private readonly ApiKeyCacheService _apiKeyCacheService;

    public ApiKeyCacheServiceTests()
    {
        _mockLogger = new Mock<ILogger<ApiKeyCacheService>>();
        _mockEncryptionService = new Mock<IEncryptionService>();
        
        // Set up encryption service mock
        _mockEncryptionService
            .Setup(x => x.Encrypt(It.IsAny<string>()))
            .Returns(("encrypted-key", "encryption-key"));
            
        _mockEncryptionService
            .Setup(x => x.Decrypt(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((encryptedKey, encryptionKey) => "decrypted-key");
        
        _apiKeyCacheService = new ApiKeyCacheService(_mockLogger.Object, _mockEncryptionService.Object);
        
        // Get the test keys directory from the ApiKeyCacheService using reflection
        var field = typeof(ApiKeyCacheService).GetField("_keysDirectory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        _testKeysDirectory = (string)field.GetValue(_apiKeyCacheService);
    }

    [Fact]
    public void IsValidKey_WhenKeyExists_ReturnsTrue()
    {
        // Arrange
        var accessKey = new AccessKey
        {
            Key = "test-key",
            Consumer = "Test Consumer",
            Active = true,
            Expires = DateTime.UtcNow.AddDays(30)
        };

        // Use reflection to access the private _keyCache field
        var field = typeof(ApiKeyCacheService).GetField("_keyCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var keyCache = (System.Collections.Generic.Dictionary<string, AccessKey>)field.GetValue(_apiKeyCacheService);
        keyCache.Add("test-key", accessKey);

        // Act
        var result = _apiKeyCacheService.IsValidKey("test-key");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValidKey_WhenKeyDoesNotExist_ReturnsFalse()
    {
        // Act
        var result = _apiKeyCacheService.IsValidKey("non-existent-key");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetAllKeys_ReturnsAllKeys()
    {
        // Arrange
        var accessKey1 = new AccessKey
        {
            Key = "test-key-1",
            Consumer = "Test Consumer 1",
            Active = true,
            Expires = DateTime.UtcNow.AddDays(30)
        };
        
        var accessKey2 = new AccessKey
        {
            Key = "test-key-2",
            Consumer = "Test Consumer 2",
            Active = true,
            Expires = DateTime.UtcNow.AddDays(30)
        };

        // Use reflection to access the private _keyCache field
        var field = typeof(ApiKeyCacheService).GetField("_keyCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var keyCache = (System.Collections.Generic.Dictionary<string, AccessKey>)field.GetValue(_apiKeyCacheService);
        keyCache.Add("test-key-1", accessKey1);
        keyCache.Add("test-key-2", accessKey2);

        // Act
        var result = await _apiKeyCacheService.GetAllKeys();

        // Assert
        Assert.Equal(2, result.Count());
        Assert.Contains(result, k => k.Key == "test-key-1");
        Assert.Contains(result, k => k.Key == "test-key-2");
    }

    [Fact]
    public async Task SaveKeyToFile_SavesKeyToFileAndAddsToCache()
    {
        // Arrange
        var accessKey = new AccessKey
        {
            Key = "test-key",
            Consumer = "Test Consumer",
            Active = true,
            Expires = DateTime.UtcNow.AddDays(30)
        };

        // Act
        await _apiKeyCacheService.SaveKeyToFile(accessKey);

        // Assert
        // Verify the key was added to the cache
        Assert.True(_apiKeyCacheService.IsValidKey("test-key"));
        
        // Verify the encryption service was called
        _mockEncryptionService.Verify(x => x.Encrypt("test-key"), Times.Once);
        
        // Verify a file was created in the keys directory
        Assert.True(Directory.Exists(_testKeysDirectory));
        var keyFiles = Directory.GetFiles(_testKeysDirectory, "*.key");
        Assert.Contains(keyFiles, f => f.Contains("Test_Consumer"));
        
        // Note: We're not cleaning up the files here to avoid file access issues
        // These will be cleaned up when the test runner exits
    }

    [Fact]
    public void RefreshCache_LogsRefreshingMessage()
    {
        // Act
        _apiKeyCacheService.RefreshCache();

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                It.Is<Microsoft.Extensions.Logging.LogLevel>(l => l == Microsoft.Extensions.Logging.LogLevel.Information),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Refreshing API key cache")),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()),
            Times.Once);
    }
}

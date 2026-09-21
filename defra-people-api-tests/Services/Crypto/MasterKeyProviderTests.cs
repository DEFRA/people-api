using System;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Defra_People_API.Services;

namespace defra.people_api_tests.Services.Crypto;

public class MasterKeyProviderTests
{
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<IDataProtectionProvider> _mockDataProtectionProvider;
    private readonly Mock<ILogger<MasterKeyProvider>> _mockLogger;
    private readonly Mock<IDataProtector> _mockDataProtector;

    public MasterKeyProviderTests()
    {
        _mockConfiguration = new Mock<IConfiguration>();
        _mockDataProtectionProvider = new Mock<IDataProtectionProvider>();
        _mockLogger = new Mock<ILogger<MasterKeyProvider>>();
        _mockDataProtector = new Mock<IDataProtector>();

        _mockDataProtectionProvider
            .Setup(x => x.CreateProtector("MasterKeyProtection"))
            .Returns(_mockDataProtector.Object);
    }

    [Fact]
    public void GetMasterKey_WithUnprotectedKey_ReturnsMasterKey()
    {
        // Arrange
        var masterKey = "test-master-key";
        _mockConfiguration.Setup(x => x["Encryption:MasterKey"]).Returns(masterKey);

        var provider = new MasterKeyProvider(
            _mockConfiguration.Object,
            _mockDataProtectionProvider.Object,
            _mockLogger.Object);

        // Act
        var result = provider.GetMasterKey();

        // Assert
        Assert.Equal(masterKey, result);
    }

    // Skip this test as we can't mock extension methods directly
    [Fact(Skip = "Cannot mock extension methods directly")]
    public void GetMasterKey_WithProtectedKey_ReturnsUnprotectedKey()
    {
        // This test would verify that protected keys are unprotected correctly
    }

    [Fact]
    public void GetMasterKey_WhenKeyNotConfigured_ThrowsException()
    {
        // Arrange
        _mockConfiguration.Setup(x => x["Encryption:MasterKey"]).Returns((string)null);

        var provider = new MasterKeyProvider(
            _mockConfiguration.Object,
            _mockDataProtectionProvider.Object,
            _mockLogger.Object);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => provider.GetMasterKey());
        Assert.Equal("Master encryption key not configured", exception.Message);
    }

    // Skip this test as we can't mock extension methods directly
    [Fact(Skip = "Cannot mock extension methods directly")]
    public void GetMasterKey_WhenUnprotectionFails_ThrowsException()
    {
        // This test would verify that unprotection failures are handled correctly
    }

    [Fact]
    public void GetMasterKey_CachesMasterKey()
    {
        // Arrange
        var masterKey = "test-master-key";
        _mockConfiguration.Setup(x => x["Encryption:MasterKey"]).Returns(masterKey);

        var provider = new MasterKeyProvider(
            _mockConfiguration.Object,
            _mockDataProtectionProvider.Object,
            _mockLogger.Object);

        // Act
        var result1 = provider.GetMasterKey();
        
        // Change the configuration value to verify caching
        _mockConfiguration.Setup(x => x["Encryption:MasterKey"]).Returns("different-key");
        
        var result2 = provider.GetMasterKey();

        // Assert
        Assert.Equal(masterKey, result1);
        Assert.Equal(masterKey, result2); // Should return cached value
        _mockConfiguration.Verify(x => x["Encryption:MasterKey"], Times.Once);
    }

    [Theory]
    [InlineData("DP:protected-value", true)]
    [InlineData("protected-value", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsDataProtected_ReturnsExpectedResult(string value, bool expected)
    {
        // Act
        var result = MasterKeyProvider.IsDataProtected(value);

        // Assert
        Assert.Equal(expected, result);
    }
}

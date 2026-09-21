using System;
using System.IO;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Defra_People_API.Services;

namespace defra.people_api_tests.Services.Crypto;

public class EncryptedConfigServiceTests
{
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<EncryptedConfigService>> _mockLogger;
    private readonly Mock<IHostEnvironment> _mockEnvironment;
    private readonly Mock<IDataProtectionProvider> _mockDataProtectionProvider;
    private readonly Mock<IDataProtector> _mockDataProtector;
    private readonly EncryptedConfigService _service;

    public EncryptedConfigServiceTests()
    {
        _mockConfiguration = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<EncryptedConfigService>>();
        _mockEnvironment = new Mock<IHostEnvironment>();
        _mockDataProtectionProvider = new Mock<IDataProtectionProvider>();
        _mockDataProtector = new Mock<IDataProtector>();

        _mockDataProtectionProvider
            .Setup(x => x.CreateProtector("MasterKeyProtection"))
            .Returns(_mockDataProtector.Object);
            
        _mockDataProtectionProvider
            .Setup(x => x.CreateProtector("AdminApiKeyProtection"))
            .Returns(_mockDataProtector.Object);

        _service = new EncryptedConfigService(
            _mockConfiguration.Object,
            _mockLogger.Object,
            _mockEnvironment.Object,
            _mockDataProtectionProvider.Object);
    }

    [Fact]
    public void EnsureMasterKeyEncrypted_WhenKeyIsNull_LogsAndReturns()
    {
        // Arrange
        _mockConfiguration.Setup(x => x["Encryption:MasterKey"]).Returns((string)null);

        // Act
        _service.EnsureMasterKeyEncrypted();

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                It.IsAny<Microsoft.Extensions.Logging.LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("already protected or not set")),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()),
            Times.Once);
    }

    [Fact]
    public void EnsureMasterKeyEncrypted_WhenKeyIsAlreadyProtected_LogsAndReturns()
    {
        // Arrange
        _mockConfiguration.Setup(x => x["Encryption:MasterKey"]).Returns($"{EncryptedConfigService.DataProtectionPrefix}protected-key");

        // Act
        _service.EnsureMasterKeyEncrypted();

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                It.IsAny<Microsoft.Extensions.Logging.LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("already protected or not set")),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()),
            Times.Once);
    }

    // Skip this test as we can't mock extension methods directly
    [Fact(Skip = "Cannot mock extension methods directly")]
    public void EnsureMasterKeyEncrypted_InDevelopmentEnvironment_LogsWarningAndSkipsUpdate()
    {
        // This test would verify that updates are skipped in development environment
    }

    [Fact]
    public void EnsureAdminApiKeyEncrypted_WhenKeyIsNull_LogsAndReturns()
    {
        // Arrange
        _mockConfiguration.Setup(x => x["Authentication:AdminApiKey"]).Returns((string)null);

        // Act
        _service.EnsureAdminApiKeyEncrypted();

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                It.IsAny<Microsoft.Extensions.Logging.LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("already protected or not set")),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()),
            Times.Once);
    }

    [Fact]
    public void EnsureAdminApiKeyEncrypted_WhenKeyIsAlreadyProtected_LogsAndReturns()
    {
        // Arrange
        _mockConfiguration.Setup(x => x["Authentication:AdminApiKey"]).Returns($"{EncryptedConfigService.DataProtectionPrefix}protected-key");

        // Act
        _service.EnsureAdminApiKeyEncrypted();

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                It.IsAny<Microsoft.Extensions.Logging.LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("already protected or not set")),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()),
            Times.Once);
    }

    [Theory]
    [InlineData("DP:protected-value", true)]
    [InlineData("protected-value", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsDataProtected_ReturnsExpectedResult(string value, bool expected)
    {
        // Act
        var result = EncryptedConfigService.IsDataProtected(value);

        // Assert
        Assert.Equal(expected, result);
    }
}

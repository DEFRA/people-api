using System;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Defra_People_API.Services;

namespace defra.people_api_tests.Services.Crypto;

public class EncryptionServiceTests
{
    private readonly Mock<ILogger<EncryptionService>> _mockLogger;
    private readonly Mock<IMasterKeyProvider> _mockMasterKeyProvider;
    private readonly EncryptionService _encryptionService;

    public EncryptionServiceTests()
    {
        _mockLogger = new Mock<ILogger<EncryptionService>>();
        _mockMasterKeyProvider = new Mock<IMasterKeyProvider>();
        
        // Set up a consistent master key for testing
        _mockMasterKeyProvider.Setup(x => x.GetMasterKey()).Returns("test-master-key-that-is-long-enough-for-aes-256");
        
        _encryptionService = new EncryptionService(_mockLogger.Object, _mockMasterKeyProvider.Object);
    }

    [Fact]
    public void Encrypt_ReturnsEncryptedTextAndKey()
    {
        // Arrange
        var plainText = "test-plain-text";

        // Act
        var (encryptedText, encryptionKey) = _encryptionService.Encrypt(plainText);

        // Assert
        Assert.NotNull(encryptedText);
        Assert.NotNull(encryptionKey);
        Assert.NotEqual(plainText, encryptedText);
    }

    [Fact]
    public void Decrypt_WithValidEncryptedTextAndKey_ReturnsOriginalText()
    {
        // Arrange
        var plainText = "test-plain-text";
        var (encryptedText, encryptionKey) = _encryptionService.Encrypt(plainText);

        // Act
        var decryptedText = _encryptionService.Decrypt(encryptedText, encryptionKey);

        // Assert
        Assert.Equal(plainText, decryptedText);
    }

    [Fact]
    public void Encrypt_WithEmptyText_ReturnsEncryptedEmptyString()
    {
        // Arrange
        var plainText = "";

        // Act
        var (encryptedText, encryptionKey) = _encryptionService.Encrypt(plainText);

        // Assert
        Assert.NotNull(encryptedText);
        Assert.NotNull(encryptionKey);
        Assert.NotEqual(plainText, encryptedText);
        
        // Decrypt to verify
        var decryptedText = _encryptionService.Decrypt(encryptedText, encryptionKey);
        Assert.Equal(plainText, decryptedText);
    }

    [Fact]
    public void Encrypt_WithLongText_ReturnsEncryptedText()
    {
        // Arrange
        var plainText = new string('A', 10000); // 10KB of data

        // Act
        var (encryptedText, encryptionKey) = _encryptionService.Encrypt(plainText);

        // Assert
        Assert.NotNull(encryptedText);
        Assert.NotNull(encryptionKey);
        
        // Decrypt to verify
        var decryptedText = _encryptionService.Decrypt(encryptedText, encryptionKey);
        Assert.Equal(plainText, decryptedText);
    }

    [Fact]
    public void Decrypt_WithInvalidEncryptedText_ThrowsException()
    {
        // Arrange
        var (_, encryptionKey) = _encryptionService.Encrypt("test");
        var invalidEncryptedText = "invalid-base64-data";

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => 
            _encryptionService.Decrypt(invalidEncryptedText, encryptionKey));
    }

    [Fact]
    public void Decrypt_WithInvalidEncryptionKey_ThrowsException()
    {
        // Arrange
        var (encryptedText, _) = _encryptionService.Encrypt("test");
        var invalidEncryptionKey = "invalid-encryption-key";

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => 
            _encryptionService.Decrypt(encryptedText, invalidEncryptionKey));
    }

    [Fact]
    public void EncryptAndDecrypt_WithDifferentInstances_WorksCorrectly()
    {
        // Arrange
        var plainText = "test-plain-text";
        var (encryptedText, encryptionKey) = _encryptionService.Encrypt(plainText);
        
        // Create a new instance with the same master key
        var newEncryptionService = new EncryptionService(_mockLogger.Object, _mockMasterKeyProvider.Object);

        // Act
        var decryptedText = newEncryptionService.Decrypt(encryptedText, encryptionKey);

        // Assert
        Assert.Equal(plainText, decryptedText);
    }
}

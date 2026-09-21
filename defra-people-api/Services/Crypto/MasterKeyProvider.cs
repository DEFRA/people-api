using Microsoft.AspNetCore.DataProtection;

namespace Defra_People_API.Services;

public class MasterKeyProvider : IMasterKeyProvider
{
    private readonly IConfiguration _configuration;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ILogger<MasterKeyProvider> _logger;
    private string? _cachedMasterKey;
    private const string MasterKeyPurpose = "MasterKeyProtection";

    public MasterKeyProvider(
        IConfiguration configuration,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<MasterKeyProvider> logger)
    {
        _configuration = configuration;
        _dataProtectionProvider = dataProtectionProvider;
        _logger = logger;
    }

    public string GetMasterKey()
    {
        if (_cachedMasterKey != null)
        {
            return _cachedMasterKey;
        }

        var masterKey = _configuration["Encryption:MasterKey"];
        
        if (string.IsNullOrEmpty(masterKey))
        {
            _logger.LogError("MasterKey is not configured");
            throw new InvalidOperationException("Master encryption key not configured");
        }

        if (IsDataProtected(masterKey))
        {
            try
            {
                _cachedMasterKey = UnprotectValue(masterKey);
                return _cachedMasterKey;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unprotect MasterKey");
                throw new InvalidOperationException("Failed to unprotect MasterKey", ex);
            }
        }

        _cachedMasterKey = masterKey;
        return masterKey;
    }

    public static bool IsDataProtected(string value)
    {
        return !string.IsNullOrEmpty(value) && value.StartsWith(EncryptedConfigService.DataProtectionPrefix);
    }

    private string UnprotectValue(string protectedValue)
    {
        if (!IsDataProtected(protectedValue))
        {
            return protectedValue;
        }

        var protector = _dataProtectionProvider.CreateProtector(MasterKeyPurpose);
        return protector.Unprotect(protectedValue.Substring(EncryptedConfigService.DataProtectionPrefix.Length));
    }
}

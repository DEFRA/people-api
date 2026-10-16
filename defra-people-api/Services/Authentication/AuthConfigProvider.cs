using Defra_People_API.Entities;
using Microsoft.AspNetCore.DataProtection;

namespace Defra_People_API.Services;

public class AuthConfigProvider : IAuthConfigProvider
{
    private const string AdminApiKeyPurpose = "AdminApiKeyProtection";

    private readonly IConfiguration _configuration;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ILogger<AuthConfigProvider> _logger;

    public AuthConfigProvider(
        IConfiguration configuration,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<AuthConfigProvider> logger)
    {
        _configuration = configuration;
        _dataProtectionProvider = dataProtectionProvider;
        _logger = logger;
    }

    public AuthenticationConfig GetAuthConfig()
    {
        var adminApiKey = _configuration["Authentication:AdminApiKey"] ?? string.Empty;

        if (EncryptedConfigService.IsDataProtected(adminApiKey))
        {
            try
            {
                var protector = _dataProtectionProvider.CreateProtector(AdminApiKeyPurpose);
                adminApiKey = protector.Unprotect(adminApiKey.Substring(EncryptedConfigService.DataProtectionPrefix.Length));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unprotect AdminApiKey");
                throw new InvalidOperationException("Failed to unprotect AdminApiKey", ex);
            }
        }

        return new AuthenticationConfig { AdminApiKey = adminApiKey };
    }
}

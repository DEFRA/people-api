using Defra_People_API.Entities;
using Microsoft.AspNetCore.DataProtection;

namespace Defra_People_API.Services;

public class AuthConfigProvider : IAuthConfigProvider
{
    private readonly IConfiguration _configuration;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ILogger<AuthConfigProvider> _logger;
    private const string AdminApiKeyPurpose = "AdminApiKeyProtection";
    
    public AuthConfigProvider(IConfiguration configuration, IDataProtectionProvider dataProtectionProvider, ILogger<AuthConfigProvider> logger)
    {
        _configuration = configuration;
        _dataProtectionProvider = dataProtectionProvider;
        _logger = logger;
    }
    
    public AuthenticationConfig GetAuthConfig()
    {
        var authConfig = _configuration.GetSection("Authentication").Get<AuthenticationConfig>()!;
        
        // AdminApiKey is protected?
        if (!string.IsNullOrEmpty(authConfig.AdminApiKey) && 
            EncryptedConfigService.IsDataProtected(authConfig.AdminApiKey))
        {
            try
            {
                var protector = _dataProtectionProvider.CreateProtector(AdminApiKeyPurpose);
                var protectedValue = authConfig.AdminApiKey.Substring(EncryptedConfigService.DataProtectionPrefix.Length);
                authConfig.AdminApiKey = protector.Unprotect(protectedValue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unprotect AdminApiKey");
            }
        }
        
        return authConfig;
    }
}

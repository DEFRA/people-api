using System.Text.Json;
using Defra_People_API.Entities;
using Microsoft.AspNetCore.DataProtection;

namespace Defra_People_API.Services;

public class EncryptedConfigService : IEncryptedConfigService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EncryptedConfigService> _logger;
    private readonly IHostEnvironment _environment;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    public const string DataProtectionPrefix = "DP:";
    private const string MasterKeyPurpose = "MasterKeyProtection";
    private const string AdminApiKeyPurpose = "AdminApiKeyProtection";

    public EncryptedConfigService(
        IConfiguration configuration,
        ILogger<EncryptedConfigService> logger,
        IHostEnvironment environment,
        IDataProtectionProvider dataProtectionProvider)
    {
        _configuration = configuration;
        _logger = logger;
        _environment = environment;
        _dataProtectionProvider = dataProtectionProvider;
    }

    public void EnsureMasterKeyEncrypted()
    {
        try
        {
            var masterKey = _configuration["Encryption:MasterKey"];
            
            // If the key is empty or already protected skip
            if (string.IsNullOrEmpty(masterKey) || IsDataProtected(masterKey))
            {
                _logger.LogInformation("MasterKey is already protected or not set");
                return;
            }

            _logger.LogInformation("Protecting MasterKey with Data Protection API");
            
            var protector = _dataProtectionProvider.CreateProtector(MasterKeyPurpose);
            var protectedValue = $"{DataProtectionPrefix}{protector.Protect(masterKey)}";
            
            UpdateAppSettings("Encryption:MasterKey", protectedValue);
            
            _logger.LogInformation("MasterKey has been protected successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error protecting MasterKey");
            throw;
        }
    }

    public void EnsureAdminApiKeyEncrypted()
    {
        try
        {
            var adminApiKey = _configuration["Authentication:AdminApiKey"];
            
            // If the key is empty or already protected skip
            if (string.IsNullOrEmpty(adminApiKey) || IsDataProtected(adminApiKey))
            {
                _logger.LogInformation("AdminApiKey is already protected or not set");
                return;
            }

            _logger.LogInformation("Protecting AdminApiKey with Data Protection API");
            
            var protector = _dataProtectionProvider.CreateProtector(AdminApiKeyPurpose);
            var protectedValue = $"{DataProtectionPrefix}{protector.Protect(adminApiKey)}";
            
            UpdateAppSettings("Authentication:AdminApiKey", protectedValue);
            
            _logger.LogInformation("AdminApiKey has been protected successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error protecting AdminApiKey");
            throw;
        }
    }
    
    public static bool IsDataProtected(string value)
    {
        return !string.IsNullOrEmpty(value) && value.StartsWith(DataProtectionPrefix);
    }

    private void UpdateAppSettings(string keyPath, string newValue)
    {
        if(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
        {
            _logger.LogWarning("Skipping appsettings.json update in Development environment");
            Console.WriteLine($"\n\n/////////\n//\n// Updating AppSettings Disabled in Development. Update your local config.\n//\n// \"{keyPath}\": \"{newValue}\"\n//\n/////////");
            return;
        }

        var appSettingsPath = Path.Combine(_environment.ContentRootPath, "appsettings.json");
        
        if (!File.Exists(appSettingsPath))
        {
            _logger.LogError("appsettings.json file not found at {Path}", appSettingsPath);
            throw new FileNotFoundException("appsettings.json file not found", appSettingsPath);
        }

        try
        {
            var json = File.ReadAllText(appSettingsPath);
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            
            // Create an object with the updated value
            var updatedJson = new Dictionary<string, JsonElement>();
            var sections = keyPath.Split(':');
            
            foreach (var property in root.EnumerateObject())
            {
                if (property.Name == sections[0])
                {
                    if (sections.Length == 1)
                    {
                        updatedJson.Add(property.Name, JsonSerializer.SerializeToElement(newValue, options));
                    }
                    else
                    {
                        var nestedProps = new Dictionary<string, JsonElement>();
                        foreach (var nestedProp in property.Value.EnumerateObject())
                        {
                            if (nestedProp.Name == sections[1])
                            {
                                continue;
                            }
                            nestedProps.Add(nestedProp.Name, nestedProp.Value.Clone());
                        }
                        
                        // Add the updated nested property
                        var nestedObjDict = new Dictionary<string, string>
                        {
                            { sections[1], newValue }
                        };
                        var nestedObj = JsonSerializer.SerializeToElement(nestedObjDict, options);
                        
                        foreach (var nestedProp in nestedObj.EnumerateObject())
                        {
                            nestedProps.Add(nestedProp.Name, nestedProp.Value.Clone());
                        }
                        
                        updatedJson.Add(property.Name, JsonSerializer.SerializeToElement(nestedProps, options));
                    }
                }
                else
                {
                    // Keep everything else the same
                    updatedJson.Add(property.Name, property.Value.Clone());
                }
            }
            
            var updatedJsonString = JsonSerializer.Serialize(updatedJson, options);
            File.WriteAllText(appSettingsPath, updatedJsonString);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating appsettings.json");
            throw;
        }
    }
}

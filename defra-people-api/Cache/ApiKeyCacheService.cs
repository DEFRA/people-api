using System.Text.Json;
using Defra_People_API.Entities;
using Defra_People_API.Services;

namespace Defra_People_API.Cache;

public class ApiKeyCacheService : IApiKeyCacheService
{
    private readonly Dictionary<string, AccessKey> _keyCache = new();
    private readonly ILogger<ApiKeyCacheService> _logger;
    private readonly string _keysDirectory;
    private readonly IEncryptionService _encryptionService;

    public ApiKeyCacheService(ILogger<ApiKeyCacheService> logger, IEncryptionService encryptionService)
    {
        _logger = logger;
        _encryptionService = encryptionService;
        _keysDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Keys");
    }

    public void LoadKeysFromDirectory()
    {
        _logger.LogInformation("Loading API keys from directory: {Directory}", _keysDirectory);
        
        try
        {
            _keyCache.Clear();
            
            if (!Directory.Exists(_keysDirectory))
            {
                Directory.CreateDirectory(_keysDirectory);
                _logger.LogInformation("Created keys directory: {Directory}", _keysDirectory);
                return;
            }
            
            // Get all .json files in the directory
            var keyFiles = Directory.GetFiles(_keysDirectory, "*.key");
            _logger.LogInformation("Found {Count} key files", keyFiles.Length);
            
            foreach (var file in keyFiles)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var keyInfo = JsonSerializer.Deserialize<AccessKey>(json);
                    
                    if (keyInfo == null)
                    {
                        _logger.LogWarning("Failed to deserialize key file: {File}", file);
                        continue;
                    }
                    
                    if (!keyInfo.Active || keyInfo.Expires < DateTime.UtcNow)
                    {
                        _logger.LogInformation("Skipping expired or inactive key: {File}", file);
                        continue;
                    }
                    
                    try
                    {
                        // Decrypt the keys
                        if (!string.IsNullOrEmpty(keyInfo.EncryptedKey) && !string.IsNullOrEmpty(keyInfo.EncryptionKey))
                        {
                            keyInfo.Key = _encryptionService.Decrypt(keyInfo.EncryptedKey, keyInfo.EncryptionKey);
                            _logger.LogInformation("Decrypted key for consumer: {Consumer}", keyInfo.Consumer);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error decrypting key for consumer: {Consumer}", keyInfo.Consumer);
                        continue;
                    }
                    
                    _keyCache[keyInfo.Key] = keyInfo;
                    _logger.LogInformation("Loaded key for consumer: {Consumer}", keyInfo.Consumer);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading key file: {File}", file);
                }
            }
            
            _logger.LogInformation("Successfully loaded {Count} active API keys", _keyCache.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading API keys from directory");
        }
    }

    public void RefreshCache()
    {
        _logger.LogInformation("Refreshing API key cache");
        LoadKeysFromDirectory();
    }

    public bool IsValidKey(string key)
    {
        return _keyCache.ContainsKey(key);
    }

    public async Task<IEnumerable<AccessKey>> GetAllKeys()
    {
        return await Task.Run(() =>
        {
            return _keyCache.Values;
        });
    }

    public async Task SaveKeyToFile(AccessKey keyInfo)
    {
        if (!Directory.Exists(_keysDirectory))
        {
            Directory.CreateDirectory(_keysDirectory);
        }
        
        string originalKey = keyInfo.Key;
        
        try
        {
            var (encryptedKey, encryptionKey) = _encryptionService.Encrypt(originalKey);
            
            keyInfo.EncryptedKey = encryptedKey;
            keyInfo.EncryptionKey = encryptionKey;
            
            var filename = $"{keyInfo.Consumer.Replace(" ", "_")}_{DateTime.UtcNow.Ticks}.key";
            var filePath = Path.Combine(_keysDirectory, filename);
            
            var json = JsonSerializer.Serialize(keyInfo, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);
            
            keyInfo.Key = originalKey;
            _keyCache[originalKey] = keyInfo;
            
            _logger.LogInformation("Saved new encrypted API key to file: {File}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error encrypting and saving API key");
            throw;
        }
    }
}

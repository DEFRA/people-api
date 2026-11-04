using System.Text.Json;
using Defra_People_API.Entities;
using Defra_People_API.Services;

namespace Defra_People_API.Cache;

public class ApiKeyCacheService : IApiKeyCacheService
{
    private readonly Dictionary<string, AccessKey> _keyCache = new();
    private readonly ILogger<ApiKeyCacheService> _logger;
    private readonly IApiKeyBlobStore _blobStore;
    private readonly IEncryptionService _encryptionService;

    public ApiKeyCacheService(ILogger<ApiKeyCacheService> logger, IEncryptionService encryptionService, IApiKeyBlobStore blobStore)
    {
        _logger = logger;
        _encryptionService = encryptionService;
        _blobStore = blobStore;
    }

    public void LoadKeysFromDirectory()
    {
        _logger.LogInformation("Loading API keys from Blob storage");
        
        try
        {
            _keyCache.Clear();
            
            var keyBlobNames = _blobStore.ListKeyBlobNames();
            _logger.LogInformation("Found {Count} key blobs", keyBlobNames.Count);
            
            foreach (var blobName in keyBlobNames)
            {
                try
                {
                    var json = _blobStore.DownloadText(blobName);
                    var keyInfo = JsonSerializer.Deserialize<AccessKey>(json);
                    
                    if (keyInfo == null)
                    {
                        _logger.LogWarning("Failed to deserialize key blob: {Blob}", blobName);
                        continue;
                    }
                    
                    if (!keyInfo.Active || keyInfo.Expires < DateTime.UtcNow)
                    {
                        _logger.LogInformation("Skipping expired or inactive key: {Blob}", blobName);
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
                    _logger.LogError(ex, "Error loading key blob: {Blob}", blobName);
                }
            }
            
            _logger.LogInformation("Successfully loaded {Count} active API keys", _keyCache.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading API keys from Blob storage");
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
        string originalKey = keyInfo.Key;
        
        try
        {
            var (encryptedKey, encryptionKey) = _encryptionService.Encrypt(originalKey);
            
            keyInfo.EncryptedKey = encryptedKey;
            keyInfo.EncryptionKey = encryptionKey;
            
            var blobName = $"{keyInfo.Consumer.Replace(" ", "_")}_{DateTime.UtcNow.Ticks}.key";
            
            var json = JsonSerializer.Serialize(keyInfo, new JsonSerializerOptions { WriteIndented = true });
            await _blobStore.UploadTextAsync(blobName, json);
            
            keyInfo.Key = originalKey;
            _keyCache[originalKey] = keyInfo;
            
            _logger.LogInformation("Saved new encrypted API key to blob: {Blob}", blobName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error encrypting and saving API key");
            throw;
        }
    }
}

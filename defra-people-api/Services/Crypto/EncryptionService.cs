using System.Security.Cryptography;
using System.Text;

namespace Defra_People_API.Services;

public class EncryptionService : IEncryptionService
{
    private readonly ILogger<EncryptionService> _logger;
    private readonly byte[] _masterKey;
    private readonly byte[] _masterIv;

    public EncryptionService(ILogger<EncryptionService> logger, IMasterKeyProvider masterKeyProvider)
    {
        _logger = logger;
        
        // Get master key from the provider
        string configKey = masterKeyProvider.GetMasterKey();
        
        // Derive a consistent key and IV from the configuration string
        using var deriveBytes = new Rfc2898DeriveBytes(
            configKey,
            Encoding.UTF8.GetBytes("DefraApiKeyMasterSalt"),
            10000,
            HashAlgorithmName.SHA256);
            
        _masterKey = deriveBytes.GetBytes(32); // 256 bits
        _masterIv = deriveBytes.GetBytes(16);  // 128 bits
    }

    public (string encryptedText, string encryptionKey) Encrypt(string plainText)
    {
        try
        {
            // Generate a new AES key for this encryption
            using var aes = Aes.Create();
            aes.GenerateKey();
            aes.GenerateIV();
            
            // Encrypt the plain text with AES
            byte[] dataToEncrypt = Encoding.UTF8.GetBytes(plainText);
            byte[] encryptedData;
            
            using (var encryptor = aes.CreateEncryptor())
            using (var ms = new MemoryStream())
            {
                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                {
                    cs.Write(dataToEncrypt, 0, dataToEncrypt.Length);
                    cs.FlushFinalBlock();
                }
                encryptedData = ms.ToArray();
            }
            
            // Combine the IV and encrypted data
            byte[] combinedData = new byte[aes.IV.Length + encryptedData.Length];
            Buffer.BlockCopy(aes.IV, 0, combinedData, 0, aes.IV.Length);
            Buffer.BlockCopy(encryptedData, 0, combinedData, aes.IV.Length, encryptedData.Length);
            
            string encryptedText = Convert.ToBase64String(combinedData);
            
            // Convert the AES key to a string
            string keyString = Convert.ToBase64String(aes.Key);
            
            // Encrypt the key with the master key
            string encryptedKeyString = EncryptWithMasterKey(keyString);
            
            return (encryptedText, encryptedKeyString);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error encrypting data");
            throw new InvalidOperationException("Failed to encrypt data", ex);
        }
    }

    public string Decrypt(string encryptedText, string encryptedEncryptionKey)
    {
        try
        {
            // Decrypt the encryption key using the master key
            string decryptedKeyString = DecryptWithMasterKey(encryptedEncryptionKey);
            
            // Get the AES key from the decrypted encryption key
            byte[] aesKey = Convert.FromBase64String(decryptedKeyString);
            
            byte[] combinedData = Convert.FromBase64String(encryptedText);
            
            using var aes = Aes.Create();
            byte[] iv = new byte[aes.IV.Length];
            byte[] encryptedData = new byte[combinedData.Length - iv.Length];
            
            Buffer.BlockCopy(combinedData, 0, iv, 0, iv.Length);
            Buffer.BlockCopy(combinedData, iv.Length, encryptedData, 0, encryptedData.Length);
            
            aes.Key = aesKey;
            aes.IV = iv;
            
            // Decrypt the data
            using var decryptor = aes.CreateDecryptor();
            using var ms = new MemoryStream(encryptedData);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var reader = new StreamReader(cs, Encoding.UTF8);
            
            return reader.ReadToEnd();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error decrypting data");
            throw new InvalidOperationException("Failed to decrypt data", ex);
        }
    }
    
    private string EncryptWithMasterKey(string plainText)
    {
        try
        {
            byte[] dataToEncrypt = Encoding.UTF8.GetBytes(plainText);
            
            using var aes = Aes.Create();
            aes.Key = _masterKey;
            aes.IV = _masterIv;
            
            using var encryptor = aes.CreateEncryptor();
            using var ms = new MemoryStream();
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            {
                cs.Write(dataToEncrypt, 0, dataToEncrypt.Length);
                cs.FlushFinalBlock();
            }
            
            return Convert.ToBase64String(ms.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error encrypting with master key");
            throw new InvalidOperationException("Failed to encrypt with master key", ex);
        }
    }
    
    private string DecryptWithMasterKey(string encryptedText)
    {
        try
        {
            byte[] dataToDecrypt = Convert.FromBase64String(encryptedText);
            
            using var aes = Aes.Create();
            aes.Key = _masterKey;
            aes.IV = _masterIv;
            
            using var decryptor = aes.CreateDecryptor();
            using var ms = new MemoryStream(dataToDecrypt);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var reader = new StreamReader(cs, Encoding.UTF8);
            
            return reader.ReadToEnd();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error decrypting with master key");
            throw new InvalidOperationException("Failed to decrypt with master key", ex);
        }
    }
}

namespace Defra_People_API.Services;
public interface IEncryptionService
{
    (string encryptedText, string encryptionKey) Encrypt(string plainText);
    string Decrypt(string encryptedText, string encryptionKey);
}

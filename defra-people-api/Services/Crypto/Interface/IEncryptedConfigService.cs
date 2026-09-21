namespace Defra_People_API.Services;

public interface IEncryptedConfigService
{
    void EnsureAdminApiKeyEncrypted();
    void EnsureMasterKeyEncrypted();
}

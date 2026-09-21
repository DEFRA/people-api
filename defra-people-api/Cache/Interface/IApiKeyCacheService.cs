using Defra_People_API.Entities;

namespace Defra_People_API.Cache;

public interface IApiKeyCacheService
{
    void LoadKeysFromDirectory();
    void RefreshCache();
    bool IsValidKey(string key);
    Task<IEnumerable<AccessKey>> GetAllKeys();
    Task SaveKeyToFile(AccessKey keyInfo);
}

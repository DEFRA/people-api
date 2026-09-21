using Microsoft.Graph.Models;

namespace Defra_People_API.Cache;

public interface IUserCache
{
    Task AddUserToCache(IEnumerable<User> users);
    Task<IEnumerable<User>?> GetUserFromCache(string userId);
    Task<IEnumerable<User>?> DumpCache();
    Task PurgeCache();
}

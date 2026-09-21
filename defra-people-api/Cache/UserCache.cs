using System.Text.Json;
using Defra_People_API.Entities;
using Microsoft.Graph.Models;

namespace Defra_People_API.Cache;

public class UserCache : IUserCache
{
    private readonly Dictionary<string, User> _userCache = new();
    private readonly ILogger<UserCache> _logger;

    public UserCache(ILogger<UserCache> logger)
    {
        _logger = logger;
    }

    public async Task AddUserToCache(IEnumerable<User> users)
    {
        foreach(var user in users)
        {
            try
            {
                string cacheKey = $"{user.EmployeeId}-{user.OnPremisesSamAccountName}-{user.MailNickname}";
                
                await Task.Run(() => _userCache.Add(cacheKey, user));
                
                _logger.LogDebug("User added to cache with key: {CacheKey}", cacheKey);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Error adding user to cache: {User}", JsonSerializer.Serialize(user));
            }
        }
    }

    public async Task<IEnumerable<User>?> GetUserFromCache(string userId)
    {
        return await Task.Run(() => 
        {
            // Look for keys that contains the userId
            var cacheKey = _userCache.Keys.Where(key => 
                key.Split('-').Any(part => part == userId));
            
            if (cacheKey != null)
            {
                _logger.LogDebug("User found in cache with key: {CacheKey}", cacheKey);
                return _userCache.Where(x => cacheKey.Any(y => y == x.Key)).Select(x => x.Value);
            }
            
            _logger.LogDebug("User not found in cache for userId: {UserId}", userId);
            return null;
        });
    }

    public async Task<IEnumerable<User>?> DumpCache()
    {
        return await Task.Run(() => 
        {
            _logger.LogDebug("Dumping user cache");
            return _userCache.Values.ToList();
        });
    }

    public Task PurgeCache()
    {
        _userCache.Clear();
        _logger.LogInformation("User cache purged");
        return Task.CompletedTask;
    }
}

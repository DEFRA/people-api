using Microsoft.Graph.Models;

namespace Defra_People_API.Services.Graph;

public partial class GraphService : IGraphService
{
    public async Task<IEnumerable<User>> GetUserBySID(string id, bool getManager)
    {
        var cachedUsers = await _userCache.GetUserFromCache(id);
        if (cachedUsers != null && cachedUsers.Any())
            return cachedUsers;
        
        var graphClient = _graphClientFactory.CreateClient();

        var users = await graphClient.Users
            .GetAsync(requestConfig =>
            {
                ConfigureRequest(requestConfig, getManager);
                requestConfig.QueryParameters.Filter = $"employeeId eq '{id}'";
            });
        
        if (users == null || users.Value == null)
            return [];
            
        if(getManager)
        {
            foreach (var user in users.Value)
            {
                await EnrichManagerData(graphClient, user);
            }
        }
        
        await _userCache.AddUserToCache(users.Value);

        return users.Value;
    }
}

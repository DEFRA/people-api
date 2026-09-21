using Defra_People_API.Cache;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Users;
using Microsoft.Kiota.Abstractions;

namespace Defra_People_API.Services.Graph;

public partial class GraphService : IGraphService
{
    private readonly IGraphClientFactory _graphClientFactory;
    private readonly IUserCache _userCache;

    public GraphService(IGraphClientFactory graphClientFactory, IUserCache userCache)
    {
        _graphClientFactory = graphClientFactory;
        _userCache = userCache;
    }

    private void ConfigureRequest(RequestConfiguration<UsersRequestBuilder.UsersRequestBuilderGetQueryParameters> requestConfig, bool getManager)
    {
        requestConfig.Headers.Add("ConsistencyLevel", "eventual");
        requestConfig.QueryParameters.Count = true;
        
        requestConfig.QueryParameters.Select = new[] {
            "userPrincipalName",
            "displayName",
            "givenName",
            "surname",
            "employeeType",
            "mail",
            "officeLocation",
            "employeeId",
            "onPremisesSamAccountName",
            "mailNickname",
            "manager"
        };

        if(getManager)
        {
            requestConfig.QueryParameters.Expand = new[] {
                "manager($levels=1)"
            };
        }
    }

    private async Task EnrichManagerData(Microsoft.Graph.GraphServiceClient graphClient, User user)
    {
        if (user.Manager == null)
            return;
            
        string managerId = user.Manager.Id ?? string.Empty;
        
        if (string.IsNullOrEmpty(managerId))
            return;
            
        try
        {
            var managerDetails = await FetchManagerDetails(graphClient, managerId);
            
            if (managerDetails == null)
                return;
                
            AddManagerPropertiesToAdditionalData(user.Manager, managerDetails);
        }
        catch
        {
            // TODO: continue?
        }
    }
    
    private async Task<User?> FetchManagerDetails(Microsoft.Graph.GraphServiceClient graphClient, string managerId)
    {
        if (string.IsNullOrEmpty(managerId))
            return null;

        try
        {
            return await graphClient.Users[managerId]
                .GetAsync(requestConfig =>
                {
                    requestConfig.QueryParameters.Select = new[] {
                    "displayName",
                    "givenName",
                    "surname",
                    "userPrincipalName",
                    "mail",
                    "id"
                };
            });
        }
        catch (ServiceException)
        {
            return null;
        }
    }
    
    private void AddManagerPropertiesToAdditionalData(DirectoryObject manager, User managerDetails)
    {
        var managerData = new Dictionary<string, object>();
        
        if (managerDetails.DisplayName != null)
            managerData["displayName"] = managerDetails.DisplayName;
            
        if (managerDetails.GivenName != null)
            managerData["givenName"] = managerDetails.GivenName;
            
        if (managerDetails.Surname != null)
            managerData["surname"] = managerDetails.Surname;
            
        if (managerDetails.UserPrincipalName != null)
            managerData["userPrincipalName"] = managerDetails.UserPrincipalName;
            
        if (managerDetails.Mail != null)
            managerData["mail"] = managerDetails.Mail;
        
        if (manager.AdditionalData == null)
            manager.AdditionalData = new Dictionary<string, object>();
        
        foreach (var kvp in managerData)
        {
            manager.AdditionalData[kvp.Key] = kvp.Value;
        }
    }
}

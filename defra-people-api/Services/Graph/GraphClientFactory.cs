using Azure.Identity;
using Defra_People_API.Entities;
using Microsoft.Graph;

namespace Defra_People_API.Services;

public class GraphClientFactory : IGraphClientFactory
{
    private readonly IConfiguration _configuration;

    public GraphClientFactory(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public GraphServiceClient CreateClient()
    {
        var graphConfig = _configuration.GetSection("GraphApi").Get<GraphApiConfig>();
        var clientId = graphConfig.ClientId;
        var clientSecret = graphConfig.ClientSecret;
        var tenantId = graphConfig.TenantId;
        var scopes = graphConfig.Scopes ?? new string[] { "User.Read.All" };

        // No client secret configured: use a managed identity (or other environment-ambient
        // credential) instead of a stored secret - the recommended approach for Azure-hosted compute.
        // GraphApi:ManagedIdentityClientId is only needed for a user-assigned identity; leave unset
        // to use the compute's system-assigned identity.
        if (string.IsNullOrWhiteSpace(clientSecret))
        {
            var managedIdentityClientId = _configuration["GraphApi:ManagedIdentityClientId"];
            var managedIdentityOptions = new DefaultAzureCredentialOptions
            {
                AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
                ManagedIdentityClientId = string.IsNullOrWhiteSpace(managedIdentityClientId) ? null : managedIdentityClientId
            };

            return new GraphServiceClient(new DefaultAzureCredential(managedIdentityOptions), scopes);
        }

        var options = new ClientSecretCredentialOptions
        {
            AuthorityHost = AzureAuthorityHosts.AzurePublicCloud
        };

        var clientSecretCredential = new ClientSecretCredential(tenantId, clientId, clientSecret, options);
        return new GraphServiceClient(clientSecretCredential, scopes);
    }
}

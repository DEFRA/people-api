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

        var options = new ClientSecretCredentialOptions
        {
            AuthorityHost = AzureAuthorityHosts.AzurePublicCloud
        };

        var clientSecretCredential = new ClientSecretCredential(tenantId, clientId, clientSecret, options);
        return new GraphServiceClient(clientSecretCredential, scopes);
    }
}

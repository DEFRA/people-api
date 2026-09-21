using Microsoft.Graph;

namespace Defra_People_API.Services;

public interface IGraphClientFactory
{
    GraphServiceClient CreateClient();
}

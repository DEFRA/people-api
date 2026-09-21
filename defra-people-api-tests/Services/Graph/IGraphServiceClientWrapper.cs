using Microsoft.Graph;

namespace defra.people_api_tests.Services.Graph;

/// <summary>
/// Wrapper interface for GraphServiceClient to make it easier to mock in tests
/// </summary>
public interface IGraphServiceClientWrapper
{
    GraphServiceClient Client { get; }
}

/// <summary>
/// Implementation of IGraphServiceClientWrapper that wraps a real GraphServiceClient
/// </summary>
public class GraphServiceClientWrapper : IGraphServiceClientWrapper
{
    public GraphServiceClient Client { get; }

    public GraphServiceClientWrapper(GraphServiceClient client)
    {
        Client = client;
    }
}

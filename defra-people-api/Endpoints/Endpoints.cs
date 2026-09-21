using Defra_People_API.Endpoints.GET;
using Defra_People_API.Endpoints.POST;

namespace Defra_People_API.Endpoints;

public static partial class Endpoints
{
    public static WebApplication RegisterEndpoints(this WebApplication app)
    {
        // GET endpoints
        app = RootEndpoint.RegisterEndpoint(app);
        app = AdminListApiKeysEndpoint.RegisterEndpoint(app);
        app = AdminDumpCacheEndpoint.RegisterEndpoint(app);
        app = GetUserBySidEndpoint.RegisterEndpoint(app);
        app = GetUserByMailNicknameEndpoint.RegisterEndpoint(app);
        app = GetUserBySAMAccountEndpoint.RegisterEndpoint(app);

        // POST endpoints
        app = GenerateApiKeyEndpoint.RegisterEndpoint(app);
        app = RefreshApiKeysEndpoint.RegisterEndpoint(app);
        app = AdminPurgeCacheEndpoint.RegisterEndpoint(app);
        
        // Health check endpoints (already defined in Healthcheck.cs)
        app = RegisterHealthCheck(app);

        return app;
    }
}

using Defra_People_API.Services;
using Microsoft.AspNetCore.Authorization;
using Defra_People_API.Endpoints;
using Defra_People_API.Repository;
using Defra_People_API.Services.Logger;
using Microsoft.EntityFrameworkCore;
using Defra_People_API.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Defra_People_API.Services.Graph;
using Defra_People_API.Cache;

var builder = WebApplication.CreateBuilder();

if(builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = ApiKeyAuthenticationOptions.DefaultScheme;
    options.DefaultChallengeScheme = ApiKeyAuthenticationOptions.DefaultScheme;
})
.AddApiKeyAuthentication();

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
        
    options.AddPolicy("Admin", policy =>
        policy.RequireAssertion(context =>
        {
            // has the admin API key?
            var httpContext = context.Resource as HttpContext;
            if (httpContext == null) return false;
            
            if (!httpContext.Request.Headers.TryGetValue("X-API-Key", out var apiKeyHeaderValues))
                return false;
                
            var providedApiKey = apiKeyHeaderValues.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(providedApiKey))
                return false;
            
            var authConfigProvider = httpContext.RequestServices.GetRequiredService<IAuthConfigProvider>();
            var authConfig = authConfigProvider.GetAuthConfig();
            var adminApiKey = authConfig.AdminApiKey;
            
            return providedApiKey == adminApiKey;
        }));
});

builder.Services.AddDataProtection();

// Careful with reordering these some depend on each other
builder.Services.AddSingleton<IMasterKeyProvider, MasterKeyProvider>();
builder.Services.AddSingleton<IEncryptionService, EncryptionService>();
builder.Services.AddSingleton<IEncryptedConfigService, EncryptedConfigService>();
builder.Services.AddSingleton<IApiKeyCacheService, ApiKeyCacheService>();
builder.Services.AddSingleton<IUserCache, UserCache>();
builder.Services.AddSingleton<IAuthConfigProvider, AuthConfigProvider>();
builder.Services.AddSingleton<IGraphClientFactory, GraphClientFactory>();
builder.Services.AddScoped<IGraphService, GraphService>();

builder.Services.AddDbContext<LogDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("LogDb")));

// Load the log queue as a background service
builder.Services.AddSingleton<ILogQueue, LogQueue>();
builder.Services.AddHostedService<LogProcessingService>();

builder.Services.AddScoped<ApiLoggerFilter>();

builder.Services.AddControllers(options =>
{
    options.Filters.Add<ApiLoggerFilter>();
});

builder.Services.AddHealthChecks()
    .AddCheck<GraphApiHealthCheck>("graph_api", tags: new[] { "external_dependency" })
    .AddCheck<DatabaseHealthCheck>("database", tags: new[] { "data_store" })
    .AddCheck("api_self", () => HealthCheckResult.Healthy("API is running"), tags: new[] { "self" });

var app = builder.Build();

var apiKeyCacheService = app.Services.GetRequiredService<IApiKeyCacheService>();
apiKeyCacheService.LoadKeysFromDirectory();

// Protect the MasterKey and AdminApiKey
var encryptedConfigService = app.Services.GetRequiredService<IEncryptedConfigService>();
encryptedConfigService.EnsureMasterKeyEncrypted();
encryptedConfigService.EnsureAdminApiKeyEncrypted();

var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
var logger = loggerFactory.CreateLogger("ApiLogger");


app.UseRequestBodyCapture();

app.RegisterEndpoints();

app.UseHsts();
// app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.Run();

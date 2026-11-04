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
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder();

if(builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}

// Optional: load configuration/secrets from Azure Key Vault when configured (e.g. in Azure-hosted
// environments). Uses DefaultAzureCredential so it works with a managed identity in Azure and
// developer credentials (Azure CLI/VS) locally - no secrets required in config.
var keyVaultUri = builder.Configuration["KeyVault:Uri"];
if (!string.IsNullOrWhiteSpace(keyVaultUri))
{
    builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential());
}

// Enabled via APPLICATIONINSIGHTS_CONNECTION_STRING (or ApplicationInsights:ConnectionString) env var;
// no-ops safely when no connection string is configured.
builder.Services.AddApplicationInsightsTelemetry();

// Entra ID (Azure AD) bearer-token authentication is additive and opt-in: it only activates when
// AzureAd:TenantId/Audience are configured, so existing X-API-Key consumers keep working unchanged.
var azureAdTenantId = builder.Configuration["AzureAd:TenantId"];
var azureAdAudience = builder.Configuration["AzureAd:Audience"] ?? builder.Configuration["AzureAd:ClientId"];
var entraIdEnabled = !string.IsNullOrWhiteSpace(azureAdTenantId) && !string.IsNullOrWhiteSpace(azureAdAudience);

var authenticationBuilder = builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = entraIdEnabled ? "ApiKeyOrEntraId" : ApiKeyAuthenticationOptions.DefaultScheme;
    options.DefaultChallengeScheme = entraIdEnabled ? "ApiKeyOrEntraId" : ApiKeyAuthenticationOptions.DefaultScheme;
})
.AddApiKeyAuthentication();

if (entraIdEnabled)
{
    authenticationBuilder
        .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.Authority = $"https://login.microsoftonline.com/{azureAdTenantId}/v2.0";
            options.Audience = azureAdAudience;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true
            };
        })
        .AddPolicyScheme("ApiKeyOrEntraId", "API Key or Entra ID bearer token", options =>
        {
            options.ForwardDefaultSelector = context =>
            {
                var authHeader = context.Request.Headers.Authorization.ToString();
                return authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                    ? JwtBearerDefaults.AuthenticationScheme
                    : ApiKeyAuthenticationOptions.DefaultScheme;
            };
        });
}

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

// Persists the Data Protection key ring to Blob Storage + Key Vault when DataProtection:Enabled is
// true, so AdminApiKey/MasterKey protection survives restarts and is shared across replicas.
builder.Services.AddPeopleApiDataProtection(builder.Configuration);

// Issued API keys are persisted to Blob Storage, not local disk, so they survive restarts and
// are shared across replicas/redeploys (container filesystems are ephemeral and per-instance).
var apiKeyContainerUri = builder.Configuration["ApiKeyStorage:ContainerUri"]
    ?? throw new InvalidOperationException("ApiKeyStorage:ContainerUri is not configured");
var apiKeyManagedIdentityClientId = builder.Configuration["ApiKeyStorage:ManagedIdentityClientId"];
builder.Services.AddSingleton(new BlobContainerClient(new Uri(apiKeyContainerUri), new DefaultAzureCredential(new DefaultAzureCredentialOptions
{
    ManagedIdentityClientId = string.IsNullOrWhiteSpace(apiKeyManagedIdentityClientId) ? null : apiKeyManagedIdentityClientId
})));
builder.Services.AddSingleton<IApiKeyBlobStore, ApiKeyBlobStore>();

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

// CORS is secure-by-default (no cross-origin access) unless Cors:AllowedOrigins is configured.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("ConfiguredOrigins", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
        }
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Defra People API", Version = "v1" });

    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Name = "X-API-Key",
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "API key issued by the People API admin endpoints"
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Microsoft Entra ID bearer token (when enabled)"
    });
});

var app = builder.Build();

// Respect X-Forwarded-* headers from the reverse proxy/ingress (Azure Container Apps, App Service,
// AKS ingress, etc.) so scheme/remote-IP are correct for logging, HSTS and auth redirects.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

// Global exception handler: never leak stack traces/exception details outside Development.
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            Detail = app.Environment.IsDevelopment() ? exceptionFeature?.Error.ToString() : null
        };

        await context.Response.WriteAsJsonAsync(problem);
    });
});

// Swagger defaults to Development only; set Swagger:Enabled=true to expose it in other environments.
if (app.Configuration.GetValue("Swagger:Enabled", app.Environment.IsDevelopment()))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var apiKeyCacheService = app.Services.GetRequiredService<IApiKeyCacheService>();
apiKeyCacheService.LoadKeysFromDirectory();

// Protect the MasterKey and AdminApiKey
var encryptedConfigService = app.Services.GetRequiredService<IEncryptedConfigService>();
encryptedConfigService.EnsureMasterKeyEncrypted();
encryptedConfigService.EnsureAdminApiKeyEncrypted();

var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
var logger = loggerFactory.CreateLogger("ApiLogger");


app.UseRequestBodyCapture();

app.UseCors("ConfiguredOrigins");

app.RegisterEndpoints();

app.UseHsts();
// Disabled by default: TLS is expected to terminate at the container/ingress layer (Azure Container
// Apps, App Service, or an AKS ingress controller). Enable only if this service terminates TLS itself.
// app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.Run();

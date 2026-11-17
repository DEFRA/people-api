using Defra_People_API.Services;
using Defra_People_API.Endpoints;
using Defra_People_API.Repository;
using Defra_People_API.Services.Logger;
using Microsoft.EntityFrameworkCore;
using Defra_People_API.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Defra_People_API.Services.Graph;
using Defra_People_API.Cache;
using Azure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web;
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

// Every request must present a Microsoft Entra ID bearer token scoped to this API. Microsoft.Identity.Web
// validates the token signature against the tenant's signing keys, the issuer, the audience (this app's
// registration) and the lifetime - a token merely being valid for the tenant is not enough on its own.
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

// App roles are declared on the People API app registration (People.Read, People.Admin) and granted to
// each consumer's own Entra ID identity (managed identity, workload identity federation, or certificate).
// "PeopleRead" is satisfied by either role since People.Admin is a superset of read access.
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build())
    .AddPolicy("PeopleRead", policy => policy.RequireRole("People.Read", "People.Admin"))
    .AddPolicy("PeopleAdmin", policy => policy.RequireRole("People.Admin"));

builder.Services.AddSingleton<IUserCache, UserCache>();
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

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Microsoft Entra ID bearer token, carrying the People.Read or People.Admin app role"
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

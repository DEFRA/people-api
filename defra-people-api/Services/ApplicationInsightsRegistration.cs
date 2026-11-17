using Azure.Identity;
using Microsoft.ApplicationInsights.Extensibility;

namespace Defra_People_API.Services;

// Mirrors Services/ApplicationInsightsRegistration.cs in rpa-cph-non-subsidy / rpa-inspections-workbench /
// rpa-mts-inspections / rpa-quality-checks: registers the SDK only when a connection string is actually
// configured (the bare AddApplicationInsightsTelemetry() call throws during host start-up on this SDK
// version when no connection string is present), and optionally authenticates ingestion with a
// user-assigned managed identity instead of connection-string-only (local) authentication.
public static class ApplicationInsightsRegistration
{
    public static IServiceCollection AddPeopleApiApplicationInsights(this IServiceCollection services, IConfiguration configuration)
    {
        // Prefer the Azure environment-variable key; fall back only when it is absent, not blank.
        var connection = configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"] ??
                         configuration["ApplicationInsights:ConnectionString"];
        var identity = configuration["Telemetry:ManagedIdentityClientId"];

        // An explicitly empty identity setting is a configuration error, not an opt-out.
        // Check the section too because configuration providers can expose a key with a null value.
        var hasIdentity = identity != null || configuration.GetSection("Telemetry").GetChildren()
            .Any(child => child.Key.Equals("ManagedIdentityClientId", StringComparison.OrdinalIgnoreCase));

        var clientId = Guid.Empty;

        if (hasIdentity)
        {
            if (!Guid.TryParse(identity, out clientId) || clientId == Guid.Empty)
                throw new InvalidOperationException("Telemetry:ManagedIdentityClientId must identify the user-assigned managed identity.");

            if (string.IsNullOrWhiteSpace(connection))
                throw new InvalidOperationException("Telemetry:ManagedIdentityClientId requires an Application Insights connection string.");
        }

        // No connection string means no SDK registration. An identity without a connection
        // string has already failed above so a deployment mistake cannot silently disable telemetry.
        if (string.IsNullOrWhiteSpace(connection)) return services;

        services.AddApplicationInsightsTelemetry(options => options.ConnectionString = connection);

        // The connection string selects the telemetry resource; this identity authenticates ingestion.
        // In ACA, attach this identity and grant it Monitoring Metrics Publisher on that resource.
        // Without it, ingestion falls back to connection-string authentication (requires local auth enabled).
        if (hasIdentity)
            services.Configure<TelemetryConfiguration>(options => options.SetAzureTokenCredential(
                new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(clientId.ToString()))));

        return services;
    }
}

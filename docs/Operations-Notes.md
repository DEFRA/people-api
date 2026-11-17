# Operations Notes - Defra People API

How to configure, run, containerise and operate the service. CI/CD pipeline wiring (deploy tasks) and infrastructure provisioning (`people-api/infra`) are summarised here; see [infra/README.md](../infra/README.md) for the full Bicep handoff.

## Configuration

All settings can be supplied as environment variables, using `__` instead of `:` (for example `GraphApi__ClientSecret`). Environment variables override `appsettings.json`. User Secrets / `appsettings.Development.json` are for local development only.

| Key | Purpose | Default |
|---|---|---|
| `GraphApi:ClientId`, `TenantId`, `Scopes` | Entra ID app registration used for the Microsoft Graph client-credentials flow | empty |
| `GraphApi:ClientSecret` | App registration secret. **Leave empty to use a managed identity instead** (recommended - see below) | empty |
| `GraphApi:ManagedIdentityClientId` | Only needed for a **user-assigned** managed identity; leave unset for system-assigned | empty |
| `AzureAd:Instance`, `TenantId`, `ClientId` | The People API's **own** Entra ID app registration that Microsoft.Identity.Web validates every incoming bearer token against (signature, issuer, audience, lifetime) | `Instance` defaults to `https://login.microsoftonline.com/`; `TenantId`/`ClientId` required |
| `Authorization:PeopleReadRole`, `PeopleAdminRole` | App-role token values enforced by the `PeopleRead`/`PeopleAdmin` authorization policies; must match the values defined on the app registration (`infra/entra/New-PeopleApiEntraRegistration.ps1`) and be distinct | `People.Read` / `People.Admin` |
| `KeyVault:Uri` | When set (absolute `https` URL), Key Vault secrets are merged into configuration via `DefaultAzureCredential`. The only secret people-api currently stores there is the optional Graph client secret below | empty (not used) |
| `ApplicationInsights:ConnectionString` or `APPLICATIONINSIGHTS_CONNECTION_STRING` | Enables Application Insights telemetry. Registration is a no-op (not a startup failure) when absent - see `Services/ApplicationInsightsRegistration.cs` | empty (telemetry off) |
| `Telemetry:ManagedIdentityClientId` | Optional: authenticates Application Insights ingestion with this user-assigned managed identity instead of connection-string-only (local) authentication. Requires an Application Insights connection string to also be set | empty |
| `Cors:AllowedOrigins` | Array of allowed browser origins | empty (CORS denied by default) |
| `Swagger:Enabled` | Expose `/swagger` outside Development | `false` outside Development |

### Managed identity for Microsoft Graph

`GraphClientFactory` uses `ClientSecretCredential` when `GraphApi:ClientSecret` is set. When the secret is left empty (the Azure-recommended path), it falls back to `DefaultAzureCredential`, which uses the compute's managed identity in Azure and developer sign-in locally. To use this:

1. Enable a managed identity on the hosting compute (a user-assigned identity is provisioned by `people-api/infra/main.bicep`; set `GraphApi:ManagedIdentityClientId` to its client ID).
2. Grant it the `User.Read.All` application permission on Microsoft Graph (with admin consent).
3. Remove/leave empty `GraphApi:ClientSecret` in that environment's configuration - this is the default in `infra/app.bicep`.

### Key Vault

The service authenticates to Key Vault with `DefaultAzureCredential` (managed identity in Azure; developer sign-in locally). Secret names use `--` for `:`, for example `GraphApi--ClientSecret`. The identity needs `Key Vault Secrets User` (or equivalent) on the vault. people-api has no cookies, antiforgery tokens or session state, so unlike the other apps in this portfolio it needs no Data Protection key ring and no Blob Storage account - Key Vault is used solely for the optional Graph client secret above.

### Entra ID authentication and app roles

Every request must present a Microsoft Entra ID bearer token (`Authorization: Bearer <token>`) - there is no anonymous or API-key fallback (see [docs/Consumer-Handoff.md](./Consumer-Handoff.md) for the full migration from the old `X-API-Key` scheme). `Microsoft.Identity.Web` validates the token's signature, issuer, audience and lifetime against `AzureAd:TenantId`/`ClientId`. The token must also carry the `People.Read` or `People.Admin` app role (configurable via `Authorization:PeopleReadRole`/`PeopleAdminRole`): `People.Admin` is a superset of `People.Read`. Onboard each consumer with its own Entra ID identity (prefer managed identity for Azure-hosted consumers, workload identity federation where supported, otherwise a certificate) and grant it the role it needs with `infra/entra/Grant-PeopleApiRole.ps1` - see that script and `infra/entra/New-PeopleApiEntraRegistration.ps1` for the one-time app-registration setup. A token already issued may remain usable until it expires after a role is removed; factor token lifetime into revocation planning.

### API call audit logging

Each API call is captured by `ApiLoggerFilter` (consumer, method, endpoint, user, params, response, log level, runtime) and queued via `ILogQueue`. `LogProcessingService`, a background hosted service, drains that queue and writes each entry through the standard logging pipeline (`ILogger`), so it flows to the console and to Application Insights when configured - there is no SQL database or other persistent log store.

## Health endpoints

| Path | Meaning |
|---|---|
| `/health/live` | The process is running (self-check only) |
| `/health/ready` | Microsoft Graph is reachable |
| `/health` | Combined view of all registered checks |

These back container/orchestrator liveness and readiness probes (Azure Container Apps, App Service, AKS). No authentication is required for health endpoints.

## API documentation

`/swagger` is enabled by default in Development; set `Swagger:Enabled=true` to expose it elsewhere (for example a non-production environment only - avoid enabling it in Production).

## Containerisation

A multi-stage `Dockerfile` is provided at the repository root (`people-api/Dockerfile`), producing a non-root, chiseled ASP.NET Core 10 image. Base images are pinned to exact tags (`sdk:10.0.403-noble`, `aspnet:10.0.14-noble-chiseled-extra`), matching the pinning discipline used across the portfolio. It listens on port `8080` (`ASPNETCORE_HTTP_PORTS`). No secrets are baked into the image; all secrets/connection strings must be supplied via environment variables, Key Vault, or an orchestrator secret store at deploy time.

Build and run locally:

```bash
docker build -t defra-people-api -f people-api/Dockerfile people-api
docker run -p 8080:8080 \
  -e GraphApi__ClientId=<id> -e GraphApi__TenantId=<tenant> \
  -e AzureAd__TenantId=<tenant> -e AzureAd__ClientId=<client-id> \
  defra-people-api
```

## Known limitations / follow-ups

1. **CI/CD deploy stages are placeholders.** `azure-pipelines.yaml` builds, tests and packages the app but its `DeployToNonProd`/`DeployToProd` stages only `echo` - no live deployment task is wired up, unlike `rpa-mts-inspections`'s pipeline (real ACR push + Trivy scan + `az deployment group create`). Port that pattern here as part of the Azure handoff.
2. **Infra has not been deployed or validated against a real subscription.** `people-api/infra` was authored/updated against the same pattern as the other four apps in this portfolio but, like them, has not been run through `az deployment group what-if`/`create` or Microsoft Entra ID for real - see `infra/README.md` for the full manual-checkpoint list.


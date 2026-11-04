# Operations Notes - Defra People API

How to configure, run, containerise and operate the service. CI/CD pipeline wiring (deploy tasks) and infrastructure provisioning are not covered here.

## Configuration

All settings can be supplied as environment variables, using `__` instead of `:` (for example `GraphApi__ClientSecret`). Environment variables override `appsettings.json`. User Secrets / `appsettings.Development.json` are for local development only.

| Key | Purpose | Default |
|---|---|---|
| `GraphApi:ClientId`, `TenantId`, `Scopes` | Entra ID app registration used for the Microsoft Graph client-credentials flow | empty |
| `GraphApi:ClientSecret` | App registration secret. **Leave empty to use a managed identity instead** (see below) | empty |
| `GraphApi:ManagedIdentityClientId` | Only needed for a **user-assigned** managed identity; leave unset for system-assigned | empty |
| `ConnectionStrings:LogDb` | SQL Server connection string for the async API request log (`LogDbContext`) | empty |
| `Authentication:AdminApiKey` | Bootstrap admin API key (encrypted at rest after first startup, see below) | empty |
| `Encryption:MasterKey` | Master key used to encrypt/decrypt issued API keys. **Never rotate without a plan** - see README | empty |
| `AzureAd:TenantId`, `AzureAd:Audience` (or `ClientId`) | Enables Entra ID bearer-token authentication **in addition to** `X-API-Key` | empty (Entra ID auth disabled; API key only) |
| `KeyVault:Uri` | When set (absolute `https` URL), Key Vault secrets are merged into configuration via `DefaultAzureCredential` | empty (not used) |
| `ApplicationInsights:ConnectionString` or `APPLICATIONINSIGHTS_CONNECTION_STRING` | Enables Application Insights telemetry | empty (telemetry off) |
| `Cors:AllowedOrigins` | Array of allowed browser origins | empty (CORS denied by default) |
| `Swagger:Enabled` | Expose `/swagger` outside Development | `false` outside Development |

### Managed identity for Microsoft Graph

`GraphClientFactory` uses `ClientSecretCredential` when `GraphApi:ClientSecret` is set (existing behaviour, unchanged). When the secret is left empty, it falls back to `DefaultAzureCredential`, which uses the compute's system-assigned managed identity in Azure (App Service/Container Apps/AKS pod identity) and developer sign-in locally. To use this:

1. Enable a managed identity on the hosting compute.
2. Grant it the `User.Read.All` application permission on Microsoft Graph (with admin consent) - the same permission the app registration uses today.
3. Remove/leave empty `GraphApi:ClientSecret` in that environment's configuration.

### Key Vault

The service authenticates to Key Vault with `DefaultAzureCredential` (managed identity in Azure; developer sign-in locally). Secret names use `--` for `:`, for example `GraphApi--ClientSecret`. The identity needs `Key Vault Secrets User` (or equivalent) on the vault.

### Entra ID authentication (additive, non-breaking)

By default the API authenticates every request with the existing `X-API-Key` header scheme - **no change for current consumers** (for example `baqu`). Setting `AzureAd:TenantId` and `AzureAd:Audience` (or `ClientId`) additionally accepts a valid Entra ID bearer token (`Authorization: Bearer <token>`) as an alternative credential on the same endpoints; whichever is presented is validated. There is no requirement to migrate existing API-key consumers.

## Health endpoints

| Path | Meaning |
|---|---|
| `/health/live` | The process is running (self-check only) |
| `/health/ready` | Microsoft Graph and the log database are reachable |
| `/health` | Combined view of all registered checks |

These back container/orchestrator liveness and readiness probes (Azure Container Apps, App Service, AKS). No authentication is required for health endpoints.

## API documentation

`/swagger` is enabled by default in Development; set `Swagger:Enabled=true` to expose it elsewhere (for example a non-production environment only - avoid enabling it in Production).

## Containerisation

A multi-stage `Dockerfile` is provided at the repository root (`people-api/Dockerfile`), producing a non-root, chiseled ASP.NET Core 10 image. It listens on port `8080` (`ASPNETCORE_HTTP_PORTS`). No secrets are baked into the image; all secrets/connection strings must be supplied via environment variables, Key Vault, or an orchestrator secret store at deploy time.

Build and run locally:

```bash
docker build -t defra-people-api -f people-api/Dockerfile people-api
docker run -p 8080:8080 \
  -e GraphApi__ClientId=<id> -e GraphApi__TenantId=<tenant> \
  -e ConnectionStrings__LogDb=<connection-string> \
  -e Authentication__AdminApiKey=<key> -e Encryption__MasterKey=<key> \
  defra-people-api
```

> Docker build was not executed in this environment (Docker Desktop was not running) - validate the image build and a smoke run before relying on it in CI/CD.

## Known limitations / follow-ups (not changed in this pass)

These require real Azure infrastructure to implement and verify safely, so they are documented rather than attempted here:

1. **API key storage does not scale out.** `ApiKeyCacheService.LoadKeysFromDirectory()` reads `.key` files from a local `Keys` folder next to the app. This works for a single instance but is not shared across container replicas or redeploys. Recommend moving to a durable, shared store (database table, Azure Files, or Blob Storage) before running more than one replica.
2. **Data Protection key ring is not persisted.** `EncryptedConfigService` uses `IDataProtectionProvider` to encrypt `Encryption:MasterKey` and `Authentication:AdminApiKey`, and writes the protected value back into `appsettings.json` on disk. Without explicitly persisting the Data Protection key ring (for example to Azure Blob Storage, protected by a Key Vault key), each container instance/redeploy has its own ephemeral key ring. This is self-consistent per-instance today but should be hardened with `PersistKeysToAzureBlobStorage` + `ProtectKeysWithAzureKeyVault` before scaling out or relying on the protected value surviving a restart.
3. **CI/CD deploy stages are placeholders.** `azure-pipelines.yaml` builds, tests and packages the app but its `DeployToNonProd`/`DeployToProd` stages only `echo` - no live deployment task is wired up. Add an `AzureWebApp@1`, container registry push + Container Apps/AKS deploy task, or GitHub Actions equivalent as part of the Azure handoff.
4. **Master key rotation risk** (pre-existing, unchanged): losing or rotating `Encryption:MasterKey` invalidates every previously issued API key. Keep a secure backup and treat it as a Key Vault secret with strict access control.

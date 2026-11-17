# Defra People API

A .NET API service that provides access to user information from Microsoft Graph API. This API allows applications to retrieve user details using various identifiers such as SID, mail nickname, and SAM account name.

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later
- A Microsoft Entra ID app registration for **this API** (validates incoming bearer tokens) - see [infra/entra/New-PeopleApiEntraRegistration.ps1](infra/entra/New-PeopleApiEntraRegistration.ps1)
- A **separate** Microsoft Entra ID app registration (or managed identity) for calling Microsoft Graph

## Local Environment Setup

### 1. Configure Application Settings

The application requires several configuration settings. You can provide these using User Secrets (recommended for development), a secrets.json file, or appsettings files.

#### Option 1: Using User Secrets (Recommended)

The project includes a `secrets.template.json` file that you can use as a template. Copy it to `secrets.json` and fill in your values:

```json
{
    "GraphApi:ClientId": "your-graph-app-client-id",
    "GraphApi:ClientSecret": "your-graph-app-client-secret",
    "GraphApi:TenantId": "your-tenant-id",
    "GraphApi:Scopes": ["User.Read.All"],
    "AzureAd:Instance": "https://login.microsoftonline.com/",
    "AzureAd:TenantId": "your-tenant-id",
    "AzureAd:ClientId": "your-people-api-client-id"
}
```

#### Option 2: Using appsettings.Development.json

Create an `appsettings.Development.json` file in the `defra-people-api` directory:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "GraphApi": {
    "ClientId": "your-graph-app-client-id",
    "ClientSecret": "your-graph-app-client-secret",
    "TenantId": "your-tenant-id",
    "Scopes": ["User.Read.All"]
  },
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "your-tenant-id",
    "ClientId": "your-people-api-client-id"
  }
}
```

### 2. App roles

Requests must carry the `People.Read` or `People.Admin` app role (configurable via `Authorization:PeopleReadRole`/`PeopleAdminRole`, defaulting to those two values). Define them on the app registration with `infra/entra/New-PeopleApiEntraRegistration.ps1`, then grant each consumer's own identity the role it needs with `infra/entra/Grant-PeopleApiRole.ps1` - see [docs/Operations-Notes.md](docs/Operations-Notes.md).

### 3. Build and Run the API

```bash
cd defra-people-api
dotnet build
dotnet run
```

The API will be available at `https://localhost:7068` by default.

## API Endpoints

### GET Endpoints

- `/` (anonymous)
- `/getusers/by-sid/{id}` (requires `People.Read` or `People.Admin`)
- `/getusers/by-mailnickname/{id}` (requires `People.Read` or `People.Admin`)
- `/getusers/by-samaccount/{id}` (requires `People.Read` or `People.Admin`)
- `/admin/dump-cache` (requires `People.Admin`)

### POST Endpoints

- `/admin/purge-cache` (requires `People.Admin`)

### Health Checks

- `/health/live` - liveness probe (process is running)
- `/health/ready` - readiness probe (Microsoft Graph reachable)
- `/health` - combined health check view

### API Documentation

Swagger/OpenAPI UI is available at `/swagger`, enabled by default in the Development environment. Set `Swagger:Enabled=true` to expose it in another environment (avoid enabling it in Production).

## Authentication

Every request must present a Microsoft Entra ID bearer token (`Authorization: Bearer <token>`) validated by Microsoft.Identity.Web against `AzureAd:TenantId`/`ClientId` (signature, issuer, audience and lifetime), and carrying the required app role. There is no anonymous or API-key fallback - see [docs/Consumer-Handoff.md](docs/Consumer-Handoff.md) for the consumer migration guide.

## Microsoft Graph credentials

By default the Graph client uses `GraphApi:ClientId`/`ClientSecret`/`TenantId` (client-credentials flow), unchanged. If `GraphApi:ClientSecret` is left empty, the app instead uses a managed identity (`DefaultAzureCredential`) - recommended when hosting in Azure, since no secret needs to be stored.

## Azure Key Vault and Application Insights

- Set `KeyVault:Uri` to an Azure Key Vault URL to merge Key Vault secrets into configuration (authenticates via `DefaultAzureCredential`). The only secret people-api stores there is an optional Microsoft Graph client secret - this is a stateless bearer-token API with no cookies/session state, so it needs no Data Protection key ring or Blob Storage, unlike the portfolio's user-facing apps.
- Set `ApplicationInsights:ConnectionString` or the `APPLICATIONINSIGHTS_CONNECTION_STRING` environment variable to enable Application Insights telemetry and exception tracking.

## Containerisation

Run from the `people-api` directory (repository root of this project):

```bash
docker build -t defra-people-api -f Dockerfile .
docker run -p 8080:8080 defra-people-api
```

See [docs/Operations-Notes.md](docs/Operations-Notes.md) for the full configuration reference, health checks, and known limitations, and [docs/Consumer-Handoff.md](docs/Consumer-Handoff.md) for the consumer-facing contract summary.


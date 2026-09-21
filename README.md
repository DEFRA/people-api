# Defra People API

A .NET API service that provides access to user information from Microsoft Graph API. This API allows applications to retrieve user details using various identifiers such as SID, mail nickname, and SAM account name.

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- Azure AD application registration with Microsoft Graph API permissions

## Local Environment Setup

### 1. Configure Application Settings

The application requires several configuration settings. You can provide these using User Secrets (recommended for development), a secrets.json file, or appsettings files.

#### Option 1: Using User Secrets (Recommended)

The project includes a `secrets.template.json` file that you can use as a template. Copy it to `secrets.json` and fill in your values:

```json
{
    "GraphApi:ClientId": "your-client-id",
    "GraphApi:ClientSecret": "your-client-secret",
    "GraphApi:TenantId": "your-tenant-id",
    "GraphApi:Scopes": ["User.Read.All"],
    "Authentication:AdminApiKey": "",
    "Encryption:MasterKey": ""
}
```

You'll also need to add the Authentication and Encryption settings to this file.

#### Option 2: Using appsettings.Development.json

Create an `appsettings.Development.json` file in the `defra.people-api` directory:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "GraphApi": {
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret",
    "TenantId": "your-tenant-id",
    "Scopes": ["User.Read.All"]
  },
  "Authentication": {
    "AdminApiKey": "your-admin-api-key"
  },
  "Encryption": {
    "MasterKey": "your-encryption-master-key"
  }
}
```

### 2. Master Encryption Key

The `Encryption:MasterKey` setting is used to encrypt and decrypt API keys. This key is critical for the operation of the API.

**IMPORTANT**: If the master encryption key is lost or changed, all previously generated API keys will become invalid and will need to be regenerated. Make sure to:

- Keep a secure backup of this key
- Use the same key across all environments that need to share API keys
- Never change this key unless you're prepared to regenerate all API keys

### 3. Generate Admin API Key

You can generate a secure admin API key using the provided script:

```bash
./defra.people-api/generate-admin-key.sh
```

Use the generated key in your configuration (User Secrets or appsettings file).

### 4. Build and Run the API

```bash
cd defra.people-api
dotnet build
dotnet run
```

The API will be available at `https://localhost:7068` by default.

## API Endpoints

### GET Endpoints

- `/`
- `/getusers/by-sid`
- `/getusers/by-mailnickname`
- `/getusers/by-samaccount`
- `/admin/list-api-keys`

### POST Endpoints

- `/admin/generate-api-key`
- `/admin/refresh-api-keys`


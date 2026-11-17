<#
.SYNOPSIS
Creates the Microsoft Entra app registration for the Defra People API: single-tenant API (no sign-in
redirect URIs - it is called service-to-service, never interactively), an Application ID URI, and the
two app roles People.Read / People.Admin restricted to application (service) identities. Safe by
default: it only prints what it would do unless -Apply is given.

.DESCRIPTION
Run by the identity team, signed in to Azure CLI (az login) with permission to create app registrations in
the approved tenant. Unlike the portfolio's user-facing apps (Workbench/MTS Inspections/Quality Checks),
People API is consumed by other applications' own identities (managed identity, workload identity
federation, or a certificate) rather than by interactively signed-in people, so its app roles use
allowedMemberTypes = ['Application'] and it defines no redirect URI, no client secret and no
Authorization Code flow of its own.

It does not assign a consumer to a role - use Grant-PeopleApiRole.ps1 (in this folder) for that, as a
separate, access-controlled step per consumer.

.EXAMPLE
./infra/entra/New-PeopleApiEntraRegistration.ps1 -DisplayName 'Defra People API (UAT)'
Shows the plan.

.EXAMPLE
./infra/entra/New-PeopleApiEntraRegistration.ps1 -DisplayName 'Defra People API (UAT)' -Apply
Creates the registration and its service principal.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateLength(1, 120)]
    [string] $DisplayName,

    # Token-safe app-role values; they must match Authorization:PeopleReadRole/PeopleAdminRole in the
    # application settings (Program.cs).
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]{0,119}$')]
    [string] $PeopleReadRole = 'People.Read',
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]{0,119}$')]
    [string] $PeopleAdminRole = 'People.Admin',

    [switch] $Apply
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($PeopleReadRole -ieq $PeopleAdminRole) {
    throw 'PeopleReadRole and PeopleAdminRole must be different values.'
}

$roles = @(
    @{ Value = $PeopleReadRole; Name = 'People API Readers'; Description = 'Look up Defra people/user records by SID, mail nickname or SAM account name.' }
    @{ Value = $PeopleAdminRole; Name = 'People API Administrators'; Description = 'Operate People API maintenance endpoints (cache dump/purge) in addition to read access.' }
)

$appRoles = $roles | ForEach-Object {
    [ordered]@{
        id = [guid]::NewGuid().ToString()
        # Application, not User: every consumer authenticates as its own service identity
        # (managed identity, workload identity federation, or certificate), never a signed-in person.
        allowedMemberTypes = @('Application')
        displayName = $_.Name
        description = $_.Description
        value = $_.Value
        isEnabled = $true
    }
}

Write-Output "Display name        : $DisplayName"
Write-Output 'Sign-in audience    : AzureADMyOrg (single tenant)'
Write-Output 'Redirect URI        : none (service-to-service API, no interactive sign-in)'
Write-Output "App roles           : $($roles.Value -join ', ') (allowedMemberTypes: Application)"
Write-Output 'Client secret       : none created (token validation needs no secret of its own)'
Write-Output 'Implicit grant      : disabled'

if (-not $Apply) {
    Write-Output ''
    Write-Output 'Plan only. Re-run with -Apply to create the registration.'
    return
}

$existing = az ad app list --display-name $DisplayName --query '[].appId' -o tsv
if ($LASTEXITCODE -ne 0) { throw 'az ad app list failed. Sign in with az login first.' }
if ($existing) { throw "An app registration named '$DisplayName' already exists ($existing). Choose another name or update it in the portal." }

$rolesFile = Join-Path ([IO.Path]::GetTempPath()) "people-api-roles-$([guid]::NewGuid().ToString('N')).json"

try {
    $appRoles | ConvertTo-Json -Depth 5 -AsArray | Set-Content -LiteralPath $rolesFile -Encoding utf8
    $clientId = az ad app create `
        --display-name $DisplayName `
        --sign-in-audience 'AzureADMyOrg' `
        --enable-id-token-issuance false `
        --enable-access-token-issuance false `
        --app-roles "@$rolesFile" `
        --query appId -o tsv
    if ($LASTEXITCODE -ne 0 -or -not $clientId) { throw 'The app registration was not created.' }

    # Application ID URI (audience consumers request a token for). Must be set after creation because
    # it can reference the app's own clientId.
    az ad app update --id $clientId --identifier-uris "api://$clientId" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "The registration $clientId was created but its identifier URI was not set. Set it in the portal." }

    az ad sp create --id $clientId --query id -o tsv | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "The registration $clientId was created but its service principal was not. Create it in the portal." }

    $tenantId = az account show --query tenantId -o tsv

    Write-Output ''
    Write-Output "Created. AzureAd__TenantId = $tenantId"
    Write-Output "         AzureAd__ClientId = $clientId"
    Write-Output "         Application ID URI = api://$clientId"
    Write-Output ''
    Write-Output 'Next, per consumer: run Grant-PeopleApiRole.ps1 to assign that consumer''s own managed identity,'
    Write-Output 'workload-identity-federated identity, or certificate-backed service principal to the People.Read or'
    Write-Output 'People.Admin role. No secret or token was printed - this API does not require one.'
}
finally {
    if (Test-Path -LiteralPath $rolesFile) { Remove-Item -LiteralPath $rolesFile -Force }
}

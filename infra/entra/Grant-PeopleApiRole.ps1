<#
.SYNOPSIS
Assigns one consumer's own Entra ID service identity (managed identity, workload-identity-federated app
registration, or certificate-backed app registration) to a People API app role (People.Read or
People.Admin). Safe by default: it only prints what it would do unless -Apply is given.

.DESCRIPTION
Run by the identity team, signed in to Azure CLI (az login) with Application.ReadWrite.All (or equivalent)
Microsoft Graph delegated/application permission. App-role assignments for Application-type roles are not
exposed by `az ad app permission`, so this script calls the Microsoft Graph appRoleAssignments API directly
via `az rest`.

Each consumer gets its own identity and exactly the role(s) it needs - do not share one identity across
consumers, and do not grant People.Admin to a consumer that only needs People.Read.

.PARAMETER ConsumerServicePrincipalObjectId
The OBJECT ID (not client/application ID) of the consumer's service principal - e.g. the object ID of its
user-assigned managed identity, or of its own app registration's service principal when using workload
identity federation or a certificate. Find it with:
  az identity show --name <uami-name> --resource-group <rg> --query principalId -o tsv
or
  az ad sp show --id <consumer-client-id> --query id -o tsv

.EXAMPLE
./infra/entra/Grant-PeopleApiRole.ps1 -PeopleApiClientId '<people-api-app-id>' -ConsumerServicePrincipalObjectId '<consumer-sp-object-id>' -Role People.Read
Shows the plan.

.EXAMPLE
./infra/entra/Grant-PeopleApiRole.ps1 -PeopleApiClientId '<people-api-app-id>' -ConsumerServicePrincipalObjectId '<consumer-sp-object-id>' -Role People.Read -Apply
Grants the role.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9a-fA-F-]{36}$')]
    [string] $PeopleApiClientId,

    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9a-fA-F-]{36}$')]
    [string] $ConsumerServicePrincipalObjectId,

    [Parameter(Mandatory)]
    [ValidateSet('People.Read', 'People.Admin')]
    [string] $Role,

    [switch] $Apply
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$peopleApiSpId = az ad sp show --id $PeopleApiClientId --query id -o tsv
if ($LASTEXITCODE -ne 0 -or -not $peopleApiSpId) { throw "No service principal found for People API app '$PeopleApiClientId'. Create it with New-PeopleApiEntraRegistration.ps1 first." }

$appRoleId = az ad sp show --id $peopleApiSpId --query "appRoles[?value=='$Role'].id | [0]" -o tsv
if ($LASTEXITCODE -ne 0 -or -not $appRoleId -or $appRoleId -eq 'null') { throw "App role '$Role' was not found on the People API registration. Re-run New-PeopleApiEntraRegistration.ps1 or check -PeopleReadRole/-PeopleAdminRole were set correctly there." }

$consumer = az ad sp show --id $ConsumerServicePrincipalObjectId --query '{displayName:displayName, appId:appId}' -o json | ConvertFrom-Json
if ($LASTEXITCODE -ne 0 -or -not $consumer) { throw "No service principal found with object ID '$ConsumerServicePrincipalObjectId'." }

Write-Output "People API app      : $PeopleApiClientId (service principal $peopleApiSpId)"
Write-Output "Consumer            : $($consumer.displayName) (appId $($consumer.appId), object ID $ConsumerServicePrincipalObjectId)"
Write-Output "Role to grant       : $Role (app role ID $appRoleId)"

if (-not $Apply) {
    Write-Output ''
    Write-Output 'Plan only. Re-run with -Apply to grant the role.'
    return
}

$existingAssignments = az rest --method GET `
    --uri "https://graph.microsoft.com/v1.0/servicePrincipals/$ConsumerServicePrincipalObjectId/appRoleAssignments" `
    --query "value[?resourceId=='$peopleApiSpId' && appRoleId=='$appRoleId']" -o json | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) { throw 'Failed to check existing app-role assignments via Microsoft Graph.' }
if ($existingAssignments.Count -gt 0) {
    Write-Output ''
    Write-Output "Already granted - no change made (assignment id $($existingAssignments[0].id))."
    return
}

$bodyFile = Join-Path ([IO.Path]::GetTempPath()) "people-api-grant-$([guid]::NewGuid().ToString('N')).json"
try {
    [ordered]@{
        principalId = $ConsumerServicePrincipalObjectId
        resourceId = $peopleApiSpId
        appRoleId = $appRoleId
    } | ConvertTo-Json | Set-Content -LiteralPath $bodyFile -Encoding utf8

    az rest --method POST `
        --uri "https://graph.microsoft.com/v1.0/servicePrincipals/$ConsumerServicePrincipalObjectId/appRoleAssignments" `
        --body "@$bodyFile" --headers 'Content-Type=application/json' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'The Microsoft Graph app-role assignment request failed.' }

    Write-Output ''
    Write-Output "Granted $Role to $($consumer.displayName)."
    Write-Output 'Remember: a token already issued under the previous role set may remain usable until it expires -'
    Write-Output 'see docs/Operations-Notes.md for revocation guidance.'
}
finally {
    if (Test-Path -LiteralPath $bodyFile) { Remove-Item -LiteralPath $bodyFile -Force }
}

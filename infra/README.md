# Bicep handoff

This is a repository-only infrastructure handoff, not a deployment or production
approval. The receiving team supplies approved parameter values and manually
tests in its own subscription. No regional, quota, Azure Policy, what-if, cost,
recovery, compliance or deployed-runtime checks have been performed here.

This supersedes the earlier Data-Protection-only scope of this folder: the
templates now provision the People API's full compute and network stack
(mirroring `rpa-cph-non-subsidy` / `rpa-inspections-workbench` /
`rpa-mts-inspections` / `rpa-quality-checks`). There is no Azure SQL Database,
no Blob Storage account and no Data Protection key ring: the async API-call
audit log (`LogProcessingService`) now writes through the standard logging
pipeline (console / Application Insights when configured) instead of a SQL
table, and this is a stateless bearer-token API with no cookies, antiforgery
tokens or session state, so nothing in the app actually uses ASP.NET Core Data
Protection - the sibling apps need it to encrypt their Entra ID sign-in
cookie, but people-api validates bearer tokens statelessly and has no cookie
to protect. There is also no `api-keys` Blob container: every consumer now
authenticates with its own Entra ID identity and app role instead of an issued
API key (see [docs/Consumer-Handoff.md](../docs/Consumer-Handoff.md)).

## Files and stages

- [main.bicep](./main.bicep): foundation - network/private DNS/endpoints,
  user-assigned identity, ACR, Key Vault, monitoring and ACA environment.
  **No application starts.**
- [app.bicep](./app.bicep): application activation after the receiving-team
  prerequisites below. Uses the same foundation names and identity.
- [entra/New-PeopleApiEntraRegistration.ps1](./entra/New-PeopleApiEntraRegistration.ps1):
  creates the People API app registration with the `People.Read`/`People.Admin`
  app roles restricted to **Application** member types (every consumer is a
  service identity, never an interactively signed-in person).
- [entra/Grant-PeopleApiRole.ps1](./entra/Grant-PeopleApiRole.ps1): assigns one
  consumer's own service principal (managed identity, workload-identity-federated
  registration, or certificate-backed registration) to one of those roles, via
  the Microsoft Graph `appRoleAssignments` API. Run once per consumer.
- [Test-Templates.ps1](./Test-Templates.ps1): local compilation, synthetic
  negative tests and optional parameter-file validation. It does not log in,
  query Azure or deploy.
- Parameter examples are deliberately incomplete: `null` means **unknown**,
  not a valid production default. Copy them outside Git and supply every value.
  Their checked-in state is intentionally not deployable.

Both entry points target an existing resource group. The receiving team owns
resource-group creation, subscription selection and permissions. Supply the
same resource group, location, prefix, environment and tags for both stages.
Changing these identifiers can create a different stack; review what-if first.
The templates target Azure public cloud; sovereign-cloud compatibility is not
asserted.

## Local checks

Use an installed [Bicep CLI](https://learn.microsoft.com/azure/azure-resource-manager/bicep/install).
Pinned AVM modules are restored from the public Microsoft registry. Generated
ARM JSON is temporary, not a handoff source file.

```powershell
.\infra\Test-Templates.ps1 -BicepPath 'C:\tools\bicep.exe'
.\infra\Test-Templates.ps1 -BicepPath 'C:\tools\bicep.exe' `
  -FoundationParameters 'C:\private\people-api\foundation.parameters.json' `
  -ApplicationParameters 'C:\private\people-api\application.parameters.json'
```

Run the parameter check before any Azure what-if or deployment. It rejects
missing/unknown fields, wrong types, provider-independent bounds, invalid
names/CIDRs, overlapping or out-of-VNet subnets, unsupported local CPU/memory
choices, invalid image digests, duplicate app-role values, an empty Graph
scopes list and invalid CORS origins. Provider/region-specific combinations
still need owner checks. Do not treat passing this script as budget/compliance/
recovery certification.

## Required foundation inputs

Every input is required; none selects a region or production sizing for you.

| Inputs | Contract |
|---|---|
| `location`, `tags` | Owner-approved region and tag object; no default region. |
| `prefix`, `environmentName` | Lowercase alphanumeric, 2-6 and 2-3 characters. Names include deterministic resource-group-scoped uniqueness. |
| `vnetCidr`, `acaSubnetCidr`, `privateEndpointSubnetCidr` | Approved, canonical RFC1918 IPv4 ranges; nonoverlapping subnets inside VNet. ACA subnet /27 or larger. Check overlap with all connected networks and ACA platform-reserved ranges externally. |
| `acaZoneRedundant` | Explicit zone choice, subject to Consumption/region support. No Dedicated fallback. |
| `acrSku`, `acrZoneRedundant` | Basic/Standard/Premium; zone redundancy requires Premium. Standard registry RBAC, not repository ABAC. |
| `logRetentionDays`, `applicationInsightsRetentionDays` | Explicit supported retention; eventual regulatory adequacy remains owner review. |
| `vaultSoftDeleteDays` | Vault soft-delete 7-90 days. Vault purge protection is enabled. |

This creates one ACA environment, not a multi-region failover topology. No
paired region, recovery strategy, monthly ceiling or legal scope is assumed.

## Required application inputs

Copy [app.parameters.example.json](./app.parameters.example.json) outside Git
and replace every `null`. The example contains no credentials.

| Inputs | Contract |
|---|---|
| `location`, `prefix`, `environmentName`, `tags` | Match foundation exactly. |
| `imageRepository`, `imageDigest` | Repository inside the new ACR and immutable `sha256:` digest obtained after receiving-team push. |
| `entraTenantId`, `entraClientId` | The People API's own app registration (from `entra/New-PeopleApiEntraRegistration.ps1`) - the audience every consumer's token must target. Never assumed from local development. |
| `peopleReadRole`, `peopleAdminRole` | Exact app-role token values from that registration; must be distinct. Read access is satisfied by either role. |
| `graphApiClientId`, `graphApiTenantId`, `graphApiScopes` | The **separate** Microsoft Graph app registration People API calls *as itself* (client-credentials flow) - not the AzureAd:* values above. At least one scope (e.g. `User.Read.All`). |
| `graphApiClientSecretName` | Leave as an explicit empty string (recommended, passwordless) to use the runtime managed identity for Graph instead - see `docs/Operations-Notes.md`. Otherwise the owner-populated Key Vault secret **name**, never a secret value. |
| `corsAllowedOrigins` | Explicit array of allowed browser origins; leave empty (CORS stays denied by default) for server-to-server-only consumers. |
| `containerSize` | Explicit supported CPU:memory pair, e.g. format `CPU:GiB`, selected from the Bicep allowed-values list. No default pair. |
| `minReplicas`, `maxReplicas`, `concurrentRequests` | Explicit bounds/HTTP threshold; min <= max, max 1-300. Confirm workload quota and subnet capacity before deployment. |
| `terminationGracePeriodSeconds` | Explicit shutdown allowance, 1-600 seconds. |

Liveness/readiness use HTTP 8080 with the application hostname as the Host
header, 10-second periods and three-failure thresholds; these operational
probe settings are not an RTO/SLA. No platform Easy Auth is layered over the
existing application's Microsoft.Identity.Web bearer-token validation.

Application Insights receives its non-secret endpoint connection string plus
`Telemetry__ManagedIdentityClientId`; local ingestion authentication is
disabled on the component and the runtime identity receives component-scoped
Monitoring Metrics Publisher. Key Vault is used only for the **optional**
Graph client secret (`graphApiClientSecretName`) - leave it empty to avoid
storing any secret at all. No storage keys/SQL passwords are supplied to the
app, and there is no database to provision at all.

## Manual deployment checkpoints

The following are **receiving-team commands**, not commands executed by this
handoff. Obtain production approvals before using them.

### 1. Foundation

```powershell
az account set --subscription '<approved-subscription-id>'
az deployment group what-if --resource-group '<approved-resource-group>' `
  --template-file '.\infra\main.bicep' `
  --parameters '@C:\private\people-api\foundation.parameters.json'
az deployment group create --name 'people-api-foundation' `
  --resource-group '<approved-resource-group>' `
  --template-file '.\infra\main.bicep' `
  --parameters '@C:\private\people-api\foundation.parameters.json'
az deployment group show --name 'people-api-foundation' `
  --resource-group '<approved-resource-group>' --query properties.outputs
```

Stop on failed validation/deployment. Review regional Consumption/private
network/zone support, SKU availability, quotas, costs, tenancy and policy before
retrying; never silently substitute a region, compute model or SKU.

### 2. Owner bootstrap, from an approved private-network path

1. Verify private DNS resolves Key Vault through the new private endpoint
   from the workload and operator execution paths. Public access to Key
   Vault is disabled.
2. Push the receiving team's built image into ACR with a separate authorized
   build/push principal. Runtime gets registry-scoped `AcrPull` only. Obtain the
   repository's immutable `sha256:` digest.
3. Run `entra/New-PeopleApiEntraRegistration.ps1 -Apply` to create the People
   API's own app registration and its `People.Read`/`People.Admin` app roles.
   Then, **per consumer**, run `entra/Grant-PeopleApiRole.ps1 -Apply` to assign
   that consumer's own managed identity/workload-identity-federated
   registration/certificate-backed registration to the role it needs. Never
   share one identity across consumers, and never grant `People.Admin` to a
   consumer that only needs read access.
4. If using the optional Graph client-secret path, populate the configuration
   secret in Key Vault through approved owner tooling and private access, and
   supply only its name to `graphApiClientSecretName`. The recommended default
   is to leave it empty and grant the runtime managed identity the
   `User.Read.All` Microsoft Graph application permission instead (no secret
   to rotate or leak).
5. Allow identity/RBAC propagation, then explicitly verify image pull, secret
   resolution and private service access before application activation.
6. Configure receiving-team operational alerts, notification routing and
   recovery procedures. No alert rules/action groups are generated here.

### 3. Application activation and acceptance

```powershell
az deployment group what-if --resource-group '<approved-resource-group>' `
  --template-file '.\infra\app.bicep' `
  --parameters '@C:\private\people-api\application.parameters.json'
az deployment group create --name 'people-api-application' `
  --resource-group '<approved-resource-group>' `
  --template-file '.\infra\app.bicep' `
  --parameters '@C:\private\people-api\application.parameters.json'
```

Verify HTTPS, anonymous liveness/readiness (readiness reflects Microsoft Graph
reachability), a bearer token with `People.Read` succeeding on `/getusers/*`
and failing with 403 on `/admin/*`, and a `People.Admin` token succeeding on
both.

On application failure, retain foundation and identity for diagnosis. The
operator owns rollback to the last known-good image/configuration.

## Module pins and validation boundary

AVM versions are explicit in source and restored from Microsoft Container
Registry: identity 0.6.0; VNet 0.10.2; Log Analytics 0.16.1; Application Insights
0.8.0; ACR 0.13.1; Key Vault 0.14.2; ACA environment 0.16.0; ACA
application 0.23.0. The private endpoint/DNS helper and NSG resources use
native Bicep for their small, explicit contracts. AVM usage telemetry is
disabled.

Compilation does not exercise ARM authorization, private data-plane access,
provider policies or RBAC propagation. No Phase 4 approval is implied by the
existence of these files.

References: [ACA networking](https://learn.microsoft.com/azure/container-apps/networking),
[ACA NSG requirements](https://learn.microsoft.com/azure/container-apps/firewall-integration),
[managed-identity ACR pulls](https://learn.microsoft.com/azure/container-apps/managed-identity-image-pull),
[Key Vault network security](https://learn.microsoft.com/azure/key-vault/general/network-security),
[Microsoft Graph app role assignments](https://learn.microsoft.com/graph/api/serviceprincipal-post-approleassignedto).

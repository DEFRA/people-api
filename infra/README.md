# People API — Data Protection infra (Blob + Key Vault key ring)

Remediates the gap in [docs/Operations-Notes.md](../docs/Operations-Notes.md#known-limitations--follow-ups-not-changed-in-this-pass):
the Data Protection key ring is not persisted, so `Encryption:MasterKey`/`Authentication:AdminApiKey`
protection breaks on restart or when scaled to more than one replica. Follows the same
Blob-persistence + Key-Vault-wrapping approach already used by `rpa-cph-non-subsidy`
(see its [infra/README.md](../../rpa-cph-non-subsidy/infra/README.md)), scoped to just
the resources this remediation needs — it does not provision people-api's compute host.

## Files

- [main.bicep](./main.bicep): foundation — user-assigned identity, Key Vault (RBAC), and a
  private Blob container (`data-protection`) for the persisted key-ring XML. Does not create
  the wrapping key itself (see step 2 below) and does not provision compute.
- [app.bicep](./app.bicep): grants the identity **Key Vault Crypto User** on the already-created
  wrapping key, and outputs the `DataProtection__*` app settings ready to attach to whichever
  compute hosts people-api (Container Apps, App Service, or AKS).
- [Initialize-DataProtectionKey.ps1](./Initialize-DataProtectionKey.ps1): operator-run key
  creation/rotation-policy step. Idempotent — reuses a compatible existing `data-protection`
  RSA key rather than recreating it.

## Required inputs

| Parameter | Contract |
|---|---|
| `prefix`, `environmentName` | Lowercase alphanumeric, 2-6 and 2-3 characters; must match between `main.bicep` and `app.bicep`. |
| `keyRingStorageSku` | `Standard_LRS`/`Standard_ZRS`/`Standard_GRS`/`Standard_GZRS`. |
| `blobSoftDeleteDays` | 1-365. `vaultSoftDeleteDays` | 7-90 (vault purge protection is always enabled). |
| `wrappingKeySize` | `2048`/`3072`/`4096`. `wrappingKeyRotationDays` must be less than `wrappingKeyExpiryDays`. |

## Deployment order

```powershell
# 1. Foundation
az deployment group create --name people-api-dp-foundation --resource-group '<rg>' `
  --template-file .\infra\main.bicep --parameters '@.\infra\main.parameters.json'
az deployment group show --name people-api-dp-foundation --resource-group '<rg>' --query properties.outputs

# 2. Create/verify the wrapping key (read dataProtectionKeyBootstrap from step 1 outputs)
.\infra\Initialize-DataProtectionKey.ps1 -SubscriptionId '<output subscriptionId>' `
  -ResourceGroupName '<output resourceGroupName>' -VaultName '<output vaultName>' `
  -WrappingKeySize <output wrappingKeySize> -WrappingKeyRotationDays <output wrappingKeyRotationDays> `
  -WrappingKeyExpiryDays <output wrappingKeyExpiryDays>

# 3. Grant the identity crypto access to the key and get the app settings to wire in
az deployment group create --name people-api-dp-app --resource-group '<rg>' `
  --template-file .\infra\app.bicep --parameters '@.\infra\app.parameters.json'
az deployment group show --name people-api-dp-app --resource-group '<rg>' --query properties.outputs.appSettings.value
```

Apply the `appSettings` output as environment variables (`DataProtection__Enabled`,
`DataProtection__BlobUri`, `DataProtection__KeyIdentifier`, `DataProtection__ApplicationName`,
`DataProtection__ManagedIdentityClientId`) on people-api's compute, and attach the
user-assigned identity from `main.bicep`'s `managedIdentityResourceId` output to that compute.
`SharedDataProtection.AddPeopleApiDataProtection` (in
[Services/Crypto/SharedDataProtection.cs](../defra-people-api/Services/Crypto/SharedDataProtection.cs))
reads these settings and only activates Blob+Key Vault persistence when `DataProtection:Enabled=true`;
leaving it `false`/unset preserves today's local/ephemeral key ring for environments without this infra.

## Notes

- Role assignments: `Key Vault Secrets User` (vault-scoped, for existing `GraphApi`/`LogDb`/
  `Encryption` secrets) and `Storage Blob Data Contributor` (container-scoped) come from
  `main.bicep`; `Key Vault Crypto User` (key-scoped) comes from `app.bicep` after the key exists.
- This template keeps the Key Vault and storage account on public network access, unlike
  `rpa-cph-non-subsidy`'s private-endpoint topology — add private endpoints/VNet integration
  separately if/when people-api's hosting network design requires it.
- `ApplicationName` must stay identical across replicas of the same environment and differ
  between environments, or key-ring entries won't decrypt across them.

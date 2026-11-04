targetScope = 'resourceGroup'

@description('Explicit approved Azure location; no default region.')
@minLength(1)
param location string

@description('Two to six lowercase alphanumeric characters.')
@minLength(2)
@maxLength(6)
param prefix string

@description('Two or three lowercase alphanumeric characters identifying this environment.')
@minLength(2)
@maxLength(3)
param environmentName string

param tags object

@allowed(['Standard_LRS', 'Standard_ZRS', 'Standard_GRS', 'Standard_GZRS'])
param keyRingStorageSku string

@minValue(1)
@maxValue(365)
param blobSoftDeleteDays int

@minValue(7)
@maxValue(90)
param vaultSoftDeleteDays int

@minValue(28)
@description('Owner-bootstrap key rotation period; emitted as a bootstrap input, not applied by ARM.')
param wrappingKeyRotationDays int

@minValue(28)
@description('Owner-bootstrap key expiry policy; retain historical versions needed for decryption.')
param wrappingKeyExpiryDays int

@allowed([2048, 3072, 4096])
@description('RSA wrapping-key size for the separate private bootstrap step.')
param wrappingKeySize int

var stem = '${prefix}-${environmentName}'
var globalStem = '${prefix}${environmentName}${uniqueString(resourceGroup().id, prefix, environmentName)}'
var keyRingContainerName = 'data-protection'
var wrappingKeyName = 'data-protection'

// Runtime identity for people-api; grant it access to Key Vault secrets (GraphApi/LogDb/Encryption
// config, same as today's KeyVault:Uri support) and to the Data Protection key ring container.
module identity 'br/public:avm/res/managed-identity/user-assigned-identity:0.6.0' = {
  name: '${stem}-identity'
  params: {
    name: '${stem}-id'
    location: location
    tags: tags
    enableTelemetry: false
  }
}

module vault 'br/public:avm/res/key-vault/vault:0.14.2' = {
  name: '${stem}-vault'
  params: {
    name: 'kv${globalStem}'
    location: location
    tags: tags
    enableTelemetry: false
    sku: 'standard'
    enableRbacAuthorization: true
    enableSoftDelete: true
    enablePurgeProtection: true
    softDeleteRetentionInDays: vaultSoftDeleteDays
    enableVaultForDeployment: false
    enableVaultForTemplateDeployment: false
    enableVaultForDiskEncryption: false
    roleAssignments: [
      {
        principalId: identity.outputs.principalId
        principalType: 'ServicePrincipal'
        roleDefinitionIdOrName: 'Key Vault Secrets User'
      }
    ]
  }
}

// Private container for the persisted Data Protection key ring XML (PersistKeysToAzureBlobStorage).
module storage 'br/public:avm/res/storage/storage-account:0.33.1' = {
  name: '${stem}-keyring'
  params: {
    name: 'st${globalStem}'
    location: location
    tags: tags
    enableTelemetry: false
    skuName: keyRingStorageSku
    kind: 'StorageV2'
    allowSharedKeyAccess: false
    defaultToOAuthAuthentication: true
    allowBlobPublicAccess: false
    allowCrossTenantReplication: false
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    fileServices: {}
    queueServices: {}
    tableServices: {}
    blobServices: {
      isVersioningEnabled: true
      deleteRetentionPolicyEnabled: true
      deleteRetentionPolicyDays: blobSoftDeleteDays
      containerDeleteRetentionPolicyEnabled: true
      containerDeleteRetentionPolicyDays: blobSoftDeleteDays
      containers: [
        {
          name: keyRingContainerName
          publicAccess: 'None'
          roleAssignments: [
            {
              principalId: identity.outputs.principalId
              principalType: 'ServicePrincipal'
              roleDefinitionIdOrName: 'Storage Blob Data Contributor'
            }
          ]
        }
      ]
    }
  }
}

output managedIdentityResourceId string = identity.outputs.resourceId
output managedIdentityPrincipalId string = identity.outputs.principalId
output managedIdentityClientId string = identity.outputs.clientId
output keyVaultUri string = vault.outputs.uri
output keyVaultName string = vault.outputs.name
output storageAccountName string = storage.outputs.name
output dataProtectionBlobUri string = 'https://${storage.outputs.name}.blob.${az.environment().suffixes.storage}/${keyRingContainerName}/keys.xml'
output dataProtectionKeyIdentifier string = '${vault.outputs.uri}keys/${wrappingKeyName}'
output dataProtectionApplicationName string = '${stem}-app'
output dataProtectionKeyBootstrap object = {
  subscriptionId: subscription().subscriptionId
  resourceGroupName: resourceGroup().name
  vaultName: vault.outputs.name
  keyName: wrappingKeyName
  wrappingKeySize: wrappingKeySize
  wrappingKeyRotationDays: wrappingKeyRotationDays
  wrappingKeyExpiryDays: wrappingKeyExpiryDays
}

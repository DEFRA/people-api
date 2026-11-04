targetScope = 'resourceGroup'

@minLength(1)
param location string

@minLength(2)
@maxLength(6)
param prefix string

@minLength(2)
@maxLength(3)
param environmentName string

var stem = '${prefix}-${environmentName}'
var globalStem = '${prefix}${environmentName}${uniqueString(resourceGroup().id, prefix, environmentName)}'
var wrappingKeyName = 'data-protection'
var applicationName = '${stem}-app'

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' existing = {
  name: '${stem}-id'
}

resource vault 'Microsoft.KeyVault/vaults@2024-11-01' existing = {
  name: 'kv${globalStem}'
}

resource storage 'Microsoft.Storage/storageAccounts@2025-01-01' existing = {
  name: 'st${globalStem}'
}

// Key is created out-of-band by Initialize-DataProtectionKey.ps1; this only grants wrap/unwrap once it exists.
resource wrappingKey 'Microsoft.KeyVault/vaults/keys@2024-11-01' existing = {
  parent: vault
  name: wrappingKeyName
}

resource keyAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(wrappingKey.id, identity.id, 'key-wrap')
  scope: wrappingKey
  properties: {
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'e147488a-f6f5-4113-8e2d-b22465e65bf6') // Key Vault Crypto User
  }
}

// Ready-to-wire app settings (as env vars: DataProtection__<Key>/ApiKeyStorage__<Key>) for whichever
// compute hosts people-api (Container Apps, App Service, or AKS) - this template does not assume the host.
output appSettings object = {
  DataProtection__Enabled: 'true'
  DataProtection__BlobUri: 'https://${storage.name}.blob.${az.environment().suffixes.storage}/data-protection/keys.xml'
  DataProtection__KeyIdentifier: '${vault.properties.vaultUri}keys/${wrappingKeyName}'
  DataProtection__ApplicationName: applicationName
  DataProtection__ManagedIdentityClientId: identity.properties.clientId
  ApiKeyStorage__ContainerUri: 'https://${storage.name}.blob.${az.environment().suffixes.storage}/api-keys'
  ApiKeyStorage__ManagedIdentityClientId: identity.properties.clientId
}

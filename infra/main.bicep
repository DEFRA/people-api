targetScope = 'resourceGroup'

@description('Explicit receiving-team-approved Azure location; no default region.')
@minLength(1)
param location string

@description('Two to six lowercase alphanumeric characters; validated by Test-Templates.ps1.')
@minLength(2)
@maxLength(6)
param prefix string

@description('Two or three lowercase alphanumeric characters identifying this environment.')
@minLength(2)
@maxLength(3)
param environmentName string

param tags object
param vnetCidr string
param acaSubnetCidr string
param privateEndpointSubnetCidr string
param acaZoneRedundant bool

@allowed(['Basic', 'Standard', 'Premium'])
param acrSku string

@description('Requires Premium when true; no registry replication is implied.')
param acrZoneRedundant bool

@minValue(30)
@maxValue(730)
param logRetentionDays int

@allowed([30, 60, 90, 120, 180, 270, 365, 550, 730])
param applicationInsightsRetentionDays int

@minValue(7)
@maxValue(90)
param vaultSoftDeleteDays int

var stem = '${prefix}-${environmentName}'
var globalStem = '${prefix}${environmentName}${uniqueString(resourceGroup().id, prefix, environmentName)}'
var applicationName = '${stem}-app'

module identity 'br/public:avm/res/managed-identity/user-assigned-identity:0.6.0' = {
  name: '${stem}-identity'
  params: {
    name: '${stem}-id'
    location: location
    tags: tags
    enableTelemetry: false
  }
}

resource acaNsg 'Microsoft.Network/networkSecurityGroups@2024-05-01' = {
  name: '${stem}-aca-nsg'
  location: location
  tags: tags
  properties: {
    securityRules: [
      {
        name: 'AllowPublicHttpsIngress'
        properties: {
          priority: 200
          access: 'Allow'
          direction: 'Inbound'
          protocol: 'Tcp'
          sourceAddressPrefix: 'Internet'
          sourcePortRange: '*'
          destinationAddressPrefix: acaSubnetCidr
          destinationPortRanges: ['443', '31443']
        }
      }
    ]
  }
}

resource endpointNsg 'Microsoft.Network/networkSecurityGroups@2024-05-01' = {
  name: '${stem}-pe-nsg'
  location: location
  tags: tags
  properties: {
    securityRules: []
  }
}

module network 'br/public:avm/res/network/virtual-network:0.10.2' = {
  name: '${stem}-network'
  params: {
    name: '${stem}-vnet'
    location: location
    tags: tags
    enableTelemetry: false
    addressPrefixes: [vnetCidr]
    subnets: [
      {
        name: 'aca'
        addressPrefix: acaSubnetCidr
        delegation: 'Microsoft.App/environments'
        networkSecurityGroupResourceId: acaNsg.id
      }
      {
        name: 'private-endpoints'
        addressPrefix: privateEndpointSubnetCidr
        privateEndpointNetworkPolicies: 'Enabled'
        networkSecurityGroupResourceId: endpointNsg.id
      }
    ]
  }
}

module logs 'br/public:avm/res/operational-insights/workspace:0.16.1' = {
  name: '${stem}-logs'
  params: {
    name: '${stem}-logs'
    location: location
    tags: tags
    enableTelemetry: false
    skuName: 'PerGB2018'
    dataRetention: logRetentionDays
    forceCmkForQuery: false
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

module insights 'br/public:avm/res/insights/component:0.8.0' = {
  name: '${stem}-insights'
  params: {
    name: '${stem}-insights'
    location: location
    tags: tags
    enableTelemetry: false
    workspaceResourceId: logs.outputs.resourceId
    applicationType: 'web'
    kind: 'web'
    disableIpMasking: false
    disableLocalAuth: true
    retentionInDays: applicationInsightsRetentionDays
  }
}

resource insightsScope 'Microsoft.Insights/components@2020-02-02' existing = {
  name: '${stem}-insights'
}

resource telemetryAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(insightsScope.id, resourceId('Microsoft.ManagedIdentity/userAssignedIdentities', '${stem}-id'), 'telemetry')
  scope: insightsScope
  properties: {
    principalId: identity.outputs.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '3913510d-42f4-4e42-8a64-420c390055eb')
  }
  dependsOn: [insights]
}

module registry 'br/public:avm/res/container-registry/registry:0.13.1' = {
  name: '${stem}-registry'
  params: {
    name: 'acr${globalStem}'
    location: location
    tags: tags
    enableTelemetry: false
    acrSku: acrSku
    acrAdminUserEnabled: false
    anonymousPullEnabled: false
    roleAssignmentMode: 'LegacyRegistryPermissions'
    azureADAuthenticationAsArmPolicyStatus: 'enabled'
    publicNetworkAccess: 'Enabled'
    networkRuleSetDefaultAction: 'Allow'
    zoneRedundancy: acrZoneRedundant ? 'Enabled' : 'Disabled'
    retentionPolicyStatus: 'disabled'
    exportPolicyStatus: 'enabled'
    diagnosticSettings: [
      {
        name: 'platform'
        workspaceResourceId: logs.outputs.resourceId
      }
    ]
    roleAssignments: [
      {
        principalId: identity.outputs.principalId
        principalType: 'ServicePrincipal'
        roleDefinitionIdOrName: 'AcrPull'
      }
    ]
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
    publicNetworkAccess: 'Disabled'
    networkAcls: {
      bypass: 'None'
      defaultAction: 'Deny'
    }
    diagnosticSettings: [
      {
        name: 'platform'
        workspaceResourceId: logs.outputs.resourceId
      }
    ]
    roleAssignments: [
      {
        principalId: identity.outputs.principalId
        principalType: 'ServicePrincipal'
        roleDefinitionIdOrName: 'Key Vault Secrets User'
      }
    ]
  }
}

var privateServices = [
  {
    name: 'vault'
    id: resourceId('Microsoft.KeyVault/vaults', 'kv${globalStem}')
    groupId: 'vault'
    zone: 'privatelink.vaultcore.azure.net'
  }
]

module privateConnections 'modules/private-endpoint.bicep' = [
  for service in privateServices: {
    name: '${stem}-${service.name}-private'
    params: {
      name: '${stem}-${service.name}-pe'
      location: location
      tags: tags
      targetResourceId: service.id
      groupId: service.groupId
      dnsZoneName: service.zone
      vnetResourceId: network.outputs.resourceId
      subnetResourceId: '${network.outputs.resourceId}/subnets/private-endpoints'
    }
    dependsOn: [vault]
  }
]

module environment 'br/public:avm/res/app/managed-environment:0.16.0' = {
  name: '${stem}-environment'
  params: {
    name: '${stem}-env'
    location: location
    tags: tags
    enableTelemetry: false
    infrastructureSubnetResourceId: '${network.outputs.resourceId}/subnets/aca'
    internal: false
    publicNetworkAccess: 'Enabled'
    zoneRedundant: acaZoneRedundant
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
    ]
    appLogsConfiguration: {
      destination: 'azure-monitor'
    }
    diagnosticSettings: [
      {
        name: 'platform'
        workspaceResourceId: logs.outputs.resourceId
      }
    ]
  }
}

output applicationHost string = '${applicationName}.${environment.outputs.defaultDomain}'
output applicationOrigin string = 'https://${applicationName}.${environment.outputs.defaultDomain}'
output applicationName string = applicationName
output managedEnvironmentResourceId string = environment.outputs.resourceId
output managedIdentityResourceId string = identity.outputs.resourceId
output managedIdentityPrincipalId string = identity.outputs.principalId
output managedIdentityClientId string = identity.outputs.clientId
output registryName string = registry.outputs.name
output registryLoginServer string = registry.outputs.loginServer
output keyVaultUri string = vault.outputs.uri
output keyVaultName string = vault.outputs.name
output applicationInsightsResourceId string = insights.outputs.resourceId
output logAnalyticsResourceId string = logs.outputs.resourceId
output acaSubnetCidr string = acaSubnetCidr

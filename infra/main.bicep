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

@description('Explicitly approved Premium SKU required for ACR Private Link, not for zone redundancy.')
@allowed(['Premium'])
param acrSku string

@description('Legacy ACR property retained for compatibility. Zone redundancy is automatic across all tiers in supported regions regardless of this value; no geo-replication is implied.')
param acrZoneRedundant bool

@minValue(30)
@maxValue(730)
param logRetentionDays int

@allowed([30, 60, 90, 120, 180, 270, 365, 550, 730])
param applicationInsightsRetentionDays int

@minValue(7)
@maxValue(90)
param vaultSoftDeleteDays int

@sealed()
type newAmplsConfig = {
  mode: 'Create'
  associationOwner: 'Project'
  ingestionAccessMode: 'PrivateOnly'
  queryAccessMode: 'PrivateOnly'
}

@sealed()
type existingAmplsConfig = {
  mode: 'Existing'
  resourceId: string
  associationOwner: 'Project' | 'Platform'
  workspaceAssociationName: string?
  applicationInsightsAssociationName: string?
}

@discriminator('mode')
type amplsConfig = newAmplsConfig | existingAmplsConfig

@sealed()
type newMonitorEndpointConfig = {
  mode: 'Create'
}

@sealed()
type existingMonitorEndpointConfig = {
  mode: 'Existing'
  resourceId: string
}

@discriminator('mode')
type monitorEndpointConfig = newMonitorEndpointConfig | existingMonitorEndpointConfig

type monitorZoneName = 'privatelink.monitor.azure.com'
  | 'privatelink.oms.opinsights.azure.com'
  | 'privatelink.ods.opinsights.azure.com'
  | 'privatelink.agentsvc.azure-automation.net'
  | 'privatelink.blob.core.windows.net'

@sealed()
type newMonitorZoneConfig = {
  name: monitorZoneName
  mode: 'Create'
  linkMode: 'Create'
}

@sealed()
type existingMonitorZoneConfig = {
  name: monitorZoneName
  mode: 'Existing'
  resourceId: string
  linkMode: 'Create' | 'Existing' | 'External'
  existingLinkName: string?
}

@discriminator('mode')
type monitorZoneConfig = newMonitorZoneConfig | existingMonitorZoneConfig

@sealed()
type managedMonitorDnsConfig = {
  mode: 'Managed'
  @minLength(5)
  @maxLength(5)
  zones: monitorZoneConfig[]
}

@sealed()
type platformMonitorDnsConfig = {
  mode: 'PlatformManaged'
}

@discriminator('mode')
type monitorDnsConfig = managedMonitorDnsConfig | platformMonitorDnsConfig

@description('Private is the secure default. Transition preserves public monitoring only during an explicitly approved, tested cutover.')
@allowed(['Private', 'Transition'])
param monitoringAccessProfile string = 'Private'

@description('Approved change/exception reference for Transition; explicit empty string for Private. Validate with Test-Templates.ps1 before deployment.')
param monitoringTransitionApprovalReference string

param monitoringAmpls amplsConfig
param monitoringPrivateEndpoint monitorEndpointConfig
param monitoringDns monitorDnsConfig

var stem = '${prefix}-${environmentName}'
var globalStem = '${prefix}${environmentName}${uniqueString(resourceGroup().id, prefix, environmentName)}'
var applicationName = '${stem}-app'
var monitoringPublicNetworkAccess = monitoringAccessProfile == 'Transition' && !empty(monitoringTransitionApprovalReference)
  ? 'Enabled'
  : 'Disabled'
var monitorAmplsId = monitoringAmpls.mode == 'Create'
  ? resourceId('Microsoft.Insights/privateLinkScopes', '${stem}-ampls')
  : monitoringAmpls.resourceId
var monitorZones = monitoringDns.mode == 'Managed' ? monitoringDns.zones : []
var monitorNewZones = filter(monitorZones, zone => zone.mode == 'Create')
var monitorLinkedZones = filter(monitorZones, zone => zone.linkMode == 'Create')
var monitorZoneIds = [
  for zone in monitorZones: zone.mode == 'Create'
    ? resourceId('Microsoft.Network/privateDnsZones', zone.name)
    : zone.resourceId
]

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
        name: 'AllowPrivateHttpsIngress'
        properties: {
          priority: 200
          access: 'Allow'
          direction: 'Inbound'
          protocol: 'Tcp'
          sourceAddressPrefix: 'VirtualNetwork'
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
    publicNetworkAccessForIngestion: monitoringPublicNetworkAccess
    publicNetworkAccessForQuery: monitoringPublicNetworkAccess
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
    publicNetworkAccessForIngestion: monitoringPublicNetworkAccess
    publicNetworkAccessForQuery: monitoringPublicNetworkAccess
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
    publicNetworkAccess: 'Disabled'
    networkRuleSetDefaultAction: 'Deny'
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
  {
    name: 'registry'
    id: resourceId('Microsoft.ContainerRegistry/registries', 'acr${globalStem}')
    groupId: 'registry'
    zone: 'privatelink.azurecr.io'
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
    dependsOn: [vault, registry]
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
    internal: true
    publicNetworkAccess: 'Disabled'
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

module environmentDns 'modules/internal-environment-dns.bicep' = {
  name: '${stem}-environment-dns'
  params: {
    dnsZoneName: environment.outputs.defaultDomain
    ipv4Address: environment.outputs.staticIp!
    vnetResourceId: network.outputs.resourceId
    tags: tags
  }
}

module monitorScope 'modules/monitor-private-link-scope.bicep' = if (monitoringAmpls.associationOwner == 'Project') {
  name: '${stem}-monitor-scope-${uniqueString(resourceGroup().id)}'
  scope: resourceGroup(split(monitorAmplsId, '/')[2], split(monitorAmplsId, '/')[4])
  params: {
    name: last(split(monitorAmplsId, '/'))
    tags: tags
    createScope: monitoringAmpls.mode == 'Create'
    workspaceResourceId: logs.outputs.resourceId
    applicationInsightsResourceId: insights.outputs.resourceId
    workspaceAssociationName: monitoringAmpls.mode == 'Create'
      ? 'workspace-${uniqueString(resourceGroup().id, stem)}'
      : (monitoringAmpls.associationOwner == 'Project' ? monitoringAmpls.workspaceAssociationName! : '')
    applicationInsightsAssociationName: monitoringAmpls.mode == 'Create'
      ? 'insights-${uniqueString(resourceGroup().id, stem)}'
      : (monitoringAmpls.associationOwner == 'Project' ? monitoringAmpls.applicationInsightsAssociationName! : '')
  }
}

module monitorDnsZones 'modules/monitor-private-dns-zone.bicep' = [
  for zone in monitorNewZones: {
    name: '${stem}-monitor-dns-${uniqueString(resourceGroup().id, zone.name)}'
    params: {
      dnsZoneName: zone.name
      tags: tags
      createZone: true
      createVnetLink: false
      vnetResourceId: resourceId('Microsoft.Network/virtualNetworks', '${stem}-vnet')
    }
  }
]

module monitorEndpoint 'modules/monitor-private-endpoint.bicep' = if (monitoringPrivateEndpoint.mode == 'Create') {
  name: '${stem}-monitor-private'
  params: {
    name: '${stem}-monitor-pe'
    location: location
    tags: tags
    amplsResourceId: monitorAmplsId
    subnetResourceId: '${network.outputs.resourceId}/subnets/private-endpoints'
    dnsZoneResourceIds: monitorZoneIds
  }
  dependsOn: [monitorScope, monitorDnsZones]
}

// Publish new links only after the endpoint's DNS zone group has been provisioned.
module monitorDnsLinks 'modules/monitor-private-dns-zone.bicep' = [
  for zone in monitorLinkedZones: {
    name: '${stem}-monitor-link-${uniqueString(resourceGroup().id, zone.name)}'
    scope: resourceGroup(
      zone.mode == 'Create' ? subscription().subscriptionId : split(zone.resourceId, '/')[2],
      zone.mode == 'Create' ? resourceGroup().name : split(zone.resourceId, '/')[4]
    )
    params: {
      dnsZoneName: zone.name
      tags: tags
      createZone: false
      createVnetLink: true
      vnetResourceId: resourceId('Microsoft.Network/virtualNetworks', '${stem}-vnet')
    }
    dependsOn: [network, monitorEndpoint, monitorDnsZones]
  }
]

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
output virtualNetworkResourceId string = network.outputs.resourceId
output applicationPrivateDnsZoneId string = environmentDns.outputs.resourceId
output applicationInternalIpAddress string = environment.outputs.staticIp!
output monitorPrivateLinkScopeResourceId string = monitorAmplsId
output monitorPrivateEndpointResourceId string = monitoringPrivateEndpoint.mode == 'Create'
  ? monitorEndpoint!.outputs.resourceId
  : monitoringPrivateEndpoint.resourceId
output monitorPrivateDnsZoneResourceIds array = monitorZoneIds
output monitoringAccessProfile string = monitoringAccessProfile

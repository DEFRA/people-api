targetScope = 'resourceGroup'

param dnsZoneName string
param tags object
param createZone bool
param createVnetLink bool
param vnetResourceId string

resource managedZone 'Microsoft.Network/privateDnsZones@2024-06-01' = if (createZone) {
  name: dnsZoneName
  location: 'global'
  tags: tags
}

resource zone 'Microsoft.Network/privateDnsZones@2024-06-01' existing = {
  name: dnsZoneName
}

resource link 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = if (createVnetLink) {
  parent: zone
  name: 'vnet-${uniqueString(toLower(vnetResourceId))}'
  location: 'global'
  tags: tags
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: vnetResourceId
    }
  }
  dependsOn: [managedZone]
}

output resourceId string = zone.id

targetScope = 'resourceGroup'

@minLength(1)
param location string

@minLength(2)
@maxLength(6)
param prefix string

@minLength(2)
@maxLength(3)
param environmentName string

param tags object

@description('Repository inside the foundation ACR, without a registry hostname or tag.')
@minLength(1)
param imageRepository string

@description('Immutable registry digest sha256:<64 lowercase hexadecimal characters>, not a local image ID.')
@minLength(71)
@maxLength(71)
param imageDigest string

@minLength(36)
@maxLength(36)
param entraTenantId string

@minLength(36)
@maxLength(36)
param entraClientId string

@description('App-role token values; must match the values defined on the People API app registration (see infra/entra/New-PeopleApiEntraRegistration.ps1).')
@minLength(1)
param peopleReadRole string

@minLength(1)
param peopleAdminRole string

@description('Microsoft Graph app registration used for the client-credentials flow (GraphApi:ClientId/TenantId). Separate from AzureAd:* - this is the identity People API presents *to* Graph, not the identity it validates incoming callers against.')
@minLength(36)
@maxLength(36)
param graphApiClientId string

@minLength(36)
@maxLength(36)
param graphApiTenantId string

@description('Microsoft Graph app-only token-request scope: ["https://graph.microsoft.com/.default"]. Application permissions such as User.Read.All are granted separately in Entra ID.')
@minLength(1)
@maxLength(1)
@allowed(['https://graph.microsoft.com/.default'])
param graphApiScopes array

@description('Name of the owner-populated Key Vault secret holding the Graph app registration client secret. Leave empty (recommended) to use the managed identity instead - see docs/Operations-Notes.md.')
param graphApiClientSecretName string

@description('Allowed CORS origins for browser-based consumers; leave empty to keep CORS denied by default (server-to-server consumers are unaffected).')
param corsAllowedOrigins array

@description('Explicit supported CPU:memory pair for the Consumption profile.')
@allowed([
  '0.25:0.5Gi'
  '0.5:1Gi'
  '0.75:1.5Gi'
  '1:2Gi'
  '1.25:2.5Gi'
  '1.5:3Gi'
  '1.75:3.5Gi'
  '2:4Gi'
  '2.25:4.5Gi'
  '2.5:5Gi'
  '2.75:5.5Gi'
  '3:6Gi'
  '3.25:6.5Gi'
  '3.5:7Gi'
  '3.75:7.5Gi'
  '4:8Gi'
])
param containerSize string

@minValue(0)
@maxValue(300)
param minReplicas int

@minValue(1)
@maxValue(300)
param maxReplicas int

@minValue(1)
@maxValue(10000)
param concurrentRequests int

@minValue(1)
@maxValue(600)
param terminationGracePeriodSeconds int

var stem = '${prefix}-${environmentName}'
var globalStem = '${prefix}${environmentName}${uniqueString(resourceGroup().id, prefix, environmentName)}'
var hostName = '${stem}-app.${managedEnvironment.properties.defaultDomain}'
var size = split(containerSize, ':')
var hasGraphApiSecret = !empty(graphApiClientSecretName)

resource managedEnvironment 'Microsoft.App/managedEnvironments@2026-01-01' existing = {
  name: '${stem}-env'
}

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' existing = {
  name: '${stem}-id'
}

resource registry 'Microsoft.ContainerRegistry/registries@2025-04-01' existing = {
  name: 'acr${globalStem}'
}

resource vault 'Microsoft.KeyVault/vaults@2024-11-01' existing = {
  name: 'kv${globalStem}'
}

resource insights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: '${stem}-insights'
}

resource logs 'Microsoft.OperationalInsights/workspaces@2025-07-01' existing = {
  name: '${stem}-logs'
}

var baseSecrets = hasGraphApiSecret ? [
  {
    name: 'graphapi-client-secret'
    keyVaultUrl: '${vault.properties.vaultUri}secrets/${graphApiClientSecretName}'
    identity: identity.id
  }
] : []

var baseEnv = [
  { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
  { name: 'ASPNETCORE_HTTP_PORTS', value: '8080' }
  { name: 'AllowedHosts', value: hostName }
  { name: 'AzureAd__Instance', value: az.environment().authentication.loginEndpoint }
  { name: 'AzureAd__TenantId', value: entraTenantId }
  { name: 'AzureAd__ClientId', value: entraClientId }
  { name: 'Authorization__PeopleReadRole', value: peopleReadRole }
  { name: 'Authorization__PeopleAdminRole', value: peopleAdminRole }
  { name: 'GraphApi__ClientId', value: graphApiClientId }
  { name: 'GraphApi__TenantId', value: graphApiTenantId }
  { name: 'GraphApi__ManagedIdentityClientId', value: identity.properties.clientId }
  { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: insights.properties.ConnectionString }
  { name: 'Telemetry__ManagedIdentityClientId', value: identity.properties.clientId }
]

// App-only token requests use .default; application permissions are granted separately.
var scopesEnv = [for (scope, i) in graphApiScopes: {
  name: 'GraphApi__Scopes__${i}'
  value: scope
}]

var corsEnv = [for (origin, i) in corsAllowedOrigins: {
  name: 'Cors__AllowedOrigins__${i}'
  value: origin
}]

var graphSecretEnv = hasGraphApiSecret ? [
  { name: 'GraphApi__ClientSecret', secretRef: 'graphapi-client-secret' }
] : []

module application 'br/public:avm/res/app/container-app:0.23.0' = {
  name: '${stem}-activation'
  params: {
    name: '${stem}-app'
    location: location
    tags: tags
    enableTelemetry: false
    environmentResourceId: managedEnvironment.id
    workloadProfileName: 'Consumption'
    managedIdentities: {
      userAssignedResourceIds: [identity.id]
    }
    activeRevisionsMode: 'Single'
    // External to the app environment, but private to the internal environment's network.
    ingressExternal: true
    ingressAllowInsecure: false
    ingressTargetPort: 8080
    ingressTransport: 'http'
    terminationGracePeriodSeconds: terminationGracePeriodSeconds
    registries: [
      {
        server: registry.properties.loginServer
        identity: identity.id
      }
    ]
    secrets: baseSecrets
    containers: [
      {
        name: 'api'
        image: '${registry.properties.loginServer}/${imageRepository}@${imageDigest}'
        resources: {
          cpu: json(size[0])
          memory: size[1]
        }
        env: concat(baseEnv, scopesEnv, corsEnv, graphSecretEnv)
        probes: [
          {
            type: 'Liveness'
            httpGet: {
              path: '/health/live'
              port: 8080
              scheme: 'HTTP'
              httpHeaders: [{ name: 'Host', value: hostName }]
            }
            initialDelaySeconds: 10
            periodSeconds: 10
            timeoutSeconds: 3
            failureThreshold: 3
          }
          {
            type: 'Readiness'
            httpGet: {
              path: '/health/ready'
              port: 8080
              scheme: 'HTTP'
              httpHeaders: [{ name: 'Host', value: hostName }]
            }
            initialDelaySeconds: 10
            periodSeconds: 10
            timeoutSeconds: 5
            failureThreshold: 3
          }
        ]
      }
    ]
    scaleSettings: {
      minReplicas: minReplicas
      maxReplicas: maxReplicas
      rules: [
        {
          name: 'http'
          http: {
            metadata: {
              concurrentRequests: string(concurrentRequests)
            }
          }
        }
      ]
    }
    diagnosticSettings: [
      {
        name: 'platform'
        workspaceResourceId: logs.id
      }
    ]
  }
}

output applicationResourceId string = application.outputs.resourceId
output applicationOrigin string = 'https://${application.outputs.fqdn}'

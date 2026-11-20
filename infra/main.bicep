// One environment (dev | test | prod) of the StoreOps platform.
//
//   az deployment sub create --location canadacentral \
//     --template-file infra/main.bicep --parameters infra/parameters/dev.bicepparam
//
// Prerequisite: infra/shared.bicep (container registry) and the image tag pushed to it.
targetScope = 'subscription'

@allowed([
  'dev'
  'test'
  'prod'
])
param environmentName string

@description('Short, lowercase, alphanumeric application name used in resource names.')
@minLength(3)
@maxLength(12)
param appName string = 'storeops'

param location string = deployment().location

@description('Image tag to deploy (git SHA). The same tag is promoted dev -> test -> prod.')
param imageTag string

@description('Name of the shared container registry (output of shared.bicep).')
param registryName string

@description('Client ID of this environment\'s Entra app registration (scripts/entra/create-app-registration.sh).')
param entraClientId string

@description('Object ID and display name of the deployment identity (GitHub Actions). It becomes a PostgreSQL Entra admin so the pipeline can grant the app identity its database role.')
param deployerObjectId string
param deployerName string

@description('Optional Entra group of human DBAs, added as PostgreSQL Entra admin (break-glass, troubleshooting).')
param dbaGroupObjectId string = ''
param dbaGroupName string = ''

@description('Key Vault secret names exposed to the apps (create them in the vault first).')
param keyVaultSecretNames array = []

param minReplicas int = 0
param enablePostgresHighAvailability bool = false

var isProd = environmentName == 'prod'
var suffix = uniqueString(subscription().id, appName, environmentName)
var baseName = '${appName}-${environmentName}'
var tags = {
  app: appName
  environment: environmentName
  managedBy: 'bicep'
}

var postgresAdmins = concat(
  [
    {
      objectId: deployerObjectId
      name: deployerName
      type: 'ServicePrincipal'
    }
  ],
  empty(dbaGroupObjectId)
    ? []
    : [
        {
          objectId: dbaGroupObjectId
          name: dbaGroupName
          type: 'Group'
        }
      ]
)

resource rg 'Microsoft.Resources/resourceGroups@2025-04-01' = {
  name: 'rg-${baseName}'
  location: location
  tags: tags
}

module identity 'modules/identity.bicep' = {
  scope: rg
  name: 'identity'
  params: {
    name: 'id-${baseName}'
    location: location
    tags: tags
  }
}

module monitoring 'modules/monitoring.bicep' = {
  scope: rg
  name: 'monitoring'
  params: {
    logAnalyticsName: 'log-${baseName}'
    appInsightsName: 'appi-${baseName}'
    location: location
    tags: tags
    retentionInDays: isProd ? 90 : 30
  }
}

module storage 'modules/storage.bicep' = {
  scope: rg
  name: 'storage'
  params: {
    name: take('st${appName}${environmentName}${suffix}', 24)
    location: location
    tags: tags
    isProd: isProd
    principalId: identity.outputs.principalId
  }
}

module keyVault 'modules/keyvault.bicep' = {
  scope: rg
  name: 'keyvault'
  params: {
    name: take('kv-${baseName}-${suffix}', 24)
    location: location
    tags: tags
    isProd: isProd
    principalId: identity.outputs.principalId
  }
}

module postgres 'modules/postgres.bicep' = {
  scope: rg
  name: 'postgres'
  params: {
    name: 'psql-${baseName}-${suffix}'
    location: location
    tags: tags
    databaseName: appName
    isProd: isProd
    enableHighAvailability: enablePostgresHighAvailability
    administrators: postgresAdmins
  }
}

// AcrPull on the shared registry (another resource group).
module registryPull 'modules/registry-pull.bicep' = {
  scope: resourceGroup('rg-${appName}-shared')
  name: 'registry-pull-${environmentName}'
  params: {
    registryName: registryName
    principalId: identity.outputs.principalId
  }
}

module apps 'modules/container-apps.bicep' = {
  scope: rg
  name: 'container-apps'
  params: {
    location: location
    tags: tags
    environmentName: 'cae-${baseName}'
    webAppName: 'ca-${baseName}'
    migrateJobName: 'caj-${baseName}-migrate'
    cleanupJobName: 'caj-${baseName}-cleanup'
    logAnalyticsName: monitoring.outputs.logAnalyticsName
    appInsightsName: monitoring.outputs.appInsightsName
    identityId: identity.outputs.id
    identityClientId: identity.outputs.clientId
    identityName: identity.outputs.name
    registryServer: '${registryName}.azurecr.io'
    webImage: '${registryName}.azurecr.io/${appName}-web:${imageTag}'
    jobsImage: '${registryName}.azurecr.io/${appName}-jobs:${imageTag}'
    appVersion: imageTag
    postgresHost: postgres.outputs.host
    databaseName: postgres.outputs.databaseName
    blobEndpoint: storage.outputs.blobEndpoint
    dataProtectionKeyUri: keyVault.outputs.dataProtectionKeyUri
    keyVaultUri: keyVault.outputs.uri
    keyVaultSecretNames: keyVaultSecretNames
    entraTenantId: tenant().tenantId
    entraClientId: entraClientId
    minReplicas: minReplicas
  }
  dependsOn: [
    // The identity must be able to pull images before the first revision starts.
    registryPull
  ]
}

output resourceGroupName string = rg.name
output identityName string = identity.outputs.name
output identityPrincipalId string = identity.outputs.principalId
output postgresServerName string = postgres.outputs.name
output postgresDatabaseName string = postgres.outputs.databaseName
output webAppName string = apps.outputs.webAppName
output webFqdn string = apps.outputs.webFqdn
output migrateJobName string = apps.outputs.migrateJobName
output keyVaultName string = keyVault.outputs.name

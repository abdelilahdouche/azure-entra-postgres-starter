// Container Apps environment + web app + jobs (migrations, scheduled cleanup).
// Everything runs as one user-assigned managed identity: image pull, PostgreSQL, Blob, Key Vault,
// and the Entra sign-in client credential (federated, no secret).
param location string
param tags object

param environmentName string
param webAppName string
param migrateJobName string
param cleanupJobName string

param logAnalyticsName string
param appInsightsName string

param identityId string
param identityClientId string
@description('PostgreSQL role mapped to the managed identity (= identity name).')
param identityName string

param registryServer string
param webImage string
param jobsImage string
param appVersion string

param postgresHost string
param databaseName string
param blobEndpoint string
param dataProtectionKeyUri string
param keyVaultUri string

@description('Key Vault secret names exposed to the apps as configuration "Secrets:<name>" (optional).')
param keyVaultSecretNames array = []

param entraTenantId string
param entraClientId string

param minReplicas int = 0
param maxReplicas int = 3
@description('Cron (UTC) for the retention cleanup job.')
param cleanupCron string = '0 7 * * *'

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2025-02-01' existing = {
  name: logAnalyticsName
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: appInsightsName
}

resource environment 'Microsoft.App/managedEnvironments@2025-07-01' = {
  name: environmentName
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
    ]
  }
}

// ---- Shared configuration ----------------------------------------------------------------------

var registries = [
  {
    server: registryServer
    identity: identityId
  }
]

// Container Apps secrets: either a value (App Insights connection string, not a credential)
// or a reference to a Key Vault secret, resolved with the managed identity.
var secrets = concat(
  [
    {
      name: 'appinsights-connection-string'
      value: appInsights.properties.ConnectionString
    }
  ],
  map(keyVaultSecretNames, s => {
    name: toLower(s)
    keyVaultUrl: '${keyVaultUri}secrets/${s}'
    identity: identityId
  })
)

var commonEnv = concat(
  [
    { name: 'AZURE_CLIENT_ID', value: identityClientId }
    { name: 'APP_VERSION', value: appVersion }
    { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', secretRef: 'appinsights-connection-string' }
    { name: 'Postgres__Host', value: postgresHost }
    { name: 'Postgres__Database', value: databaseName }
    { name: 'Postgres__Username', value: identityName }
    { name: 'Postgres__UseEntraAuth', value: 'true' }
    { name: 'Storage__BlobEndpoint', value: blobEndpoint }
    { name: 'Storage__UploadsContainer', value: 'uploads' }
  ],
  map(keyVaultSecretNames, s => { name: 'Secrets__${s}', secretRef: toLower(s) })
)

var webEnv = [
  { name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED', value: 'true' }
  { name: 'AzureAd__TenantId', value: entraTenantId }
  { name: 'AzureAd__ClientId', value: entraClientId }
  // Sign-in client credential = a token of the managed identity, trusted by the app registration
  // through a federated identity credential (scripts/entra/register-environment.sh). No client secret.
  { name: 'AzureAd__ClientCredentials__0__SourceType', value: 'SignedAssertionFromManagedIdentity' }
  { name: 'AzureAd__ClientCredentials__0__ManagedIdentityClientId', value: identityClientId }
  { name: 'DataProtection__BlobUri', value: '${blobEndpoint}dataprotection/keys.xml' }
  { name: 'DataProtection__KeyUri', value: dataProtectionKeyUri }
]

// ---- Web app -----------------------------------------------------------------------------------

resource web 'Microsoft.App/containerApps@2025-07-01' = {
  name: webAppName
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identityId}': {}
    }
  }
  properties: {
    environmentId: environment.id
    workloadProfileName: 'Consumption'
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
      registries: registries
      secrets: secrets
    }
    template: {
      containers: [
        {
          name: 'web'
          image: webImage
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: concat(commonEnv, webEnv)
          probes: [
            // Probes do not depend on the database: a DB outage must not restart healthy containers.
            {
              type: 'Liveness'
              httpGet: {
                path: '/healthz/live'
                port: 8080
              }
              periodSeconds: 30
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/healthz/live'
                port: 8080
              }
              periodSeconds: 10
            }
          ]
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
        rules: [
          {
            name: 'http'
            http: {
              metadata: {
                concurrentRequests: '50'
              }
            }
          }
        ]
      }
    }
  }
}

// ---- Jobs (same identity, same configuration, different command) -----------------------------

resource migrateJob 'Microsoft.App/jobs@2025-07-01' = {
  name: migrateJobName
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identityId}': {}
    }
  }
  properties: {
    environmentId: environment.id
    workloadProfileName: 'Consumption'
    configuration: {
      triggerType: 'Manual'
      replicaTimeout: 900
      replicaRetryLimit: 0
      manualTriggerConfig: {
        parallelism: 1
        replicaCompletionCount: 1
      }
      registries: registries
      secrets: secrets
    }
    template: {
      containers: [
        {
          name: 'migrate'
          image: jobsImage
          args: [
            'migrate'
          ]
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: commonEnv
        }
      ]
    }
  }
}

resource cleanupJob 'Microsoft.App/jobs@2025-07-01' = {
  name: cleanupJobName
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identityId}': {}
    }
  }
  properties: {
    environmentId: environment.id
    workloadProfileName: 'Consumption'
    configuration: {
      triggerType: 'Schedule'
      replicaTimeout: 1800
      replicaRetryLimit: 1
      scheduleTriggerConfig: {
        cronExpression: cleanupCron
        parallelism: 1
        replicaCompletionCount: 1
      }
      registries: registries
      secrets: secrets
    }
    template: {
      containers: [
        {
          name: 'cleanup'
          image: jobsImage
          args: [
            'cleanup'
          ]
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: commonEnv
        }
      ]
    }
  }
}

output webAppName string = web.name
output webFqdn string = web.properties.configuration.ingress.fqdn
output migrateJobName string = migrateJob.name
output cleanupJobName string = cleanupJob.name

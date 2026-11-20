using '../main.bicep'

// Values that differ per environment live here. Pipeline-provided values are read from
// environment variables (set by .github/workflows/deploy-environment.yml).
param environmentName = 'test'
param location = 'canadacentral'
param minReplicas = 0
param enablePostgresHighAvailability = false

param imageTag = readEnvironmentVariable('IMAGE_TAG', 'latest')
param registryName = readEnvironmentVariable('REGISTRY_NAME', '')
param entraClientId = readEnvironmentVariable('ENTRA_CLIENT_ID', '')
param deployerObjectId = readEnvironmentVariable('DEPLOYER_OBJECT_ID', '')
param deployerName = readEnvironmentVariable('DEPLOYER_NAME', '')
param dbaGroupObjectId = readEnvironmentVariable('DBA_GROUP_OBJECT_ID', '')
param dbaGroupName = readEnvironmentVariable('DBA_GROUP_NAME', '')

// Example: param keyVaultSecretNames = ['smtp-password']
param keyVaultSecretNames = []

// Shared resources used by every environment: the container registry.
// Images are built once and promoted dev -> test -> prod by tag (same artifact everywhere).
targetScope = 'subscription'

@description('Short, lowercase, alphanumeric application name used in resource names.')
@minLength(3)
@maxLength(12)
param appName string = 'storeops'

@description('Azure region.')
param location string = deployment().location

var tags = {
  app: appName
  environment: 'shared'
  managedBy: 'bicep'
}

resource rg 'Microsoft.Resources/resourceGroups@2025-04-01' = {
  name: 'rg-${appName}-shared'
  location: location
  tags: tags
}

module registry 'modules/registry.bicep' = {
  scope: rg
  name: 'registry'
  params: {
    name: 'cr${appName}${uniqueString(rg.id)}'
    location: location
    tags: tags
  }
}

output resourceGroupName string = rg.name
output registryName string = registry.outputs.name
output registryLoginServer string = registry.outputs.loginServer

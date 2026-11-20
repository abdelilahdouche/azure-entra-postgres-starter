@description('Registry name (globally unique, alphanumeric).')
param name string
param location string
param tags object

resource registry 'Microsoft.ContainerRegistry/registries@2025-04-01' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: 'Basic'
  }
  properties: {
    // Pulls use managed identities (AcrPull), pushes use the pipeline's Entra identity.
    adminUserEnabled: false
  }
}

output name string = registry.name
output loginServer string = registry.properties.loginServer

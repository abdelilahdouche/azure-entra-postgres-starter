// User-assigned managed identity shared by the web app and the jobs of one environment.
// Its name is also the PostgreSQL role name (see scripts/db/grant-db-access.sh).
param name string
param location string
param tags object

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: name
  location: location
  tags: tags
}

output id string = identity.id
output name string = identity.name
output principalId string = identity.properties.principalId
output clientId string = identity.properties.clientId

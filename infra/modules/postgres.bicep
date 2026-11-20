// Azure Database for PostgreSQL Flexible Server with Microsoft Entra authentication ONLY.
// Password authentication is disabled: there is no admin password to store, rotate or leak.
param name string
param location string
param tags object
param databaseName string
param isProd bool
param enableHighAvailability bool = false

@description('Entra administrators: [{ objectId, name, type: User | Group | ServicePrincipal }]. Include the deployment identity so the pipeline can create the app role.')
param administrators array

resource server 'Microsoft.DBforPostgreSQL/flexibleServers@2025-08-01' = {
  name: name
  location: location
  tags: tags
  sku: isProd
    ? {
        name: 'Standard_D2ds_v5'
        tier: 'GeneralPurpose'
      }
    : {
        name: 'Standard_B1ms'
        tier: 'Burstable'
      }
  properties: {
    version: '16'
    authConfig: {
      activeDirectoryAuth: 'Enabled'
      passwordAuth: 'Disabled'
      tenantId: tenant().tenantId
    }
    storage: {
      storageSizeGB: 32
      autoGrow: 'Enabled'
    }
    backup: {
      backupRetentionDays: isProd ? 35 : 7
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: {
      mode: enableHighAvailability ? 'ZoneRedundant' : 'Disabled'
    }
    network: {
      // Demo setting. For production, prefer VNet integration / private endpoint (see docs/architecture.md).
      publicNetworkAccess: 'Enabled'
    }
  }
}

// The server accepts one administration operation at a time: create admins sequentially.
@batchSize(1)
resource entraAdmins 'Microsoft.DBforPostgreSQL/flexibleServers/administrators@2025-08-01' = [
  for admin in administrators: {
    parent: server
    name: admin.objectId
    properties: {
      principalType: admin.type
      principalName: admin.name
      tenantId: tenant().tenantId
    }
  }
]

// Lets Azure-hosted services (Container Apps) reach the server. Access still requires a valid Entra token.
resource allowAzureServices 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2025-08-01' = {
  parent: server
  name: 'AllowAllAzureServicesAndResourcesWithinAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
  dependsOn: [
    entraAdmins
  ]
}

resource database 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2025-08-01' = {
  parent: server
  name: databaseName
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
  dependsOn: [
    allowAzureServices
  ]
}

output name string = server.name
output host string = server.properties.fullyQualifiedDomainName
output databaseName string = database.name

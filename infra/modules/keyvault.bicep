// Key Vault (RBAC mode) holding:
//  - the RSA key that wraps ASP.NET Core data-protection keys (auth cookies survive restarts/scale-out)
//  - any third-party secret the apps need (SMTP, partner API keys...), referenced by Container Apps
param name string
param location string
param tags object
param isProd bool

@description('Principal ID of the managed identity that reads secrets and uses the wrapping key.')
param principalId string

var secretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6' // Key Vault Secrets User
var cryptoEncryptionUserRoleId = 'e147488a-f6f5-4113-8e2d-b22465e65bf6' // Key Vault Crypto Service Encryption User

resource vault 'Microsoft.KeyVault/vaults@2025-05-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    tenantId: tenant().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: isProd ? 90 : 7
    enablePurgeProtection: isProd ? true : null
  }
}

// ARM creates the first version only; later deployments leave the existing key untouched.
resource dataProtectionKey 'Microsoft.KeyVault/vaults/keys@2025-05-01' = {
  parent: vault
  name: 'dataprotection'
  properties: {
    kty: 'RSA'
    keySize: 2048
    keyOps: [
      'wrapKey'
      'unwrapKey'
    ]
  }
}

resource secretsUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, principalId, secretsUserRoleId)
  scope: vault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', secretsUserRoleId)
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}

resource cryptoUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, principalId, cryptoEncryptionUserRoleId)
  scope: vault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', cryptoEncryptionUserRoleId)
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}

output name string = vault.name
output uri string = vault.properties.vaultUri
output dataProtectionKeyUri string = dataProtectionKey.properties.keyUri

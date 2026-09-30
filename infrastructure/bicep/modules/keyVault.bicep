@description('Key Vault name (globally unique).')
param name string

@description('Azure region.')
param location string

@description('Resource tags.')
param tags object = {}

@description('Enable purge protection (recommended for production).')
param enablePurgeProtection bool = true

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
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
    softDeleteRetentionInDays: 90
    enablePurgeProtection: enablePurgeProtection
    publicNetworkAccess: 'Enabled'
  }
}

output id string = keyVault.id

output name string = keyVault.name

output uri string = keyVault.properties.vaultUri

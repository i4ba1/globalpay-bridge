@description('Key Vault name that will hold the secret.')
param keyVaultName string

@description('Secret name.')
param secretName string

@secure()
@description('Secret value.')
param secretValue string

resource secret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  name: '${keyVaultName}/${secretName}'
  properties: {
    value: secretValue
    contentType: 'text/plain'
  }
}

output secretUri string = secret.properties.secretUri

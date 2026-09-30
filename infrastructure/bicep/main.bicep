targetScope = 'resourceGroup'

@description('Environment name (dev, test or prod).')
@allowed([
  'dev'
  'test'
  'prod'
])
param environment string

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Base name used for resource naming.')
@minLength(3)
@maxLength(20)
param baseName string = 'globalpay'

@description('SQL administrator login name.')
param sqlAdminLogin string

@secure()
@description('SQL administrator password.')
@minLength(12)
param sqlAdminPassword string

@description('OIDC authority used by the API (Entra ID issuer).')
param oidcAuthority string

@description('Token audience expected by the API.')
param oidcAudience string = 'globalpay-api'

@description('Payment provider base address.')
param paymentProviderBaseAddress string

@description('Key Vault Secrets User role definition id.')
param keyVaultSecretsUserRoleId string = '4633458b-17de-408a-b874-0445c86b69e6'

@description('Resource tags.')
param tags object = {
  application: 'globalpay-bridge'
  environment: environment
}

var nameSuffix = '${baseName}-${environment}'
var isProduction = environment == 'prod'

module logAnalytics './modules/logAnalytics.bicep' = {
  name: 'logAnalytics'
  params: {
    name: '${nameSuffix}-law'
    location: location
    tags: tags
  }
}

module appInsights './modules/appInsights.bicep' = {
  name: 'appInsights'
  params: {
    name: '${nameSuffix}-ai'
    location: location
    logAnalyticsWorkspaceId: logAnalytics.outputs.id
    tags: tags
  }
}

module storage './modules/storage.bicep' = {
  name: 'storage'
  params: {
    name: '${replace(nameSuffix, '-', '')}sa'
    location: location
    tags: tags
  }
}

module redis './modules/redis.bicep' = {
  name: 'redis'
  params: {
    name: '${nameSuffix}-redis'
    location: location
    tags: tags
  }
}

module keyVault './modules/keyVault.bicep' = {
  name: 'keyVault'
  params: {
    name: '${nameSuffix}-kv'
    location: location
    enablePurgeProtection: isProduction
    tags: tags
  }
}

module sql './modules/sqlServer.bicep' = {
  name: 'sql'
  params: {
    name: '${nameSuffix}-sql'
    location: location
    administratorLogin: sqlAdminLogin
    administratorPassword: sqlAdminPassword
    databaseName: 'GlobalPayBridge'
    tags: tags
  }
}

module sqlConnectionSecret './modules/keyVaultSecret.bicep' = {
  name: 'sqlConnectionSecret'
  params: {
    keyVaultName: keyVault.outputs.name
    secretName: 'SqlConnectionString'
    secretValue: 'Server=tcp:${sql.outputs.fqdn},1433;Database=${sql.outputs.databaseName};User Id=${sqlAdminLogin};Password=${sqlAdminPassword};Encrypt=True;TrustServerCertificate=False;'
  }
}

module blobConnectionSecret './modules/keyVaultSecret.bicep' = {
  name: 'blobConnectionSecret'
  params: {
    keyVaultName: keyVault.outputs.name
    secretName: 'BlobConnectionString'
    secretValue: storage.outputs.blobConnectionString
  }
}

module plan './modules/appServicePlan.bicep' = {
  name: 'plan'
  params: {
    name: '${nameSuffix}-plan'
    location: location
    skuName: isProduction ? 'P1v3' : 'B1'
    capacity: isProduction ? 2 : 1
    tags: tags
  }
}

module api './modules/webApp.bicep' = {
  name: 'api'
  params: {
    name: '${nameSuffix}-api'
    location: location
    planId: plan.outputs.id
    applicationInsightsConnectionString: appInsights.outputs.connectionString
    keyVaultName: keyVault.outputs.name
    appSettings: {
      ASPNETCORE_ENVIRONMENT: isProduction ? 'Production' : 'Development'
      Authentication__Authority: oidcAuthority
      Authentication__Audience: oidcAudience
      Payments__Provider__BaseAddress: paymentProviderBaseAddress
      Blobs__ServiceUri: storage.outputs.blobServiceUri
      ConnectionStrings__Redis: '${redis.outputs.name}.redis.cache.windows.net:${redis.outputs.sslPort},ssl=True,abortConnect=False'
      ConnectionStrings__GlobalPay: '@Microsoft.KeyVault(SecretUri=${sqlConnectionSecret.outputs.secretUri})'
      ConnectionStrings__Blobs: '@Microsoft.KeyVault(SecretUri=${blobConnectionSecret.outputs.secretUri})'
    }
    tags: tags
  }
}

module functions './modules/functionApp.bicep' = {
  name: 'functions'
  params: {
    name: '${nameSuffix}-func'
    location: location
    applicationInsightsConnectionString: appInsights.outputs.connectionString
    keyVaultName: keyVault.outputs.name
    appSettings: {
      Blobs: '@Microsoft.KeyVault(SecretUri=${blobConnectionSecret.outputs.secretUri})'
      ConnectionStrings__Redis: '${redis.outputs.name}.redis.cache.windows.net:${redis.outputs.sslPort},ssl=True,abortConnect=False'
      ConnectionStrings__GlobalPay: '@Microsoft.KeyVault(SecretUri=${sqlConnectionSecret.outputs.secretUri})'
    }
    tags: tags
  }
}

module apiKeyVaultAccess './modules/roleAssignment.bicep' = {
  name: 'apiKeyVaultAccess'
  params: {
    principalId: api.outputs.principalId
    roleDefinitionGuid: keyVaultSecretsUserRoleId
  }
}

module functionsKeyVaultAccess './modules/roleAssignment.bicep' = {
  name: 'functionsKeyVaultAccess'
  params: {
    principalId: functions.outputs.principalId
    roleDefinitionGuid: keyVaultSecretsUserRoleId
  }
}

output apiUrl string = api.outputs.url

output apiName string = api.outputs.name

output functionAppName string = functions.outputs.name

output sqlServerFqdn string = sql.outputs.fqdn

output keyVaultName string = keyVault.outputs.name

output applicationInsightsName string = appInsights.outputs.name

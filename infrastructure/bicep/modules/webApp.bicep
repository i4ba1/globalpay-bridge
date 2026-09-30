@description('Web app name (globally unique).')
param name string

@description('Azure region.')
param location string

@description('App Service plan resource id.')
param planId string

@description('Application Insights connection string.')
param applicationInsightsConnectionString string

@description('Key Vault name used for secret references.')
param keyVaultName string

@description('Application settings.')
param appSettings object = {}

@description('Resource tags.')
param tags object = {}

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: name
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: planId
    httpsOnly: true
    clientAffinityEnabled: false
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      alwaysOn: true
      http20Enabled: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: [
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: applicationInsightsConnectionString
        }
        {
          name: 'ApplicationInsights__ConnectionString'
          value: applicationInsightsConnectionString
        }
        for setting in items(appSettings): {
          name: setting.key
          value: setting.value
        }
      ]
    }
  }
}

output id string = webApp.id

output name string = webApp.name

output url string = 'https://${webApp.properties.defaultHostName}'

output principalId string = webApp.identity.principalId

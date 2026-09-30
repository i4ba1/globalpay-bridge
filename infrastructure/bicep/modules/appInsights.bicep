@description('Application Insights name.')
param name string

@description('Azure region.')
param location string

@description('Log Analytics workspace resource id.')
param logAnalyticsWorkspaceId string

@description('Resource tags.')
param tags object = {}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: name
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspaceId
    IngestionMode: 'LogAnalytics'
    DisableIpMasking: true
  }
}

output id string = appInsights.id

output name string = appInsights.name

output connectionString string = appInsights.properties.ConnectionString

@description('App Service plan name.')
param name string

@description('Azure region.')
param location string

@description('SKU name.')
param skuName string = 'B1'

@description('Instance count.')
param capacity int = 1

@description('Resource tags.')
param tags object = {}

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: skuName
    capacity: capacity
  }
  kind: 'linux'
  properties: {
    reserved: true
    zoneRedundant: false
  }
}

output id string = plan.id

output name string = plan.name

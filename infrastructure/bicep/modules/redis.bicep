@description('Azure Cache for Redis name.')
param name string

@description('Azure region.')
param location string

@description('Resource tags.')
param tags object = {}

resource redis 'Microsoft.Cache/redis@2024-03-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'Basic'
      family: 'C'
      capacity: 0
    }
    enableNonSslPort: false
    minimumTlsVersion: '1.2'
    redisConfiguration: {
      'maxmemory-policy': 'allkeys-lru'
    }
  }
}

output id string = redis.id

output name string = redis.name

output hostName string = redis.properties.hostName

output sslPort int = redis.properties.sslPort

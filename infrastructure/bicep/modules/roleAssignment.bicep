@description('Principal (managed identity object id) receiving the role.')
param principalId string

@description('Role definition GUID, for example 4633458b-17de-408a-b874-0445c86b69e6 for Key Vault Secrets User.')
param roleDefinitionGuid string

@description('Principal type.')
@allowed([
  'ServicePrincipal'
  'User'
  'Group'
])
param principalType string = 'ServicePrincipal'

resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(principalId, roleDefinitionGuid, resourceGroup().id)
  properties: {
    principalId: principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleDefinitionGuid)
    principalType: principalType
  }
}

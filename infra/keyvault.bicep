targetScope = 'resourceGroup'

@description('Globally unique Key Vault name.')
param vaultName string

@description('Object ID of the deployment operator, used to create the initial secret.')
param operatorObjectId string

@secure()
@description('OBO application client secret. This is never emitted as a deployment output.')
param oboClientSecret string

resource vault 'Microsoft.KeyVault/vaults@2024-11-01' = {
  name: vaultName
  location: resourceGroup().location
  tags: {
    purpose: 'demo'
    owner: 'gbelenky'
    workload: 'foundry-graph-obo-agent'
  }
  properties: {
    enableRbacAuthorization: true
    enablePurgeProtection: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    publicNetworkAccess: 'Enabled'
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
  }
}

resource operatorSecretsOfficer 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, operatorObjectId, 'key-vault-secrets-officer')
  scope: vault
  properties: {
    principalId: operatorObjectId
    principalType: 'User'
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      'b86a8fe4-44ce-4948-aee5-eccb2c155cd7')
  }
}

resource oboClientSecretResource 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = {
  parent: vault
  name: 'APP-OBO-CLIENT-SECRET'
  properties: {
    value: oboClientSecret
    attributes: {
      enabled: true
    }
  }
}

output vaultName string = vault.name
output vaultUri string = vault.properties.vaultUri

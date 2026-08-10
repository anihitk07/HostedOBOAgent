param tags object
param functionAppName string
param staticWebAppName string
param functionApiClientId string
param spaClientId string
param foundryAccountName string
param relayIdentityName string

resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  name: staticWebAppName
  location: 'westeurope'
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  tags: union(tags, {
    'azd-service-name': 'web'
  })
  properties: {
    allowConfigFileUpdates: true
  }
}

resource functionApp 'Microsoft.Web/sites@2024-04-01' existing = {
  name: functionAppName
}

resource functionAppAuth 'Microsoft.Web/sites/config@2024-04-01' = {
  parent: functionApp
  name: 'authsettingsV2'
  properties: {
    platform: {
      enabled: true
      runtimeVersion: '~1'
    }
    globalValidation: {
      requireAuthentication: true
      unauthenticatedClientAction: 'Return401'
    }
    identityProviders: {
      azureActiveDirectory: {
        enabled: true
        registration: {
          clientId: functionApiClientId
          openIdIssuer: '${environment().authentication.loginEndpoint}${tenant().tenantId}/v2.0'
        }
        validation: {
          allowedAudiences: [
            'api://${functionApiClientId}'
          ]
          defaultAuthorizationPolicy: {
            allowedApplications: [
              spaClientId
            ]
          }
        }
      }
    }
    login: {
      tokenStore: {
        enabled: false
      }
    }
    httpSettings: {
      requireHttps: true
    }
  }
}

resource functionCors 'Microsoft.Web/sites/config@2024-04-01' = {
  parent: functionApp
  name: 'web'
  properties: {
    cors: {
      allowedOrigins: [
        'https://${staticWebApp.properties.defaultHostname}'
      ]
      supportCredentials: false
    }
  }
}

resource foundryAccount 'Microsoft.CognitiveServices/accounts@2024-10-01' existing = {
  name: foundryAccountName
}

resource relayIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = {
  name: relayIdentityName
}

resource foundryAgentConsumerAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(foundryAccount.id, relayIdentity.id, 'foundry-agent-consumer')
  scope: foundryAccount
  properties: {
    principalId: relayIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      'eed3b665-ab3a-47b6-8f48-c9382fb1dad6')
  }
}

output staticWebAppName string = staticWebApp.name
output staticWebAppUrl string = 'https://${staticWebApp.properties.defaultHostname}'

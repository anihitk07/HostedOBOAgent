@description('Specifies the name of the virtual network.')
param vNetName string

@description('Specifies the name of the subnet for Function App virtual network integration.')
param appSubnetName string = 'snet-function-relay'

@description('Specifies the address prefix for the Function App integration subnet.')
param appSubnetAddressPrefix string = '10.250.3.0/24'

resource virtualNetwork 'Microsoft.Network/virtualNetworks@2024-05-01' existing = {
  name: vNetName
}

resource appSubnet 'Microsoft.Network/virtualNetworks/subnets@2024-05-01' = {
  parent: virtualNetwork
  name: appSubnetName
  properties: {
    addressPrefix: appSubnetAddressPrefix
    delegations: [
      {
        name: 'function-flex-consumption'
        properties: {
          serviceName: 'Microsoft.App/environments'
        }
      }
    ]
    privateEndpointNetworkPolicies: 'Disabled'
    privateLinkServiceNetworkPolicies: 'Enabled'
  }
}

output appSubnetName string = appSubnetName
output appSubnetID string = appSubnet.id

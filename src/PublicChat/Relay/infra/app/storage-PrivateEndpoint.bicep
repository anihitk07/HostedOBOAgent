param virtualNetworkName string
param subnetName string
@description('Specifies the storage account resource name')
param resourceName string
param location string = resourceGroup().location
param tags object = {}
param enableBlob bool = true
param enableQueue bool = false
param enableTable bool = false

resource vnet 'Microsoft.Network/virtualNetworks@2021-08-01' existing = {
  name: virtualNetworkName
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2021-09-01' existing = {
  name: resourceName
}

// Storage DNS zone names
var blobPrivateDNSZoneName = 'privatelink.blob.${environment().suffixes.storage}'

resource blobPrivateDnsZone 'Microsoft.Network/privateDnsZones@2024-06-01' existing = {
  name: blobPrivateDNSZoneName
}

// AVM module for Blob Private Endpoint with private DNS zone
module blobPrivateEndpoint 'br/public:avm/res/network/private-endpoint:0.11.0' = if (enableBlob) {
  name: 'blob-private-endpoint-deployment'
  params: {
    name: 'blob-private-endpoint'
    location: location
    tags: tags
    subnetResourceId: '${vnet.id}/subnets/${subnetName}'
    privateLinkServiceConnections: [
      {
        name: 'blobPrivateLinkConnection'
        properties: {
          privateLinkServiceId: storageAccount.id
          groupIds: [
            'blob'
          ]
        }
      }
    ]
    customDnsConfigs: []
    privateDnsZoneGroup: {
      name: 'blobPrivateDnsZoneGroup'
      privateDnsZoneGroupConfigs: [
        {
          name: 'storageBlobARecord'
          privateDnsZoneResourceId: blobPrivateDnsZone.id
        }
      ]
    }
  }
}

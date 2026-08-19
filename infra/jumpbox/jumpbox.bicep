// ponytail: minimal jumpbox + Bastion so azd/az/dotnet commands can reach the
// private Foundry project and Key Vault once publicNetworkAccess=Disabled.
// Not part of the upstream template — added because template 15 assumes the
// operator already has private connectivity.
targetScope = 'resourceGroup'

param location string
param vnetName string
@description('Existing VNet must have room for two new /26 or larger subnets.')
param bastionSubnetPrefix string = '192.168.2.0/26'
param jumpboxSubnetPrefix string = '192.168.2.64/26'
param jumpboxVmName string = 'vm-hostedobo-jump'
param adminUsername string = 'azureadmin'
@secure()
param adminPassword string

resource vnet 'Microsoft.Network/virtualNetworks@2024-05-01' existing = {
  name: vnetName
}

resource bastionSubnet 'Microsoft.Network/virtualNetworks/subnets@2024-05-01' = {
  parent: vnet
  name: 'AzureBastionSubnet'
  properties: {
    addressPrefix: bastionSubnetPrefix
  }
}

resource jumpboxSubnet 'Microsoft.Network/virtualNetworks/subnets@2024-05-01' = {
  parent: vnet
  name: 'snet-jumpbox'
  properties: {
    addressPrefix: jumpboxSubnetPrefix
  }
  dependsOn: [
    bastionSubnet
  ]
}

resource bastionPip 'Microsoft.Network/publicIPAddresses@2024-05-01' = {
  name: 'pip-bastion-hostedobo'
  location: location
  sku: {
    name: 'Standard'
  }
  properties: {
    publicIPAllocationMethod: 'Static'
  }
}

resource bastion 'Microsoft.Network/bastionHosts@2024-05-01' = {
  name: 'bas-hostedobo-wus3'
  location: location
  sku: {
    name: 'Standard'
  }
  properties: {
    ipConfigurations: [
      {
        name: 'ipconfig'
        properties: {
          subnet: {
            id: bastionSubnet.id
          }
          publicIPAddress: {
            id: bastionPip.id
          }
        }
      }
    ]
  }
}

resource jumpboxNic 'Microsoft.Network/networkInterfaces@2024-05-01' = {
  name: '${jumpboxVmName}-nic'
  location: location
  properties: {
    ipConfigurations: [
      {
        name: 'ipconfig1'
        properties: {
          subnet: {
            id: jumpboxSubnet.id
          }
          privateIPAllocationMethod: 'Dynamic'
        }
      }
    ]
  }
}

resource jumpboxVm 'Microsoft.Compute/virtualMachines@2024-07-01' = {
  name: jumpboxVmName
  location: location
  properties: {
    hardwareProfile: {
      vmSize: 'Standard_D2s_v5'
    }
    osProfile: {
      computerName: jumpboxVmName
      adminUsername: adminUsername
      adminPassword: adminPassword
    }
    storageProfile: {
      imageReference: {
        publisher: 'MicrosoftWindowsDesktop'
        offer: 'windows-11'
        sku: 'win11-23h2-pro'
        version: 'latest'
      }
      osDisk: {
        createOption: 'FromImage'
        managedDisk: {
          storageAccountType: 'Premium_LRS'
        }
      }
    }
    networkProfile: {
      networkInterfaces: [
        {
          id: jumpboxNic.id
        }
      ]
    }
  }
}

output jumpboxVmName string = jumpboxVm.name
output bastionName string = bastion.name

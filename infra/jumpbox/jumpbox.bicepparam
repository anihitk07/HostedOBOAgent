using './jumpbox.bicep'

// ponytail: adminPassword deliberately NOT set here. Pass it at deploy time:
//   az deployment group create ... --parameters adminPassword=$env:JUMPBOX_ADMIN_PASSWORD
// Never commit a password value into this file.
param location = 'westus3'
param vnetName = 'vnet-hostedobo-wus3'
param jumpboxVmName = 'vm-obo-jump'
param adminUsername = 'azureadmin'

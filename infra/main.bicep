// MiniERP on Azure: App Service (Linux, .NET 8) hosting the API + Angular build, and
// Azure SQL Database (serverless, auto-pauses when idle to keep cost near zero).
//
//   az group create -n rg-minierp -l centralindia
//   az deployment group create -g rg-minierp -f infra/main.bicep \
//     -p sqlAdminPassword=<secret> jwtKey=<secret>
//
// Secrets are passed as secure parameters and never stored in this repository.

@description('Short name used to derive resource names.')
param appName string = 'minierp'

param location string = resourceGroup().location

@description('SQL administrator login.')
param sqlAdminLogin string = 'minierpadmin'

@secure()
@description('SQL administrator password.')
param sqlAdminPassword string

@secure()
@description('JWT signing key (32+ random characters).')
param jwtKey string

@description('Seed the fictional demo company on first start.')
param seedDemoData bool = true

var suffix = uniqueString(resourceGroup().id)
var sqlServerName = 'sql-${appName}-${suffix}'
var webAppName = 'app-${appName}-${suffix}'

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource database 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: 'MiniErp'
  location: location
  sku: {
    name: 'GP_S_Gen5_1'
    tier: 'GeneralPurpose'
  }
  properties: {
    autoPauseDelay: 60
    minCapacity: json('0.5')
    requestedBackupStorageRedundancy: 'Local'
  }
}

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: 'plan-${appName}-${suffix}'
  location: location
  kind: 'linux'
  sku: {
    name: 'B1'
  }
  properties: {
    reserved: true
  }
}

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: webAppName
  location: location
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      alwaysOn: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      healthCheckPath: '/health'
      appSettings: [
        { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
        { name: 'Jwt__Key', value: jwtKey }
        { name: 'Database__SeedDemoData', value: string(seedDemoData) }
        { name: 'App__TimeZone', value: 'Asia/Kolkata' }
      ]
      connectionStrings: [
        {
          name: 'Default'
          type: 'SQLAzure'
          connectionString: 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${database.name};User ID=${sqlAdminLogin};Password=${sqlAdminPassword};Encrypt=True;Connection Timeout=60'
        }
      ]
    }
  }
}

output webAppName string = webApp.name
output url string = 'https://${webApp.properties.defaultHostName}'

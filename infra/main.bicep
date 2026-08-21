targetScope = 'resourceGroup'

@description('Short lowercase project prefix. It must be globally unique enough for ACR, Key Vault, and Azure SQL names.')
@minLength(5)
@maxLength(12)
param resourcePrefix string

@allowed([
  'dev'
  'stg'
  'prod'
])
param environmentName string = 'prod'

param location string = resourceGroup().location

@description('Microsoft Entra display name of the Azure SQL administrator user or group.')
param sqlEntraAdministratorLogin string

@description('Microsoft Entra object ID of the Azure SQL administrator user or group.')
param sqlEntraAdministratorObjectId string

@description('Bootstrap SQL login. SQL authentication is disabled after the Microsoft Entra administrator is configured.')
param sqlAdministratorLogin string = 'eis-bootstrap-admin'

@secure()
@minLength(16)
param sqlAdministratorPassword string

@secure()
@minLength(32)
param jwtSigningKey string

param resendEnabled bool = false

@secure()
param resendApiKey string = ''

param sqlDatabaseName string = 'EnterpriseIdentityService'
param sqlDatabaseSkuName string = 'GP_S_Gen5_1'
param sqlDatabaseSkuTier string = 'GeneralPurpose'
param sqlDatabaseCapacity int = 1
param sqlDatabaseMinCapacity int = 1
param sqlDatabaseAutoPauseDelay int = 60
param sqlDatabaseMaxSizeBytes int = 34359738368
param logRetentionInDays int = 30

var stem = '${toLower(resourcePrefix)}-${environmentName}'
var compactStem = replace(stem, '-', '')
var tags = {
  project: 'EnterpriseIdentityService'
  environment: environmentName
  'managed-by': 'Bicep'
}
var acrPullRoleDefinitionId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '7f951dda-4ed3-4680-a7ca-43fe172d538d')
var keyVaultSecretsUserRoleDefinitionId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '4633458b-17de-408a-b874-0445c86b69e6')

resource runtimeIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${stem}-api-mi'
  location: location
  tags: tags
}

resource migrationIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${stem}-migrate-mi'
  location: location
  tags: tags
}

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  #disable-next-line BCP334
  name: '${compactStem}acr'
  location: location
  tags: tags
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false
    dataEndpointEnabled: false
    publicNetworkAccess: 'Enabled'
  }
}

resource runtimeAcrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, runtimeIdentity.id, acrPullRoleDefinitionId)
  scope: registry
  properties: {
    roleDefinitionId: acrPullRoleDefinitionId
    principalId: runtimeIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource migrationAcrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, migrationIdentity.id, acrPullRoleDefinitionId)
  scope: registry
  properties: {
    roleDefinitionId: acrPullRoleDefinitionId
    principalId: migrationIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: '${stem}-kv'
  location: location
  tags: tags
  properties: {
    tenantId: tenant().tenantId
    enableRbacAuthorization: true
    enablePurgeProtection: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    publicNetworkAccess: 'Enabled'
    sku: {
      family: 'A'
      name: 'standard'
    }
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Allow'
    }
  }
}

resource jwtSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'jwt-signing-key'
  properties: {
    value: jwtSigningKey
  }
}

resource resendSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (resendEnabled) {
  parent: keyVault
  name: 'resend-api-key'
  properties: {
    value: resendApiKey
  }
}

resource runtimeKeyVaultSecretsUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, runtimeIdentity.id, keyVaultSecretsUserRoleDefinitionId)
  scope: keyVault
  properties: {
    roleDefinitionId: keyVaultSecretsUserRoleDefinitionId
    principalId: runtimeIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${stem}-law'
  location: location
  tags: tags
  properties: {
    retentionInDays: logRetentionInDays
    sku: {
      name: 'PerGB2018'
    }
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
    workspaceCapping: {
      dailyQuotaGb: json('0.5')
    }
  }
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${stem}-appi'
  location: location
  kind: 'web'
  tags: tags
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${stem}-cae'
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
    zoneRedundant: false
  }
}

resource sqlServer 'Microsoft.Sql/servers@2023-08-01' = {
  name: '${stem}-sql'
  location: location
  tags: tags
  properties: {
    administratorLogin: sqlAdministratorLogin
    administratorLoginPassword: sqlAdministratorPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    restrictOutboundNetworkAccess: 'Disabled'
    version: '12.0'
  }
}

resource sqlEntraAdministrator 'Microsoft.Sql/servers/administrators@2023-08-01' = {
  parent: sqlServer
  name: 'ActiveDirectory'
  properties: {
    administratorType: 'ActiveDirectory'
    login: sqlEntraAdministratorLogin
    sid: sqlEntraAdministratorObjectId
    tenantId: tenant().tenantId
  }
}

resource sqlEntraOnlyAuthentication 'Microsoft.Sql/servers/azureADOnlyAuthentications@2023-08-01' = {
  parent: sqlServer
  name: 'Default'
  properties: {
    azureADOnlyAuthentication: true
  }
  dependsOn: [
    sqlEntraAdministrator
  ]
}

resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-08-01' = {
  parent: sqlServer
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  tags: tags
  sku: {
    name: sqlDatabaseSkuName
    tier: sqlDatabaseSkuTier
    capacity: sqlDatabaseCapacity
    family: 'Gen5'
  }
  properties: {
    autoPauseDelay: sqlDatabaseAutoPauseDelay
    maxSizeBytes: sqlDatabaseMaxSizeBytes
    minCapacity: sqlDatabaseMinCapacity
    requestedBackupStorageRedundancy: 'Local'
    zoneRedundant: false
  }
}

output registryName string = registry.name
output registryLoginServer string = registry.properties.loginServer
output containerAppsEnvironmentName string = containerAppsEnvironment.name
output runtimeIdentityName string = runtimeIdentity.name
output runtimeIdentityClientId string = runtimeIdentity.properties.clientId
output migrationIdentityName string = migrationIdentity.name
output migrationIdentityClientId string = migrationIdentity.properties.clientId
output keyVaultName string = keyVault.name
output jwtSigningKeySecretVersion string = last(split(jwtSecret.properties.secretUriWithVersion, '/'))
output sqlServerName string = sqlServer.name
output sqlServerFullyQualifiedDomainName string = sqlServer.properties.fullyQualifiedDomainName
output provisionedSqlDatabaseName string = sqlDatabase.name
output applicationInsightsName string = applicationInsights.name

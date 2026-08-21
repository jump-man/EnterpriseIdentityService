targetScope = 'resourceGroup'

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

@description('Set false during first deployment to provision the migration job before creating the API.')
param deployApplication bool = true

@description('Immutable ACR image reference. Production should pass an image digest.')
param containerImage string

@description('Full source Git commit SHA represented by the image.')
@minLength(40)
@maxLength(40)
param gitSha string

@description('Lowercase revision suffix, normally the first 12 characters of gitSha.')
@minLength(1)
@maxLength(12)
param revisionSuffix string

@description('Externally reachable HTTPS origin used in verification and password-reset links.')
@minLength(9)
param publicBaseUrl string

@description('Exact 32-character Key Vault version of the jwt-signing-key secret. Rotation is a deliberate deployment change.')
@minLength(32)
@maxLength(32)
param jwtSigningKeySecretVersion string

param jwtIssuer string = 'EnterpriseIdentityService'
param jwtAudience string = 'EnterpriseIdentityService.Client'
param jwtExpirationMinutes int = 15
param authenticationSessionLifetime string = '30.00:00:00'
param recoveryTokenLifetime string = '00:15:00'
param recoveryRequestCooldown string = '00:01:00'
param emailVerificationTokenLifetime string = '1.00:00:00'
param emailVerificationResendCooldown string = '00:01:00'
param resendEnabled bool = false
param resendFromAddress string = 'onboarding@example.invalid'
param resendFromName string = 'Enterprise Identity Service'
param sqlDatabaseName string = 'EnterpriseIdentityService'

var stem = '${toLower(resourcePrefix)}-${environmentName}'
var compactStem = replace(stem, '-', '')
var containerAppName = '${stem}-api'
var initialApplicationRevisionName = '${containerAppName}--${revisionSuffix}'
var tags = {
  project: 'EnterpriseIdentityService'
  environment: environmentName
  'managed-by': 'Bicep'
}
var runtimeConnectionString = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Initial Catalog=${sqlDatabaseName};Authentication=Active Directory Managed Identity;User ID=${runtimeIdentity.properties.clientId};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
var migrationConnectionString = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Initial Catalog=${sqlDatabaseName};Authentication=Active Directory Managed Identity;User ID=${migrationIdentity.properties.clientId};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
var applicationSecrets = concat([
  {
    name: 'jwt-signing-key'
    keyVaultUrl: '${keyVault.properties.vaultUri}secrets/jwt-signing-key/${jwtSigningKeySecretVersion}'
    identity: runtimeIdentity.id
  }
], resendEnabled ? [
  {
    name: 'resend-api-key'
    keyVaultUrl: '${keyVault.properties.vaultUri}secrets/resend-api-key'
    identity: runtimeIdentity.id
  }
] : [])
var resendEnvironment = resendEnabled ? [
  {
    name: 'Resend__ApiKey'
    secretRef: 'resend-api-key'
  }
] : []
var applicationEnvironment = concat([
  {
    name: 'ASPNETCORE_ENVIRONMENT'
    value: 'Production'
  }
  {
    name: 'ASPNETCORE_HTTP_PORTS'
    value: '8080'
  }
  {
    name: 'ConnectionStrings__Database'
    value: runtimeConnectionString
  }
  {
    name: 'Jwt__Issuer'
    value: jwtIssuer
  }
  {
    name: 'Jwt__Audience'
    value: jwtAudience
  }
  {
    name: 'Jwt__SigningKey'
    secretRef: 'jwt-signing-key'
  }
  {
    name: 'Jwt__ExpirationMinutes'
    value: string(jwtExpirationMinutes)
  }
  {
    name: 'AuthenticationSessions__Lifetime'
    value: authenticationSessionLifetime
  }
  {
    name: 'PasswordRecovery__TokenLifetime'
    value: recoveryTokenLifetime
  }
  {
    name: 'PasswordRecovery__RequestCooldown'
    value: recoveryRequestCooldown
  }
  {
    name: 'PasswordRecovery__PublicBaseUrl'
    value: publicBaseUrl
  }
  {
    name: 'EmailVerification__TokenLifetime'
    value: emailVerificationTokenLifetime
  }
  {
    name: 'EmailVerification__ResendCooldown'
    value: emailVerificationResendCooldown
  }
  {
    name: 'EmailVerification__PublicBaseUrl'
    value: publicBaseUrl
  }
  {
    name: 'Resend__Enabled'
    value: string(resendEnabled)
  }
  {
    name: 'Resend__FromAddress'
    value: resendFromAddress
  }
  {
    name: 'Resend__FromName'
    value: resendFromName
  }
  {
    name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
    value: applicationInsights.properties.ConnectionString
  }
  {
    name: 'Deployment__GitSha'
    value: gitSha
  }
], resendEnvironment)

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' existing = {
  #disable-next-line BCP334
  name: '${compactStem}acr'
}

resource runtimeIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = {
  name: '${stem}-api-mi'
}

resource migrationIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = {
  name: '${stem}-migrate-mi'
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: '${stem}-kv'
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: '${stem}-appi'
}

resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' existing = {
  name: '${stem}-cae'
}

resource sqlServer 'Microsoft.Sql/servers@2023-08-01' existing = {
  name: '${stem}-sql'
}

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = if (deployApplication) {
  name: containerAppName
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${runtimeIdentity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      activeRevisionsMode: 'Multiple'
      maxInactiveRevisions: 5
      ingress: {
        external: true
        allowInsecure: false
        targetPort: 8080
        transport: 'auto'
        traffic: [
          {
            revisionName: initialApplicationRevisionName
            weight: 100
          }
        ]
      }
      registries: [
        {
          server: registry.properties.loginServer
          identity: runtimeIdentity.id
        }
      ]
      secrets: applicationSecrets
    }
    template: {
      revisionSuffix: revisionSuffix
      containers: [
        {
          name: 'api'
          image: containerImage
          env: applicationEnvironment
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          probes: [
            {
              type: 'Startup'
              httpGet: {
                path: '/health/live'
                port: 8080
                scheme: 'HTTP'
              }
              initialDelaySeconds: 5
              periodSeconds: 5
              timeoutSeconds: 3
              failureThreshold: 10
              successThreshold: 1
            }
            {
              type: 'Liveness'
              httpGet: {
                path: '/health/live'
                port: 8080
                scheme: 'HTTP'
              }
              initialDelaySeconds: 30
              periodSeconds: 30
              timeoutSeconds: 5
              failureThreshold: 3
              successThreshold: 1
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/health/ready'
                port: 8080
                scheme: 'HTTP'
              }
              initialDelaySeconds: 10
              periodSeconds: 10
              timeoutSeconds: 5
              failureThreshold: 6
              successThreshold: 1
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
      terminationGracePeriodSeconds: 30
    }
  }
}

resource migrationJob 'Microsoft.App/jobs@2024-03-01' = {
  name: '${stem}-migrate'
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${migrationIdentity.id}': {}
    }
  }
  properties: {
    environmentId: containerAppsEnvironment.id
    configuration: {
      triggerType: 'Manual'
      replicaTimeout: 900
      replicaRetryLimit: 0
      manualTriggerConfig: {
        parallelism: 1
        replicaCompletionCount: 1
      }
      registries: [
        {
          server: registry.properties.loginServer
          identity: migrationIdentity.id
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'migration'
          image: containerImage
          command: [
            '/app/migrations/efbundle'
          ]
          args: [
            '--verbose'
          ]
          env: [
            {
              name: 'ConnectionStrings__Database'
              value: migrationConnectionString
            }
            {
              name: 'Deployment__GitSha'
              value: gitSha
            }
          ]
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
        }
      ]
    }
  }
}

output containerAppName string = deployApplication ? containerApp.name : ''
output containerAppFqdn string = deployApplication ? containerApp!.properties.configuration.ingress.fqdn : ''
output migrationJobName string = migrationJob.name
output deployedImage string = containerImage
output deployedGitSha string = gitSha

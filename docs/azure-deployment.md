# Azure deployment

The deployment architecture provides a reproducible production path without changing the Domain or
Application projects. Azure remains a deployment concern; the API still consumes
ordinary ASP.NET Core configuration and the Infrastructure project still configures
EF Core from `ConnectionStrings:Database`.

Live Azure provisioning has not been performed because an eligible Azure
subscription is not currently available. The deployment architecture, infrastructure
definitions, and CI/CD path are implemented and validated without claiming a live
production deployment.

## Architecture

```text
GitHub push / pull request
        |
        v
GitHub Actions CI
  restore -> build -> unit/integration/architecture tests
  -> NuGet vulnerability gate -> Bicep build
  -> Docker contract checks -> Trivy gate -> SPDX SBOM
        |
        v
validated image.tar identified by the full Git SHA
        |
        v
GitHub Actions production environment + OIDC
        |
        v
Azure Container Registry (immutable SHA tag, then digest)
        |
        v
Azure Container Apps manual migration job (EF bundle, one execution)
        |
        v
Azure Container Apps candidate revision -> readiness -> traffic promotion
        |
        +--> Azure SQL Database (Microsoft Entra managed identity)
        +--> Key Vault (JWT and optional Resend secret references)
        +--> Log Analytics / Application Insights
```

Local Development remains Docker Compose plus its SQL Server container. Production
uses Azure SQL Database; the Compose SQL image is never deployed to Azure.

## Repository deployment files

- `infra/main.bicep` provisions the shared production foundation.
- `infra/workload.bicep` provisions the manual migration job and API workload from
  an immutable image reference.
- `infra/production.parameters.example.json` contains only non-secret example
  parameters. Required secure parameters are deliberately absent.
- `infra/sql/bootstrap-identities.sql` creates separate contained database users for
  the API and migration managed identities.
- `.github/workflows/ci.yml` validates source and produces the deployable artifact.
- `.github/workflows/deploy.yml` pushes, migrates, creates a revision, verifies it,
  and promotes traffic.

## Azure resources and cost profile

The foundation creates one Basic ACR, two user-assigned managed identities, one
Key Vault, one Log Analytics workspace with 30-day retention and a 0.5 GB daily cap,
one workspace-based Application Insights component, one Consumption Container Apps
environment, and one Azure SQL logical server/database. The workload creates one
externally accessible Container App and one manually triggered Container Apps Job.

The steady-state API scale is deliberately `minReplicas: 1`, `maxReplicas: 1`.
Current fixed-window rate-limit state lives in process memory, so multiple replicas
would not enforce a service-wide limit. A deployment briefly runs the candidate and
previous revision together; after successful promotion, the workflow deactivates the
old revision and returns to one active replica. The old revision remains in Container
Apps history but is cold: rollback must reactivate it before restoring traffic.

Azure SQL and monitoring ingestion are normally the dominant recurring costs. The
defaults select a small serverless General Purpose SQL database, local backup
redundancy, Basic ACR, Consumption compute, bounded log retention/ingestion, and a
single production environment. Confirm current regional pricing and tune the SQL
SKU, auto-pause behavior, and log cap for the expected workload before provisioning.

## Naming and environments

`resourcePrefix` must be 5-12 lowercase letters, digits, or hyphens and must make the
globally scoped ACR, Key Vault, and SQL names unique. Bicep combines it with `dev`,
`stg`, or `prod`. Production is the only paid Azure environment required initially;
Development remains local. The same templates can add Staging later without changing
application boundaries.

Resources receive `project`, `environment`, and `managed-by` tags. Names are
deterministic, so CD needs only `AZURE_RESOURCE_PREFIX` and the resource group.

## Foundation provisioning

Prerequisites are Azure CLI with Bicep, permission to create the represented
resources and role assignments, and a Microsoft Entra user or group that will be the
Azure SQL administrator. Register the `Microsoft.App`, `Microsoft.ContainerRegistry`,
`Microsoft.KeyVault`, `Microsoft.ManagedIdentity`, `Microsoft.OperationalInsights`,
`Microsoft.Insights`, and `Microsoft.Sql` resource providers if the subscription has
not used them before.

From PowerShell, supply secret values from a secure local source. Never put them in
the example parameter file, shell history, CI logs, or source control.

```powershell
$resourceGroup = 'rg-eis-prod'
$location = 'swedencentral'
$resourcePrefix = 'eisunique01'
$entraSqlAdminName = 'replace-with-entra-group-name'
$entraSqlAdminObjectId = '00000000-0000-0000-0000-000000000000'

az group create --name $resourceGroup --location $location

az deployment group create `
  --name foundation `
  --resource-group $resourceGroup `
  --template-file infra/main.bicep `
  --parameters '@infra/production.parameters.example.json' `
  resourcePrefix=$resourcePrefix `
  sqlEntraAdministratorLogin=$entraSqlAdminName `
  sqlEntraAdministratorObjectId=$entraSqlAdminObjectId `
  sqlAdministratorPassword=$env:EIS_SQL_BOOTSTRAP_PASSWORD `
  jwtSigningKey=$env:EIS_JWT_SIGNING_KEY
```

Record the non-secret `jwtSigningKeySecretVersion` deployment output as the GitHub
production environment variable `JWT_SIGNING_KEY_SECRET_VERSION`. It is the exact
32-character Key Vault version created by the foundation deployment.

If Resend is enabled, also pass `resendEnabled=true` and
`resendApiKey=$env:EIS_RESEND_API_KEY`. The SQL bootstrap password is required by the
Azure SQL resource creation API, but `main.bicep` enables Microsoft Entra-only SQL
authentication after configuring the Entra administrator. The application never
receives that password.

Review the deployment outputs for names and identity client IDs. No output contains
a secret or connection string.

## Database identity bootstrap

Azure RBAC does not create contained Azure SQL database users. From a controlled
administrative endpoint that can reach the SQL server, authenticate as the configured
Microsoft Entra SQL administrator and run:

```powershell
sqlcmd `
  -S '<server-name>.database.windows.net' `
  -d 'EnterpriseIdentityService' `
  -G `
  -i infra/sql/bootstrap-identities.sql `
  -v RuntimeIdentityName='<prefix>-prod-api-mi' `
     MigrationIdentityName='<prefix>-prod-migrate-mi'
```

The runtime identity receives only `db_datareader` and `db_datawriter`. The migration
identity additionally receives `db_ddladmin`; it is attached only to the manual job.
Do not grant the API schema-change rights or use the SQL administrator for routine
runtime/migration access.

The initial SQL firewall rule allows connections from Azure services so Consumption
Container Apps can reach the public SQL endpoint. This is broader than a private
network boundary: other Azure tenants can reach the endpoint but cannot authenticate.
If the bootstrap client is outside Azure, add a narrow temporary firewall rule for
its IP and remove it immediately afterward. Private Endpoint and VNet integration are
deferred hardening, not reasons to describe the public endpoint as harmless.

## GitHub-to-Azure authentication

Create a dedicated Microsoft Entra application/service principal with a federated
credential whose subject is:

```text
repo:<owner>/<repository>:environment:production
```

The production workflow uses `azure/login` with OIDC. It has no reusable Azure
client secret. Configure these GitHub repository variables:

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`
- `AZURE_RESOURCE_GROUP`
- `AZURE_RESOURCE_PREFIX`
- `PRODUCTION_PUBLIC_BASE_URL` (an HTTPS origin)
- `JWT_SIGNING_KEY_SECRET_VERSION` (the foundation deployment output)

Protect a GitHub environment named `production` with required reviewers as
appropriate. The workflow grants `id-token: write`, `actions: read`, and
`contents: read` only to the deployment job; CI has only `contents: read`.

Grant the deployment principal `AcrPush` on this ACR. At the narrowest practical
scope, grant resource reads plus the required deployment, Container App revision,
traffic and secret-configuration, and Container Apps Job update/start/execution
actions. The first deployment also needs `Microsoft.Resources/deployments/*` and
create/update access to the API and migration job. Prefer a custom role scoped to the
production resource group or the individual resources; do not use Owner, and separate
the more privileged foundation provisioner from the routine CD identity.

## Runtime managed identities and secrets

The API identity has `AcrPull` on ACR and `Key Vault Secrets User` on this vault. The
migration identity has `AcrPull` but no Key Vault role. Container Apps pulls from ACR
with those identities; registry admin credentials are disabled and no registry
password is stored in GitHub, Bicep, the image, or application configuration.

Key Vault holds `jwt-signing-key` and, when enabled, `resend-api-key`. Container Apps
resolves Key Vault references and presents them under the existing configuration
keys `Jwt__SigningKey` and `Resend__ApiKey`. Non-secret settings use standard double
underscore environment mapping, including:

```text
ConnectionStrings__Database
Jwt__Issuer / Jwt__Audience / Jwt__ExpirationMinutes
AuthenticationSessions__Lifetime
PasswordRecovery__TokenLifetime / RequestCooldown / PublicBaseUrl
EmailVerification__TokenLifetime / ResendCooldown / PublicBaseUrl
Resend__Enabled / FromAddress / FromName
APPLICATIONINSIGHTS_CONNECTION_STRING
```

The Azure SQL connection strings contain no password. They select `Active Directory
Managed Identity` and the appropriate user-assigned identity client ID. Existing
startup validation continues to reject missing/invalid critical settings. JWT
validation and symmetric signing are unchanged; every future replica must receive
compatible signing material.

The JWT Key Vault reference includes the exact secret version supplied through
`jwtSigningKeySecretVersion`. Creating a newer Key Vault version therefore does not
silently restart the app with a different signing key and invalidate existing access
tokens. Changing the pinned version is a deliberate workload deployment decision;
coordinated multi-key JWT rotation remains deferred. The optional Resend reference is
versionless and follows the latest enabled Key Vault secret version.

CD reapplies the configured versioned JWT reference before creating each later
candidate and verifies the resulting URI. Changing
`JWT_SIGNING_KEY_SECRET_VERSION` is therefore an explicit signing-key cutover, not an
automatic rotation mechanism; with the current single-key validator it invalidates
tokens signed only by the prior key and requires an intentional operational plan.

## CI policy

CI runs restore, Release build, and the unit, integration, and architecture projects
separately. It fails on any known vulnerable direct or transitive NuGet package. It
builds both Bicep templates, builds the production Dockerfile, verifies the `app`
runtime user, port `8080`, source-revision label, executable migration bundle, lack
of an SDK in the runtime image, fail-fast invalid configuration, and runnable
liveness behavior.

Trivy blocks fixed High or Critical operating-system/library vulnerabilities.
Unfixed findings remain visible but do not block under the initial policy; this is a
risk decision, not a claim that the image is vulnerability-free. Anchore Syft creates
an SPDX JSON SBOM. The image archive, checksum, metadata, and SBOM are retained as a
seven-day CI artifact for main-branch pushes. Third-party actions are pinned to full
commit SHAs.

## Production deployment flow

The deployment workflow runs only after successful main-branch CI or an explicit
manual selection of a successful CI run and matching full SHA. It:

1. verifies the CI run, SHA, archive checksum, SBOM, and image source label;
2. authenticates to Azure through OIDC;
3. pushes that exact image archive to ACR as
   `enterprise-identity-service:<full-git-sha>`;
4. resolves and records the immutable ACR digest;
5. on the first run, deploys only the manual migration job;
6. updates and starts one migration execution from the immutable digest;
7. stops immediately if migration fails or times out;
8. on later deployments, replaces any moving `latestRevision` traffic rule with an
   explicit 100% assignment to the current production revision;
9. creates a new candidate revision from the same digest and verifies that production
   remains 100% on the previous revision while the candidate has 0%;
10. waits for the candidate revision FQDN's `/health/ready` and Azure health state;
11. shifts the candidate to 100% and the previous revision to 0% only after that check;
12. verifies the production HTTPS readiness endpoint; and
13. deactivates the previous revision after success while retaining its immutable
    configuration as a cold rollback target.

The first API deployment is intentionally separate because no production revision
exists to protect with a 100/0 split. Its explicitly named initial revision receives
100% traffic when the app is created, then the workflow verifies both its revision
FQDN and the production endpoint. Later deployments never use a `latestRevision`
traffic rule, so Azure readiness alone cannot promote a candidate ahead of the
workflow's explicit check.

CI and CD never rebuild different production image contents. `latest` is neither
created nor deployed. Git SHA tag, ACR digest, OCI revision label, Container Apps
revision suffix, and deployment summary provide the traceability chain.

The checked-in workload defaults are suitable for initial provisioning. If email or
other non-secret production settings differ, deploy `infra/workload.bicep` with the
corresponding parameters before relying on routine image-only deployments.

## EF Core migrations

The Docker build creates `/app/migrations/efbundle` from the same source revision as
the API. API startup still contains no `Database.Migrate()` call. The manual job runs
the bundle once with `parallelism: 1`, `replicaCompletionCount: 1`, and no automatic
retry. Its connection arrives through `ConnectionStrings__Database`; credentials do
not appear in process arguments.

Production migrations should remain backward-compatible with the immediately
previous application revision whenever practical. Prefer additive changes. For a
destructive change, use expand/contract: add compatible schema, deploy code using it,
wait through the compatibility window, then remove obsolete schema in a later
deployment. Never automatically run a down migration during application rollback.

## HTTPS, probes, logs, and telemetry

Azure Container Apps terminates public TLS and forwards HTTP to port `8080`; the
image contains no production certificate. `allowInsecure: false` enforces HTTPS at
ingress.

Liveness and startup probes use `/health/live`. Readiness uses `/health/ready` and
therefore includes Azure SQL reachability. SQL failure produces live `200` and ready
`503`; it does not trigger a false process-death classification. Responses retain
their minimal status-only shape.

Structured application logs stay on stdout/stderr and flow to Log Analytics through
the Container Apps environment. With `APPLICATIONINSIGHTS_CONNECTION_STRING`, the
API's OpenTelemetry foundation exports request, error, runtime, and related telemetry
to Application Insights. Correlation IDs and sanitized exception/request logging are
preserved; the security audit trail remains separate durable SQL data. No log volume
or application file logger is introduced.

## Rollback

List retained revisions and identify the previous known-good immutable revision:

```powershell
az containerapp revision list `
  --name '<prefix>-prod-api' `
  --resource-group '<resource-group>' `
  --output table
```

The retained revision is inactive and has no running replica. Rollback is therefore
not warm or immediate: activate it, wait for its revision FQDN readiness if needed,
restore traffic, verify production readiness, and deactivate the failed revision:

```powershell
az containerapp revision activate --name '<app>' --resource-group '<rg>' --revision '<known-good>'
$revisionFqdn = az containerapp revision show --name '<app>' --resource-group '<rg>' `
  --revision '<known-good>' --query properties.fqdn --output tsv
curl.exe --fail "https://$revisionFqdn/health/ready"
az containerapp ingress traffic set --name '<app>' --resource-group '<rg>' `
  --revision-weight '<known-good>=100' '<failed>=0'
curl.exe --fail 'https://<app-fqdn>/health/ready'
az containerapp revision deactivate --name '<app>' --resource-group '<rg>' --revision '<failed>'
```

Use the retained revision/image digest, never `latest`. Rollback is safe only if the
database remains compatible with that revision. If a migration made it incompatible,
stop and execute a deliberate data/schema recovery plan; do not automate a down
migration.

## Deferred hardening

Future work may add private endpoints and full VNet integration for SQL/Key Vault/ACR,
distributed rate limiting before multiple steady-state replicas, asymmetric JWT
signing and rotation, advanced artifact signing/admission policy, a permanent Staging
environment, WAF/gateway controls, and multi-region recovery. These are intentionally
deferred to keep the initial portfolio deployment understandable, low-cost, and
proportionate.

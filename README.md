# Enterprise Identity Service

Production-oriented Identity and Access Management service built with ASP.NET Core.

## Authentication

- `POST /api/auth/register` creates a user.
- `POST /api/auth/login` authenticates an active user and returns a short-lived JWT access token.
- `GET /api/users/me` requires `Authorization: Bearer <access-token>` and returns the authenticated user.

JWT settings are read from the `Jwt` configuration section: `Issuer`, `Audience`,
`SigningKey`, and `ExpirationMinutes`. Supply the signing key through user secrets,
environment variables, or deployment secret management; the development value is
not suitable for production. In Swagger UI, use **Authorize** and enter the raw JWT.

Access tokens are short lived and contain current permission snapshots. Persisted
refresh sessions support rotation, replay detection, per-session logout, and global
invalidation. Administrative APIs use application-defined permissions inherited
through roles.

## Security audit trail

Security-sensitive authentication, session, password, and authorization operations
produce append-only semantic audit records in the identity database. Audit records
contain controlled event identifiers and safe request context only; credentials,
tokens, hashes, request bodies, and arbitrary headers are never part of the audit
creation surface.

`GET /api/audit` requires `audit.read`. Queries default to the previous 30 days,
allow a maximum 90-day range and 100 records per page, and use deterministic cursor
pagination. The `userId` filter explicitly matches either the actor or target user.

IP addresses and bounded User-Agent values are retained for security investigation
and should be covered by the deployment's privacy and retention policy. Automated
retention/purge jobs, legal holds, external SIEM export, telemetry adapters, and an
Outbox are future extensions; Phase 13 intentionally provides no audit deletion API.

## Operational readiness

- `GET /health/live` reports whether the API process can answer requests. It does
  not access SQL Server or other external dependencies.
- `GET /health/ready` checks whether the configured identity database is reachable
  and returns `503 Service Unavailable` when that critical dependency is unavailable.

Both endpoints are anonymous, exempt from client rate limits, and return only a
minimal status document. They do not expose dependency names, connection details,
exceptions, environment information, or stack traces.

Production database migrations are an explicit deployment operation: apply the
migrations first, deploy/start the API second, and allow traffic only after readiness
becomes healthy. The API does not automatically migrate a production database at
startup.

## Correlation and operational logging

Clients may send one `X-Correlation-ID` header using up to 64 ASCII letters, digits,
periods, underscores, or hyphens. Missing, ambiguous, or invalid values are replaced
with a server-generated identifier. The effective value is returned in the same
response header, included in RFC 7807 Problem Details, placed in the structured
logging scope, and reused by the security audit context.

Request-completion logs contain only bounded method, normalized route template,
status, duration, correlation ID, and trace ID metadata. Request and response bodies,
raw query strings, authorization/cookie headers, passwords, access/refresh tokens,
reset/verification tokens, signing keys, and connection strings are not logged.

## OpenTelemetry foundation

The API registers vendor-neutral OpenTelemetry instrumentation for inbound ASP.NET
Core requests and .NET runtime metrics. When
`APPLICATIONINSIGHTS_CONNECTION_STRING` is supplied, the API enables the Azure
Monitor OpenTelemetry distribution and exports telemetry to Application Insights.
Local development remains exporter-free by default. Application logging continues
through `ILogger<T>`, and security audit records remain a separate durable concern.

## Docker and local containers

The repository includes a production-oriented Linux image for the API and a small,
cloud-neutral Compose topology for local development. The API image is built with
the .NET 10 SDK and runs on the ASP.NET Core runtime as the image's non-root `app`
user. SQL Server is reachable from the API through Compose service DNS as `sql`; it
stores its database files in the named `sql-data` volume.

Install Docker with Compose v2 and the .NET 10 SDK. From the repository root, create
the local environment file and replace **both** `replace-me` database values with
the same strong SQL Server password. Replace `JWT_SIGNING_KEY` with an independent,
random value of at least 32 characters:

```powershell
Copy-Item .env.example .env
```

`.env` contains credentials and must never be committed, copied into an image, or
used as the production secret store. `.gitignore` and `.dockerignore` exclude it;
production platforms should inject equivalent environment variables from their
secret-management facility.

Build the API image directly with:

```powershell
docker build --pull --tag enterprise-identity-service:local .
```

For a new database, start SQL Server first and wait for it to become healthy:

```powershell
docker compose up -d sql
docker compose ps
```

Database migrations are an explicit operation and are never run by API startup.
Configure a host-side connection using the same password as `SQL_SA_PASSWORD` in
`.env`, restore the repository-local EF tool, and apply the migrations before
starting the API:

```powershell
$env:ConnectionStrings__Database = 'Server=localhost,1433;Database=EnterpriseIdentityService;User ID=sa;Password=replace-with-the-value-from-.env;Encrypt=True;TrustServerCertificate=True'
dotnet tool restore
dotnet ef database update --project src/EnterpriseIdentityService.Infrastructure --startup-project src/EnterpriseIdentityService.Api
Remove-Item Env:ConnectionStrings__Database
```

If `SQL_HOST_PORT` is changed, use that host port in the migration connection
string. SQL Server is available to local tools at `localhost,SQL_HOST_PORT` as
`sa`. The API connection string must continue to use the container endpoint
`sql,1433`, never `localhost`.

Start or rebuild the complete topology after migrations:

```powershell
docker compose up --build -d
docker compose up --build -d api  # rebuild only the API after source changes
```

With the example ports, the API and health endpoints are:

- API: `http://localhost:8080`
- Liveness: `http://localhost:8080/health/live`
- Readiness: `http://localhost:8080/health/ready`

The container intentionally serves HTTP on port `8080`. TLS terminates at the
deployment ingress, reverse proxy, or cloud edge; no development certificate is
baked into the image. Password-recovery and email-verification public base URLs
remain HTTPS-only under existing startup validation and should identify that
external TLS endpoint. The example values are inert local placeholders while email
delivery is disabled.

Use stdout/stderr logs and stop the topology with:

```powershell
docker compose logs --follow api
docker compose down
```

`docker compose down` preserves the `sql-data` volume, so users, roles, sessions,
refresh tokens, and audit records survive container recreation. To intentionally
erase the local database, run `docker compose down --volumes`; this is destructive
and cannot be undone unless the database was backed up.

The API image deliberately has no added HTTP client solely for a Dockerfile
`HEALTHCHECK`. Compose checks SQL readiness with the image's bundled `sqlcmd`, while
deployment platforms should probe `/health/live` for process liveness and
`/health/ready` for database-backed readiness. A transient database outage makes
readiness fail without redefining process liveness. Compose also leaves restart and
resource policies to the deployment environment.

The API filesystem is stateless and has no application log volume. Durable identity
and audit state lives in SQL Server. No data-protection-key volume is added because
the current authentication/session implementation does not depend on ASP.NET Core
Data Protection. JWT signing configuration must be identical across replicas.
Current fixed-window rate limits are process-local, so a future multi-replica
deployment will need a deliberate distributed rate-limit strategy.

## Azure deployment and CI/CD

Production is designed for Azure Container Apps, Azure Container Registry, Azure
SQL Database, Key Vault, Log Analytics, and Application Insights. Bicep provisions
the Azure resources, while GitHub Actions validates source and an immutable image,
runs dependency and image security gates, creates an SBOM, pushes the exact validated
artifact to ACR, executes a single EF migration job, and promotes a healthy revision.

The runtime remains cloud-neutral below the API composition root: Domain and
Application contain no Azure references, existing ASP.NET Core configuration keys
are supplied as environment variables, and local Compose continues to use its SQL
Server container. Production uses managed identity for ACR, Key Vault, and Azure SQL
access. See [Azure deployment](docs/azure-deployment.md) for provisioning, OIDC,
configuration, migration, deployment, rollback, security, networking, observability,
cost, and future-hardening guidance.

### Deployment status

The Azure production architecture and deployment pipeline are implemented and
validated through CI. Live Azure provisioning has not been performed because an
eligible Azure subscription is not currently available.

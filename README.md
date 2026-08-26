# Enterprise Identity Service

Enterprise Identity Service is a production-oriented Identity and Access Management API built with .NET 10 and ASP.NET Core. It applies Clean Architecture to authentication, permission-based authorization, security controls, observability, and deployment concerns while keeping the domain model independent of frameworks and persistence.

## Key Features

- User registration, email verification, and account activation
- JWT authentication and authenticated password changes
- Password recovery and reset
- Refresh-token rotation with replay detection
- Per-session logout and logout-all
- Roles, permissions, and administrative authorization APIs
- Authorization-version invalidation for issued access tokens
- Append-only security audit trail
- Liveness and database-backed readiness endpoints
- Correlation IDs and structured request logging
- OpenTelemetry instrumentation with optional Azure Monitor export
- Docker and Docker Compose support
- CI builds, tests, security checks, and SBOM generation
- Bicep-based Azure deployment architecture

## Architecture

The solution follows Clean Architecture with dependencies pointing inward:

- **Domain** contains persistence-ignorant entities, value objects, domain events, and business invariants. It has no dependency on the other solution layers.
- **Application** contains use cases and abstractions for authentication, authorization, persistence, email, and auditing. Among solution projects, Application depends only on Domain.
- **Infrastructure** implements Application abstractions with Entity Framework Core, SQL Server, token services, password hashing, and email delivery.
- **Api** is the ASP.NET Core composition root and exposes Minimal API endpoints, authentication and authorization middleware, health checks, and observability.

Architecture tests enforce the Domain and Application dependency boundaries and prevent Infrastructure from referencing Api.

## Tech Stack

- .NET 10, C#, ASP.NET Core Minimal APIs
- Entity Framework Core, SQL Server
- JWT bearer authentication, policy-based authorization
- Swashbuckle and OpenAPI
- OpenTelemetry, Azure Monitor, Application Insights
- xUnit and ASP.NET Core integration testing
- Docker and Docker Compose
- GitHub Actions, Trivy, SPDX SBOM generation
- Bicep, Azure Container Apps, Azure Container Registry, Azure SQL Database, Azure Key Vault

## Project Structure

```text
src/
  EnterpriseIdentityService.Domain
  EnterpriseIdentityService.Application
  EnterpriseIdentityService.Infrastructure
  EnterpriseIdentityService.Api

tests/
  EnterpriseIdentityService.UnitTests
  EnterpriseIdentityService.IntegrationTests
  EnterpriseIdentityService.ArchitectureTests

infra/                  Azure Bicep templates and SQL identity bootstrap
docs/                   Architecture decisions and deployment guidance
.github/workflows/      CI and Azure deployment workflows
```

The `src` projects define the application layers, while the three test projects cover isolated behavior, infrastructure and HTTP workflows, and dependency rules.

## Testing & Quality

The solution includes unit, integration, and architecture tests. GitHub Actions restores and builds the solution with warnings treated as errors, runs each test suite, checks direct and transitive NuGet dependencies for known vulnerabilities, validates the Bicep templates, builds and smoke-tests the container image, scans it with Trivy, and generates an SPDX SBOM.

Run the complete local validation with:

```powershell
dotnet restore EnterpriseIdentityService.sln
dotnet build EnterpriseIdentityService.sln --configuration Release --no-restore
dotnet test EnterpriseIdentityService.sln --configuration Release --no-build
```

## API Overview

The following routes are representative. In the Development environment, Swagger UI at `/swagger` provides the complete interactive API description.

### Authentication

- `POST /api/auth/register`
- `POST /api/auth/login`
- `POST /api/auth/refresh`
- `POST /api/auth/logout`
- `POST /api/auth/logout-all`

### Account

- `GET /api/users/me`
- `POST /api/users/verify-email`
- `POST /api/users/resend-verification-email`
- `POST /api/users/forgot-password`
- `POST /api/users/reset-password`
- `POST /api/users/change-password`

### Authorization

- `GET /api/permissions`
- `GET|POST /api/roles`
- `PUT /api/roles/{roleId}/permissions`
- `POST|DELETE /api/users/{userId}/roles/{roleId}`

### Audit

- `GET /api/audit` requires the `audit.read` permission.

### Health

- `GET /health/live`
- `GET /health/ready`

## Security & Audit

Access tokens are short-lived and carry a permission snapshot plus an authorization version. Role and permission changes invalidate affected users' existing authorization state. Refresh tokens are persisted as hashed, rotating session credentials; replay detection revokes the compromised session. Logout can revoke either the current session or all sessions for a user.

Security-sensitive authentication, password, session, and authorization operations create semantic, append-only audit records with controlled event identifiers and safe request context. Credentials, passwords, tokens, hashes, request bodies, and arbitrary headers are excluded. Audit queries are permission-protected, and the API provides no audit deletion endpoint.

JWT settings come from the `Jwt` configuration section. The development signing key is not production-safe: do not commit secrets, and supply production values through a secret-management system. Password-recovery and email-verification links require an HTTPS public base URL.

## Observability & Health

The API accepts a valid incoming `X-Correlation-ID` or replaces it with a generated value. The effective ID is returned to the client and flows through structured logs, Problem Details, and audit context. Request-completion logging records bounded request metadata without sensitive headers, bodies, query strings, or credentials.

OpenTelemetry instruments inbound ASP.NET Core requests and .NET runtime metrics. Local development requires no exporter; setting `APPLICATIONINSIGHTS_CONNECTION_STRING` enables Azure Monitor and Application Insights export. Durable security audit records remain separate from telemetry.

`/health/live` confirms that the process can serve requests without checking external dependencies. `/health/ready` checks identity-database connectivity and returns `503 Service Unavailable` when that dependency is unavailable.

## Running Locally

### Prerequisites

- Docker with Compose v2
- .NET 10 SDK

### 1. Configure local secrets

Create `.env`, replace both `replace-me` database values with the same strong SQL Server password, and set `JWT_SIGNING_KEY` to an independent random value of at least 32 characters:

```powershell
Copy-Item .env.example .env
```

The `.env` file contains credentials and must not be committed or copied into an image.

### 2. Start SQL Server

```powershell
docker compose up -d sql
docker compose ps
```

Wait for the SQL Server container to report healthy.

### 3. Apply database migrations

Migrations are an explicit operation; API startup does not apply them. Use the password and host port configured in `.env`:

```powershell
$env:ConnectionStrings__Database = 'Server=localhost,1433;Database=EnterpriseIdentityService;User ID=sa;Password=replace-with-the-value-from-.env;Encrypt=True;TrustServerCertificate=True'
dotnet tool restore
dotnet ef database update --project src/EnterpriseIdentityService.Infrastructure --startup-project src/EnterpriseIdentityService.Api
Remove-Item Env:ConnectionStrings__Database
```

If `SQL_HOST_PORT` is not `1433`, update the host-side migration connection string. The containerized API must continue to use the Compose service address `sql,1433` from `.env`.

### 4. Start the API

```powershell
docker compose up --build -d
```

With the example ports:

- API: `http://localhost:8080`
- Swagger UI: `http://localhost:8080/swagger`
- Liveness: `http://localhost:8080/health/live`
- Readiness: `http://localhost:8080/health/ready`

Rebuild only the API after source changes with `docker compose up --build -d api`. View logs with `docker compose logs --follow api` and stop the environment with:

```powershell
docker compose down
```

## Operational Notes

- SQL Server data persists in the `sql-data` volume when the Compose environment stops.
- The container serves HTTP on port `8080`; TLS must terminate at the ingress, reverse proxy, or cloud edge.
- The API writes logs to stdout and stderr and keeps durable identity and audit state in SQL Server.
- JWT signing configuration must be consistent across replicas.
- Fixed-window rate limits are process-local; a multi-replica deployment requires a distributed rate-limiting strategy.

## Azure Deployment Architecture & CI/CD

The Azure deployment architecture uses Container Apps, Container Registry, Azure SQL Database, Key Vault, Log Analytics, and Application Insights. Separate managed identities give the runtime and migration job only their required access to ACR, Key Vault, and Azure SQL.

The pipeline is designed to promote the same immutable container artifact validated by CI. It pushes that artifact to ACR, runs its EF Core migration bundle before application deployment, creates and verifies a candidate Container Apps revision, then promotes traffic. If post-promotion readiness fails, the workflow restores traffic to the previous revision. The API itself does not run production migrations at startup.

See [Azure deployment architecture and operations](docs/azure-deployment.md) for provisioning prerequisites, GitHub OIDC configuration, migration, deployment, rollback, networking, observability, and security guidance.

## Deployment Status

The repository contains the Azure infrastructure templates and deployment workflow, and CI is configured to validate the source, Bicep, and deployable container artifact. A live Azure production environment has **not** been provisioned because an eligible Azure subscription is currently unavailable.

## Further Documentation

- [Azure deployment architecture and operations](docs/azure-deployment.md)
- [Modular monolith architecture decision](docs/architecture/decisions/0001-use-modular-monolith.md)

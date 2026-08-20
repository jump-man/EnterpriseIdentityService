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
Core requests and .NET runtime metrics. No exporter, collector, Prometheus endpoint,
or external observability backend is configured yet. Application logging continues
through `ILogger<T>`, and security audit records remain a separate durable concern.

> 🚧 Work in Progress

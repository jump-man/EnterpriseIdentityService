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

> 🚧 Work in Progress

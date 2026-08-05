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

Access tokens are stateless. Refresh tokens, revocation, logout sessions, roles,
permissions, MFA, and external identity providers are outside the current scope.

> 🚧 Work in Progress

# EnterpriseIdentityService

## Architecture

- Use a Modular Monolith architecture.
- Follow Domain-Driven Design and Clean Architecture principles.
- Modules must not reference each other's internal projects.
- Domain projects must not reference Application, Infrastructure, or Presentation.
- SharedKernel must remain small and contain only stable shared abstractions.

## Technology

- .NET 10
- C#
- Nullable reference types enabled
- Treat warnings seriously
- Use xUnit for tests

## Domain conventions

- Keep domain models persistence-ignorant.
- Do not add Entity Framework Core attributes to Domain models.
- Use private setters where appropriate.
- State changes must happen through domain methods.
- Protect business invariants inside aggregates.
- Prefer value objects for validated domain concepts.
- Domain entities must not depend on system time directly.
- Do not introduce repositories or abstractions without a concrete use case.

## Workflow

Before changing code:

1. Inspect the relevant projects and existing conventions.
2. Explain the intended change briefly.
3. Make the smallest coherent change.
4. Run build and relevant tests.
5. Report changed files and command results.

Do not create commits unless explicitly requested.

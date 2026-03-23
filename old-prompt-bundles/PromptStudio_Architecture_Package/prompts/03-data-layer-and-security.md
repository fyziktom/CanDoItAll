# Codex Prompt 03 — Data Layer, Database Configuration, and Security Baseline

## Objective
Implement the EF Core persistence baseline, provider selection, design-time factory, file-store foundation, and secure secret storage.

## Required reading
1. `docs/02-technical-requirements.md`
2. `docs/04-solution-architecture.md`
3. `docs/07-implementation-plan.md`
4. `docs/09-validation-and-testing-plan.md`

## Constraints
- Use .NET 10 and C#.
- Use Blazor Web App with Interactive Server rendering.
- Use Tailwind CSS and the shared component strategy.
- Keep code comments in English.
- Preserve the modular monolith boundaries from the architecture package.
- Prefer one `DbContext` per operation via `IDbContextFactory`.
- Keep business logic out of page-only code.
- Do not log or expose secrets.
- Add or update tests for the touched behavior.
- Keep naming and file structure aligned with the package documents.

## Scope
This prompt covers M1: `AppDbContext`, runtime and design-time creation, SQLite/PostgreSQL support, storage abstractions, secret protection, and persistence tests.

## Tasks
1. Implement `AppDbContext` and module-owned EF configuration classes.
2. Implement runtime database provider selection for SQLite and PostgreSQL.
3. Implement `IDesignTimeDbContextFactory<AppDbContext>`.
4. Register `IDbContextFactory<AppDbContext>` properly for runtime access.
5. Implement initial migrations.
6. Implement managed file storage abstractions and workspace root resolution.
7. Implement the Security module baseline with `SecretRecord`, `SecretReference`, and `ISecretProtector`.
8. Ensure secret values are encrypted at rest and redacted in logs.
9. Add integration tests for SQLite path, secret round-trip, and basic persistence.
10. Add smoke coverage for PostgreSQL configuration or provider bootstrapping.

## Required deliverables
- `AppDbContext`
- database configuration for SQLite/PostgreSQL
- design-time factory
- file storage abstractions
- secret storage service
- initial migrations
- integration tests for persistence and secret safety

## Acceptance criteria
- app can start with SQLite configuration
- PostgreSQL configuration path is implemented and testable
- migrations can be created from the design-time factory
- secret values are stored encrypted
- resource/provider code can reference secrets without duplicating raw values
- touched tests pass

## Session output format
1. Scope summary
2. Implementation plan
3. Changed files
4. Test/build commands
5. Completion summary
6. Follow-up risks or next steps

## Stop condition
Stop when persistence and security foundations are robust enough for real feature modules.
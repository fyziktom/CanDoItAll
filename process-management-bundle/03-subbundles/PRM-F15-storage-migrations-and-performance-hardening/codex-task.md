# Codex task — PRM-F15

Implement **Storage, migrations, and performance hardening** inside the uploaded CanDoItAll solution.

## Constraints

- Treat `CanDoItAll.Modules.Processes` as the canonical owner for process-management behavior.
- Do not create a new durable agent registry; use CRM-HR bindings when actors are involved.
- Do not add direct compile-time dependency on the uploaded AgentFramework repo in the first process-management implementation.
- Keep all code comments in English.
- Preserve buildability for the current solution layout.

## Required outputs

- Code changes for this feature
- Matching tests
- Migration updates if persistence changes
- A short implementation note describing what changed and how it was verified

## Done definition

This task is done when:

- Process tables live in the main app database with consistent naming and indexing conventions.
- SQLite remains supported for local users without extra setup.
- PostgreSQL migrations exist and stay in lockstep with SQLite.
- The journal and runtime tables have a defined retention/extraction seam for future scale.

## Recommended first files to touch

- `src/CanDoItAll.Infrastructure/Persistence/AppDbContext.cs`
- `src/CanDoItAll.Migrations.Sqlite/*`
- `src/CanDoItAll.Migrations.PostgreSql/*`
- `tests/CanDoItAll.Tests.Integration/* database profile coverage`
- `tests/CanDoItAll.Tests.Playwright/AppSmokeTests.DatabaseProfiles.cs`

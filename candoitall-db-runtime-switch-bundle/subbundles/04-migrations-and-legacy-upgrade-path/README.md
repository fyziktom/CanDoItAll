# 04 Migrations and Legacy Upgrade Path

## Status

- `Ready`

## Objective

- Replace the current `EnsureCreatedAsync()` + SQLite-only initializer normal path with EF migrations for SQLite and PostgreSQL, while preserving a safe upgrade path for existing legacy SQLite databases.

## Covered Inputs

- `RQ-015` migrations as the normal path
- `RQ-016` legacy SQLite upgrade path
- `RQ-012` empty database creation foundations
- Raw notes `N-03`, `N-05`, `N-15`

## Prerequisites

- `subbundles/03-dynamic-runtime-db-and-bootstrap` completed with dynamic runtime resolution and switchable factory behavior.
- `subbundles/02-control-plane-and-profile-catalog` completed so legacy SQLite onboarding has a place to land.

## Exact Source References

- `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Infrastructure/Persistence/AppDbContextFactory.cs`
- `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Web/Program.cs`
- `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Modules.Workspace/WorkspaceSchemaInitializer.cs`
- `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Modules.Projects/ProjectsSchemaInitializer.cs`
- `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Modules.Factory/PromptFactorySchemaInitializer.cs`
- `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Modules.Workbench/ProjectWorkbenchSchemaInitializer.cs`
- `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Modules.Workbench/ProjectStructureAgentSchemaInitializer.cs`
- `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Web/Composition/ModuleAssemblies.cs`
- `/mnt/data/work/CanDoItAll-toolbox-repair/tests/CanDoItAll.Tests.Integration/TestApplication.cs`
- `/mnt/data/work/CanDoItAll-toolbox-repair/tests/CanDoItAll.Tests.Components/ComponentTestHarness.cs`

## Deliverables

- Provider-specific migration setup for SQLite and PostgreSQL, including design-time composition of the full modular model.
- A normal-path bootstrap service that runs migrations instead of `EnsureCreatedAsync()`.
- A safe legacy SQLite baseline/reconciliation path for DBs created by the current app.
- Updated production, integration, component, and browser harness bootstraps that all use the same migration/bootstrap path.
- Tests proving new DB creation and legacy SQLite upgrade behavior.

## Dependency Impact

- Provider parity, database creation, and clone/snapshot claims are not trustworthy until this phase lands.
- Subbundle 08 depends on this phase for every empty-create or clone target schema.
- If this phase is weak, later tests can pass only because they never exercised PostgreSQL or migration history.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Extract or relocate module-assembly composition so migration projects/design-time factories can discover the full model without depending on the web app directly.
2. Add provider-specific migration projects or an equivalent provider-safe migration setup for SQLite and PostgreSQL.
3. Generate/commit baseline migrations for both providers from the current modular model.
4. Implement a bootstrap service that runs migrations for new and existing DBs and becomes the only normal-path schema entry point.
5. Implement legacy SQLite detection and baseline insertion/reconciliation for DBs that have tables but no `__EFMigrationsHistory`.
6. Replace `EnsureCreatedAsync()` normal-path calls in app startup and test harnesses with the migration/bootstrap service.
7. Add integration tests for new empty DB bootstrap on SQLite and PostgreSQL plus legacy SQLite upgrade.

## Scope Exceptions

- The old SQLite initializer classes may remain temporarily as legacy-reconciliation helpers if needed.
- This subbundle does **not** yet implement the end-user UI for database creation.
- Clone/snapshot execution remains for subbundle 08.

## Do Not Do

- Do not leave `EnsureCreatedAsync()` as a production normal path and still mark this subbundle complete.
- Do not generate migrations from an incomplete model that ignores modular entity configurations.
- Do not assume PostgreSQL parity without real PostgreSQL migration/bootstrap proof.

## Acceptance Checklist

- New SQLite and PostgreSQL databases initialize through migrations/bootstrap, not through `EnsureCreatedAsync()`.
- Legacy SQLite DBs created by the current app can be opened/upgraded without data loss.
- Production startup and all test harnesses converge on the same bootstrap path.
- Migration design-time composition includes the full modular model.
- The execution report shows which migration/bootstrap tests covered SQLite and PostgreSQL.

## Proof Required

- `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~Migration|FullyQualifiedName~AppDbContextFactory"`
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~Migration|FullyQualifiedName~Legacy|FullyQualifiedName~Bootstrap"`
- Run a PostgreSQL-backed integration subset that proves migration/bootstrap on PostgreSQL.
- Record the legacy SQLite upgrade test name(s) and the PostgreSQL bootstrap evidence in the execution report.

## Browser Validation Logging

- `N/A` — no end-user UI should close here.
- If a browser harness needs update because startup now migrates through the new bootstrap path, record that under commands, not as UI proof.

## Progression Gate

- `EnsureCreatedAsync()` must be removed from the normal startup/test paths and migration/bootstrap proof must exist before subbundle 07 or 08 continues.
- Legacy SQLite upgrade proof must exist before claiming existing user data is safe.

## Suggested Agent Prompt

```text
Implement subbundle 04 only.

Move CanDoItAll to a migration-based schema path:
- provider-safe migration setup
- full model composition for design-time
- legacy SQLite baseline/upgrade
- unified bootstrap service
- update startup and test harnesses

Do not expose UI yet.
Run the migration/bootstrap tests and record SQLite + PostgreSQL evidence honestly.
```

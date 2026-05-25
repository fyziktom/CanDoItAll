# COPY-PASTE PROMPT FOR CODEX

You are a senior C#/.NET architect working in the CanDoItAll repository.

Use the repository-local bundle skills before making changes:

- `codex/skills/bundles/candoitall-bundle-preparation/SKILL.md`
- `codex/skills/bundles/candoitall-bundle-execution/SKILL.md`

Target repository and branch:

```text
repo: fyziktom/CanDoItAll
branch: development
```

## Mission

Remove SQLite completely from the **main CanDoItAll runtime** and make the main application persistence **PostgreSQL-only**.

The current SQLite support has become an architectural burden: duplicate migrations, slower builds, SQLite-specific runtime coordination, UI/profile complexity, extra test matrix, and limitations around process/workflow concurrency. We want to remove it now and optionally reintroduce a separate snapshot/export mechanism later.

## Important scope boundaries

Do not modify `CanDoItAll.IPFS`. Its local SQLite explorer index is a separate utility store and remains valid.

Do not preserve SQLite as a hidden compatibility provider.

Do not implement a new snapshot system in this task. Current SQLite-backed snapshot/materialization flows should be removed or explicitly deferred.

Do not assume that `InMemory` is a valid substitute for PostgreSQL integration tests.

Keep all source code comments in English.

## Required execution strategy

Execute this as dependency-aware subbundles. Do not perform all changes in one uncontrolled pass.

Required subbundles:

1. **SB01 - Remove SQLite Runtime Provider, Driver, Dependencies, and Migration Project**
2. **SB02 - PostgreSQL-Only Database Profile and Control-Plane Contract**
3. **SB03 - Remove SQLite UI and Dev Endpoints**
4. **SB04 - Convert Tests and Test Support Away From SQLite**
5. **SB05 - Remove General SQLite-Era Runtime Limitations**
6. **SB06 - Tune Processes, Workflows, Automation, and Outbox for PostgreSQL**
7. **SB07 - Remove or Explicitly Defer SQLite-Backed Database Snapshot Flows**
8. **SB08 - Consolidate PostgreSQL Migrations Into One Baseline**
9. **SB09 - Final Validation, Documentation, CI, and Anti-Stub Audit**

Dependency rule:

```text
SB01 -> SB02 -> SB03/SB04 -> SB05 -> SB06 -> SB07 -> SB08 -> SB09
```

Do not start SB06 until SB05 is complete, because general SQLite-era runtime limitations must be removed before making process/workflow-specific changes.

## Known current SQLite surfaces to inspect

Start with these files and expand using ripgrep:

```text
src/CanDoItAll.Infrastructure/Persistence/SwitchableAppDbContextFactory.cs
src/CanDoItAll.Infrastructure/ControlPlane/DatabaseDrivers.cs
src/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileModels.cs
src/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileControlPlaneService.cs
src/CanDoItAll.Infrastructure/ControlPlane/DatabaseSnapshots.cs
src/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs
src/CanDoItAll.Infrastructure/CanDoItAll.Infrastructure.csproj
src/CanDoItAll.Migrations.Sqlite/
src/CanDoItAll.Migrations.PostgreSql/
src/CanDoItAll.Web/Infrastructure/RuntimeDatabaseSwitching.cs
src/CanDoItAll.Web/Infrastructure/DatabaseMigrationBootstrap.cs
src/CanDoItAll.Web/Program.cs
src/CanDoItAll.Modules.Workspace/Pages/Components/DatabaseSourcesSettingsPanel.razor
src/CanDoItAll.Modules.Workspace/DatabaseProfileWorkspaceService.cs
CanDoItAll.slnx
tests/CanDoItAll.Tests.Support/TestDatabaseProviderKind.cs
tests/CanDoItAll.Tests.Support/TestDatabaseProfile.cs
tests/CanDoItAll.Tests.Support/CanDoItAllTestEnvironment.cs
tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj
```

Use these searches repeatedly:

```powershell
rg -n -i "sqlite|usesqlite|migrations\.sqlite|managedsqlite|externalsqlite|importedsqlite|sqlitewritecoordination|legacysqlitemigrationbootstrap|snapshotcache|ipfssnapshot" src tests docs
rg -n -i "providerkind|databaseprofile|databasesnapshot|runtime database|switching|migrationbootstrap" src tests
rg -n -i "single writer|single-writer|concurrency|claim|outbox|lease|worker|workflow|process" src tests
```

## SB01 expectations

Remove SQLite as a runtime provider:

- Remove `DatabaseProviderKind.Sqlite` usage from runtime configuration paths.
- Remove `UseSqlite(...)`.
- Remove SQLite migrations assembly constant.
- Remove `CanDoItAll.Migrations.Sqlite` project from solution/build.
- Remove `Microsoft.EntityFrameworkCore.Sqlite` package references from main runtime projects.
- Remove SQLite driver registration from infrastructure DI.
- Remove legacy SQLite migration bootstrap paths.
- Make PostgreSQL the default persistent provider.
- Keep `InMemory` only if it is needed for narrow unit tests or non-persistence-only scenarios, but do not expose it as a real runtime option unless already intentionally supported.

Expected proof:

```text
proof/SB01/manifest.md
proof/SB01/semantic-invariants.md
evidence/SB01/sqlite-runtime-audit.log
```

## SB02 expectations

Convert database profile/control-plane contract to PostgreSQL-only:

Remove or reject these main-runtime concepts:

```text
ManagedSqlite
ExternalSqliteFile
ImportedSqlite
SnapshotCache
IpfsSnapshot
SqliteDatabaseProfileConnection
SqliteDatabasePath
CreateManagedSqliteProfileLocked
TryCreateLegacyProfileLocked
TryResolveCatalogBackedSqliteOverrideLocked
BuildSqliteOverrideProfile
```

Do not leave stale profile catalog entries silently active. If an existing local catalog references SQLite, the app should fail with a clear migration/removal message rather than trying to recover SQLite.

Expected behavior:

- PostgreSQL profile creation/selection works.
- Existing SQLite profile catalog entries are treated as unsupported legacy entries.
- Runtime profile resolution never returns SQLite.
- Default local development setup is PostgreSQL.

## SB03 expectations

Remove SQLite UI and dev endpoints:

From `DatabaseSourcesSettingsPanel.razor`, remove:

```text
Managed SQLite
Open SQLite
SQLite source
SQLite file path
Materialized SQLite path
database-profile-new-managed
database-profile-new-external
database-profile-managed-sqlite-info
database-profile-sqlite-path
```

Remove any dev endpoint that creates managed SQLite profiles, especially:

```text
/_dev/database/profiles/managed-sqlite
```

Browser proof must show:

- SQLite actions absent.
- PostgreSQL profile flow present.
- Current profile display works.
- No broken UI empty state that still references SQLite.

## SB04 expectations

Convert tests and test support away from SQLite:

- Remove `TestDatabaseProviderKind.Sqlite`.
- Remove `CreateManagedSqliteProfile(...)`.
- Remove `Microsoft.Data.Sqlite` and EF SQLite package references from main tests unless a narrowly justified non-main-runtime test remains.
- Convert persistence/integration tests to PostgreSQL-backed profiles.
- Use Testcontainers or an existing PostgreSQL test fixture if available.
- Do not replace PostgreSQL integration behavior with `InMemory`.

Expected proof:

```text
dotnet test targeted persistence tests
rg audit with no main-runtime SQLite test residue
```

## SB05 expectations

Remove general SQLite-era runtime limitations before touching specific process/workflow logic.

Audit and improve:

- Runtime database switching.
- Context lease/drain behavior.
- Single-writer assumptions.
- Provider-neutral defensive limitations.
- Generic low-concurrency worker defaults that existed only because SQLite was supported.
- Any persistence abstraction that prevents use of PostgreSQL concurrency primitives.

Do not create process/workflow-specific logic yet. This phase is about removing general restrictions and preparing PostgreSQL-native primitives.

Recommended primitives to introduce where appropriate:

- PostgreSQL-backed leasing/claiming abstractions.
- Transaction-safe claim APIs.
- PostgreSQL retry policy for transient failures.
- Clear separation between runtime database switching and operational worker concurrency.

## SB06 expectations

Tune processes, workflows, automation, command outbox, and plugin execution for PostgreSQL.

Audit:

```text
src/CanDoItAll.Modules.Processes/**
src/CanDoItAll.Modules.Automation/**
src/CanDoItAll.Modules.Plugins/**
src/CanDoItAll.Infrastructure/BackgroundJobs/**
src/CanDoItAll.Infrastructure/Persistence/**
```

Use PostgreSQL-appropriate concurrency behavior where useful:

```sql
FOR UPDATE SKIP LOCKED
```

or equivalent transaction-safe claim logic through EF/Npgsql.

Add tests proving:

- Concurrent workers do not double-claim the same workflow/outbox work.
- Retries are idempotent.
- Leases expire/recover correctly.
- Process/workflow throughput is not artificially serialized by former SQLite constraints.

## SB07 expectations

Remove or explicitly defer SQLite-backed database snapshot flows.

Current snapshot/materialization logic should not keep SQLite in the main runtime. Remove or explicitly disable/defer:

```text
SnapshotCache
IpfsSnapshot
SQLite materialized snapshot profiles
SQLite clone database profiles
restore paths using SQLite-specific PRAGMA behavior
```

Add a future-work note:

```text
Future snapshots should be implemented as a separate bounded context or portable export/import package, not as a main AppDbContext runtime provider.
```

Do not implement the future snapshot system now.

## SB08 expectations

Consolidate PostgreSQL migrations into one baseline after model stabilization.

Steps:

1. Capture the current PostgreSQL migration inventory.
2. Confirm no SQLite model branches remain.
3. Delete old PostgreSQL migrations only after the model is stable.
4. Generate one clean PostgreSQL baseline migration.
5. Validate fresh PostgreSQL database creation from zero.
6. Validate representative app startup and persistence flows.
7. Provide manual guidance for the user's one real PostgreSQL database.

Do not promise automatic migration of the user's real database unless you actually implement and test a transition script.

## SB09 expectations

Final validation:

Run at least:

```powershell
dotnet build .\CanDoItAll.slnx
dotnet test .\CanDoItAll.slnx --filter "Category!=Browser&Category!=LiveProcess"
rg -n -i "sqlite|usesqlite|migrations\.sqlite|managedsqlite|externalsqlite|importedsqlite|sqlitewritecoordination|legacysqlitemigrationbootstrap|snapshotcache|ipfssnapshot" src tests docs
```

Browser/UI proof:

- Workspace/Data Sources page does not mention SQLite.
- PostgreSQL creation/selection still works.
- Runtime current profile display works.

Migration proof:

- Fresh PostgreSQL DB can be created from the new single baseline.
- App can start against that DB.
- Representative process/workflow persistence path works.

Anti-stub proof:

- No placeholder services were added.
- No TODO-only replacement for removed SQLite behavior.
- No catch-all fallback that silently ignores unsupported SQLite profiles.
- No test was weakened from PostgreSQL integration behavior to `InMemory` just to make it pass.

## Required final output

At the end, write an execution report with:

```text
reviews/01-execution-report.md
proof/SBxx/manifest.md for every subbundle
proof/SBxx/semantic-invariants.md for every subbundle
evidence/ logs for build, tests, grep audits, UI proof, migration proof
manual-real-db-alignment.md
```

The final summary must explicitly state:

- SQLite was removed from the main CanDoItAll runtime.
- CanDoItAll.IPFS was not modified.
- PostgreSQL is the only persistent runtime provider.
- Snapshot functionality was removed/deferred, not silently preserved through SQLite.
- PostgreSQL migrations were consolidated into one baseline, or the reason if consolidation was intentionally deferred.

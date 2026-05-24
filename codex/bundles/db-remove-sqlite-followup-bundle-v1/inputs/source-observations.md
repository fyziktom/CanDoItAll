# Source observations from branch review

This file summarizes the reviewed repository evidence. Use it as a working checklist, not as a substitute for re-running `rg`, build, tests, and browser proof locally.

## Branch relationship

- `db-remove-sqlite` is ahead of `development` by 2 commits and behind by 0.
- The branch contains a very large removal of old PostgreSQL migrations and all SQLite migrations.
- The branch adds `codex/bundles/postgresql-only-main-runtime-bundle-v1`.

## Completed items observed

### SQLite EF/package/project removal

Observed:
- `CanDoItAll.slnx` no longer references `src/CanDoItAll.Migrations.Sqlite`.
- `src/CanDoItAll.Infrastructure/CanDoItAll.Infrastructure.csproj` no longer references `Microsoft.EntityFrameworkCore.Sqlite`.
- `src/CanDoItAll.Infrastructure/Persistence/SqliteWriteCoordination.cs` was removed.
- `src/CanDoItAll.Infrastructure/ControlPlane/DatabaseDrivers.cs` now registers only `InMemoryDatabaseDriver` and `PostgreSqlDatabaseDriver`.

### PostgreSQL default

Observed:
- `DatabaseOptions.Provider` default is now `PostgreSql`.
- `AppDbContextFactory` default provider is now `PostgreSql`.
- `Program.cs` still exposes development endpoints for PostgreSQL profile creation and switching, but no managed SQLite creation endpoint was observed.

### Test support

Observed:
- `TestDatabaseProviderKind` now contains `PostgreSql` and `InMemory`, not SQLite.
- `CanDoItAllTestEnvironment` creates PostgreSQL test databases through `PostgresTestDatabaseLease`.
- Integration test project no longer references `Microsoft.Data.Sqlite`.

### Snapshot deferral

Observed:
- `DatabaseSnapshotService` returns a deferred failure for create/clone/materialize.
- Snapshot-specific runtime materialization into SQLite appears removed.

### Baseline migration

Observed:
- Single baseline migration `20260523211921_InitialPostgreSqlBaseline` exists.
- Old PostgreSQL migrations appear removed.

## Remaining issues observed

### SQLite still exists in the core model

`DatabaseProfileModels.cs` still contains:
- `DatabaseProviderKind.Sqlite`,
- `DatabaseProfileSourceKind.ManagedSqlite`,
- `ExternalSqliteFile`,
- `ImportedSqlite`,
- `SnapshotCache`,
- `IpfsSnapshot`,
- `SqliteDatabaseProfileConnection`,
- `DatabaseProfileEditorModel.SqliteDatabasePath`.

This is not a full removal. It is a legacy rejection/display model.

### Control-plane still contains SQLite branches

`DatabaseProfileControlPlaneService.cs` still:
- validates `DatabaseProviderKind.Sqlite` with an error,
- blocks activation of SQLite profiles,
- throws for SQLite in `BuildPersistedProfile`,
- throws for SQLite in `BuildResolvedProfile`,
- returns "Unsupported legacy SQLite profile" in `BuildDescriptor`,
- throws in `BuildFingerprint`,
- has InMemory profile persistence support using `SqliteDatabasePath` as its database name field.

### Startup resolver can still be bricked by legacy catalog

`DatabaseProfileStartupConnectionResolver.cs` still:
- includes SQLite branches,
- throws for a persisted SQLite active profile,
- reads persisted profile catalog before runtime is fully available.

If a user's existing `catalog.json` still contains an active SQLite profile, startup can fail before the Data Sources UI can be used to remove or replace it.

### UI still has legacy SQLite branch

`DatabaseSourcesSettingsPanel.razor` no longer exposes new SQLite actions, but it still contains:

```razor
@if (databaseProfileModel.ProviderKind == DatabaseProviderKind.Sqlite)
{
    <FormSection Title="Unsupported legacy profile" ...>
```

This means SQLite remains a UI concern. The user requested removal from main CanDoItAll, not a legacy profile UX.

### Snapshot runtime stubs still exist

`IDatabaseSnapshotService`, snapshot request/result models, transport enum, and deferred runtime service remain in `DatabaseSnapshots.cs`. This is acceptable only if intentionally kept as a "future feature disabled" placeholder. For a real cleanup, remove it from runtime DI/UI/API or move it to explicit future documentation.

### PostgreSQL runtime tuning not proven

The branch report claims process/workflow runtime work passed, but reviewed diff evidence did not clearly show PostgreSQL-specific claim/locking patterns such as `FOR UPDATE SKIP LOCKED` or equivalent Npgsql transactional claim logic in process/workflow/outbox services. Add a dedicated audit and implementation pass rather than relying on generic build/test success.

### Stale or unrelated branch changes

The root `01-execution-report.md` lists files and tests unrelated to this SQLite removal. The branch also adds `.codex/bundles/project-structure-workflow-runs/proof/...` synthetic email/scenario proof artifacts. Verify whether these are intentionally part of this branch. If not, move them to the correct branch or remove them.

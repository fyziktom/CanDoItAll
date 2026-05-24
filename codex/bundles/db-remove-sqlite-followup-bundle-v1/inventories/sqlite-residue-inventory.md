# SQLite residue inventory for follow-up

## Known remaining runtime residues on `db-remove-sqlite`

### `src/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileModels.cs`

Remove:
- `DatabaseProviderKind.Sqlite`
- `DatabaseProfileSourceKind.ManagedSqlite`
- `DatabaseProfileSourceKind.ExternalSqliteFile`
- `DatabaseProfileSourceKind.ImportedSqlite`
- `DatabaseProfileSourceKind.SnapshotCache`
- `DatabaseProfileSourceKind.IpfsSnapshot`
- `SqliteDatabaseProfileConnection`
- `DatabaseProfileRecord.Sqlite`
- `DatabaseProfileEditorModel.SqliteDatabasePath`

Review:
- whether `DatabaseProfileStorageMode.ManagedPerProfile` is still needed for PostgreSQL-only profiles,
- whether `Clone` metadata is still useful after snapshot removal,
- whether `InMemory` should be persisted or test-only.

### `src/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileControlPlaneService.cs`

Remove:
- SQLite validation branch,
- SQLite activation branch,
- SQLite persisted-profile build branch,
- SQLite resolved-profile branch,
- SQLite descriptor/fingerprint branches.

Add:
- legacy catalog raw JSON quarantine before typed deserialization.

### `src/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileStartupConnectionResolver.cs`

Remove:
- SQLite provider parsing and branches.

Add:
- legacy catalog quarantine or at least skip legacy profiles safely.

### `src/CanDoItAll.Infrastructure/Persistence/SwitchableAppDbContextFactory.cs`

Remove:
- SQLite case that throws.
- String provider `"sqlite"` branch that throws can be replaced by generic unsupported-provider validation if desired.

### `src/CanDoItAll.Modules.Workspace/Pages/Components/DatabaseSourcesSettingsPanel.razor`

Remove:
- `DatabaseProviderKind.Sqlite` conditional UI.
- "Unsupported legacy profile" section.
- Any `SqliteDatabasePath` binding.
- Snapshot deferred section unless intentionally documented in a separate non-runtime settings/help page.
- Persisted InMemory profile UI unless deliberately kept.

### `src/CanDoItAll.Infrastructure/ControlPlane/DatabaseSnapshots.cs`

Remove or isolate:
- snapshot transport enum,
- manifest/payload models,
- clone/materialization request/result models,
- `DatabaseSnapshotService`,
- `IDatabaseSnapshotService` from models/interfaces if unused.

### Tests

Run and enforce:

```powershell
rg -n -i "sqlite|usesqlite|migrations\.sqlite|managedsqlite|externalsqlite|importedsqlite|snapshotcache|ipfssnapshot|sqlitewritecoordination" src tests *.slnx
```

Expected:
- no matches in main runtime source/tests after SB04,
- docs/bundle files may contain historical review text only.

## Known completed removals

Already removed:
- `src/CanDoItAll.Migrations.Sqlite`
- `src/CanDoItAll.Infrastructure/Persistence/SqliteWriteCoordination.cs`
- `Microsoft.EntityFrameworkCore.Sqlite` package from Infrastructure.

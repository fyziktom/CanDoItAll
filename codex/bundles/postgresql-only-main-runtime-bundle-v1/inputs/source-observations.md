# Source observations from `development`

This file lists observed SQLite-related surfaces that Codex should verify in the actual working tree before editing.

## Runtime EF configuration

Observed file:

```text
src/CanDoItAll.Infrastructure/Persistence/SwitchableAppDbContextFactory.cs
```

Observed concerns:

- Contains `AppDbContextMigrationsAssemblyNames.Sqlite`.
- Contains `DatabaseProviderKind.Sqlite` branch.
- Calls `UseSqlite(...)`.
- Normalizes SQLite connection strings.
- Adds SQLite write coordination connection interceptor.
- Design-time default provider currently falls back to `Sqlite`.

## Driver registration

Observed file:

```text
src/CanDoItAll.Infrastructure/ControlPlane/DatabaseDrivers.cs
```

Observed concerns:

- Contains `SqliteDatabaseDriver`.
- Contains SQLite profile connection validation.
- Contains SQLite fingerprint/path logic.
- Driver registry includes SQLite driver.

## Control-plane models

Observed file:

```text
src/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileModels.cs
```

Observed concerns:

- Contains `DatabaseProviderKind.Sqlite`.
- Contains SQLite-oriented source kinds:
  - `ManagedSqlite`
  - `ExternalSqliteFile`
  - `ImportedSqlite`
  - `SnapshotCache`
  - `IpfsSnapshot`
- Contains SQLite connection model fields.

## Control-plane service

Observed file:

```text
src/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileControlPlaneService.cs
```

Observed concerns:

- Contains managed SQLite creation.
- Contains legacy SQLite profile discovery.
- Contains SQLite catalog override resolution.
- Contains provider parse fallback to SQLite.
- Contains SQLite path resolution/materialization helpers.

## Snapshot service

Observed file:

```text
src/CanDoItAll.Infrastructure/ControlPlane/DatabaseSnapshots.cs
```

Observed concerns:

- Materialized snapshots create SQLite runtime profiles.
- Snapshot cache and IPFS snapshot profile source kinds are SQLite-coupled.
- Restore logic has SQLite-specific PRAGMA operations.

## UI

Observed file:

```text
src/CanDoItAll.Modules.Workspace/Pages/Components/DatabaseSourcesSettingsPanel.razor
```

Observed concerns:

- UI has managed SQLite and open SQLite actions.
- Empty state references SQLite-first onboarding.
- Editor has SQLite path/source fields.

## Web/dev endpoints

Observed file:

```text
src/CanDoItAll.Web/Program.cs
```

Observed concerns:

- Dev endpoint creates managed SQLite profile.
- Startup may contain runtime switching bootstrap that assumes SQLite profile availability.

## Tests

Observed files:

```text
tests/CanDoItAll.Tests.Support/TestDatabaseProviderKind.cs
tests/CanDoItAll.Tests.Support/TestDatabaseProfile.cs
tests/CanDoItAll.Tests.Support/CanDoItAllTestEnvironment.cs
tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj
```

Observed concerns:

- Test provider kind includes SQLite.
- Test support creates managed SQLite database profiles.
- Test environment cleans SQLite pools.
- Integration tests reference `Microsoft.Data.Sqlite`.

## Solution/migration projects

Observed files:

```text
CanDoItAll.slnx
src/CanDoItAll.Migrations.Sqlite/
src/CanDoItAll.Migrations.PostgreSql/
```

Observed concerns:

- SQLite migration project is included in solution.
- PostgreSQL and SQLite migrations are maintained separately.
- PostgreSQL migrations should be consolidated only after SQLite runtime branches are removed.

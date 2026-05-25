# Target architecture after follow-up

## Main persistence

The main CanDoItAll runtime has exactly one persistent provider:

```text
Main runtime persistent DB: PostgreSQL
```

`InMemory` may remain only as:
- an explicit test harness provider, or
- an explicit development override if the team deliberately wants it.

It must not be presented as a normal persisted runtime database source in the Data Sources UI.

## Profile model

Target profile model:

```csharp
public enum DatabaseProviderKind
{
    PostgreSql
    // Optional: InMemory only if deliberately kept as test/dev override.
}

public enum DatabaseProfileSourceKind
{
    PostgresConnection
    // Optional: InMemory only if deliberately kept as test/dev override.
}
```

There is no SQLite connection record and no SQLite editor field.

## Legacy catalog strategy

Do not keep SQLite in the new typed model. Use a raw JSON pre-scan:

1. Read catalog as `JsonDocument`.
2. Detect legacy entries where:
   - `providerKind == "Sqlite"`,
   - `sourceKind` is `ManagedSqlite`, `ExternalSqliteFile`, `ImportedSqlite`, `SnapshotCache`, `IpfsSnapshot`,
   - `sqlite` object exists,
   - `sqliteDatabasePath` exists.
3. Move legacy entries/document to quarantine artifact:
   - `control-plane/database-profiles/legacy-sqlite-quarantine/<timestamp>-catalog.json`
   - optionally `<timestamp>-active-profile.json`
4. Build a clean catalog containing only PostgreSQL profiles.
5. If no PostgreSQL profile remains, create the default PostgreSQL profile.
6. If active profile was removed, point active state to the selected PostgreSQL profile.
7. Log a clear warning.

## Snapshot strategy

Current branch already defers snapshots. After follow-up, snapshots should be either:
- fully removed from runtime and documented as future work, or
- isolated as a future feature not connected to `DatabaseProfileSourceKind`.

Future reimplementation should be a portable export/import workflow, not a database provider.

## PostgreSQL runtime primitives

After SQLite removal, process/workflow/outbox execution should prefer PostgreSQL-native patterns:
- transaction-scoped claiming,
- row-level locks,
- `FOR UPDATE SKIP LOCKED`,
- idempotency keys,
- optimistic concurrency tokens where domain-level conflict detection is needed,
- advisory locks only for coarse-grained singleton operations.

Avoid provider-neutral patterns that were only kept for SQLite.

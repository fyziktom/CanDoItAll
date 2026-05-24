# SB01 - Hard-remove SQLite domain model and add legacy catalog quarantine

## Goal

Remove SQLite from the typed main runtime model completely and prevent old catalog entries from bricking startup.

## Context

The first Codex pass left SQLite as a legacy enum/profile state. That keeps SQLite in the runtime model and creates dangerous startup failure paths when a legacy SQLite profile is active.

## Required changes

1. Remove `DatabaseProviderKind.Sqlite`.
2. Remove SQLite source kinds: `ManagedSqlite`, `ExternalSqliteFile`, `ImportedSqlite`, `SnapshotCache`, `IpfsSnapshot`.
3. Remove `SqliteDatabaseProfileConnection`.
4. Remove `DatabaseProfileRecord.Sqlite`.
5. Remove `DatabaseProfileEditorModel.SqliteDatabasePath`.
6. Remove SQLite branches from:
   - `DatabaseProfileControlPlaneService`
   - `DatabaseProfileStartupConnectionResolver`
   - `SwitchableAppDbContextFactory`
7. Add a raw JSON legacy catalog quarantine step before typed catalog deserialization.
8. When quarantining legacy SQLite profiles, preserve a timestamped backup and select/create a PostgreSQL profile.
9. Ensure active-profile state is rewritten if it pointed to a quarantined profile.
10. Log operator-friendly messages with the quarantine path.

## Validation

- Build succeeds.
- Unit tests cover:
  - empty catalog -> default PostgreSQL profile,
  - valid PostgreSQL catalog -> unchanged,
  - legacy SQLite-only catalog -> quarantined and default PostgreSQL created,
  - mixed catalog -> SQLite entries quarantined and PostgreSQL entries retained,
  - active profile points to removed SQLite -> active state reset safely.
- `rg` has no SQLite matches in control-plane runtime code.

## Proof artifacts

Write:

- `proof/SB01/manifest.md`
- `proof/SB01/semantic-invariants.md`
- relevant logs under `evidence/SB01/`

## Acceptance criteria

- No SQLite enum value exists in runtime model.
- Old SQLite catalog JSON cannot crash startup.
- Legacy entries are not silently lost; they are backed up/quarantined.

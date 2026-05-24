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

## Status

- Completed

## Objective

Remove retired provider/source values from the typed runtime database model and quarantine legacy catalog JSON before typed deserialization.

## Covered Inputs

- `bundle://requirements/01-followup-requirements.md`

## Prerequisites

- Repository builds after model changes.

## Exact Source References

- `repo://src/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileModels.cs`
- `repo://src/CanDoItAll.Infrastructure/ControlPlane/LegacyDatabaseProfileCatalogQuarantine.cs`

## Deliverables

- Runtime model cleanup.
- Legacy catalog quarantine.
- Unit coverage for retained and quarantined catalogs.

## Dependency Impact

- Control-plane, startup resolver, workspace service, and Data Sources UI consume the reduced provider model.

## Validation Depth

- Unit tests, build, and residue audit.

## Implementation Steps

- Remove retired typed values and branches.
- Add raw JSON quarantine before typed catalog reads.
- Update tests around profile resolution.

## Do Not Do

- Do not silently keep legacy runtime support behind typed provider values.

## Acceptance Checklist

- Runtime model has no retired provider enum value.
- Legacy catalog JSON is backed up and quarantined.
- Active selection is reset when it references a quarantined profile.

## Proof Required

- `bundle://proof/SB01/manifest.md`

## Browser Validation Logging

- No browser route is required for this subbundle.

## Progression Gate

- Build and unit proof must pass before SB02/SB04 closure.

## Suggested Agent Prompt

Implement SB01, then run the unit and residue proof commands in `proof/SB01/manifest.md`.

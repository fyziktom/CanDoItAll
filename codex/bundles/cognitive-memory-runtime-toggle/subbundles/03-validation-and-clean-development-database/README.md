# Validation And Clean Development Database

## Status

- `Completed`

## Objective

Validate the implementation and leave `candoitall_development` as a clean migrated PostgreSQL database for manual demo testing.

## Success Criteria

- Targeted tests/build proof is recorded.
- Prepared/completed bundle validation status is recorded.
- Development PostgreSQL database is reset and migrated.
- Raw notes are closed note by note in the execution report.

## Covered Inputs

- `N007`
- Requirements: `R008`

## Prerequisites

- SB01 and SB02 closure gates passed.

## Exact Source References

- `repo://src/CanDoItAll.Web/appsettings.Development.json`
- `repo://src/CanDoItAll.Migrations.PostgreSql/CanDoItAll.Migrations.PostgreSql.csproj`
- `repo://src/CanDoItAll.Migrations.Sqlite/CanDoItAll.Migrations.Sqlite.csproj`
- `repo://codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py`

## Deliverables

- Test/build transcript.
- Database reset/migration transcript.
- Proof manifests and semantic invariants.
- Updated execution report and raw-note closure table.

## Dependency Impact

- This subbundle is the final user-facing readiness gate.
- If the clean database cannot be prepared, the user cannot reliably retest the full demo flow.

## Validation Depth

- `End-to-end regression and closure`

## Implementation Steps

1. Run targeted unit tests for settings and runtime guards.
2. Run a build or broader test command if targeted proof leaves compile risk.
3. Reset `candoitall_development` PostgreSQL using safe, explicit database target checks.
4. Apply PostgreSQL migrations.
5. Update proof manifests, semantic invariants, execution report, and raw-note closure.
6. Run bundle validator for completed state, or document explicit validator limitations.

## Scope Exceptions

- No production or non-development database reset is allowed.

## Do Not Do

- Do not run destructive database commands without verifying the resolved target database is exactly `candoitall_development`.
- Do not reset user data outside the development PostgreSQL database named in `appsettings.Development.json`.

## Acceptance Checklist

- [x] Targeted tests pass or failures are documented as unrelated blockers.
- [x] Database reset targets only `candoitall_development`.
- [x] Migrations apply after reset.
- [x] Execution report records raw-note closure.

## Proof Required

- `proof/SB03/manifest.md`
- Command transcript for tests/build.
- Command transcript for PostgreSQL reset/migration.
- Changed-file hashes.
- Raw-note closure table updated to `Solved`, `Partially solved`, or `Not solved`.

## Browser Validation Logging

- Optional settings UI proof may be recorded here if the app host starts cleanly after database reset.

## Progression Gate

- Final closure only if validation and database reset proof are present or an explicit blocker is recorded.

## Suggested Agent Prompt

```text
Implement SB03 only after SB01 and SB02. Run targeted validation, reset only the configured development PostgreSQL database, apply migrations, and close the bundle with proof artifacts and raw-note closure.
```

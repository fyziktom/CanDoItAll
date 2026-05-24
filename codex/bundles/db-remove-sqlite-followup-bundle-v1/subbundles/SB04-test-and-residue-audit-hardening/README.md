# SB04 - Test and residue audit hardening

## Goal

Make SQLite residue impossible to reintroduce accidentally.

## Context

The first pass updated test support, but current source still contains runtime SQLite residues. Add durable tests/scripts.

## Required changes

1. Add a repository residue audit script under the bundle/evidence tooling.
2. Add unit tests for legacy catalog quarantine.
3. Add component tests for PostgreSQL-only Data Sources UI.
4. Add negative tests that `Database:Provider=sqlite` fails with a generic unsupported-provider error and no SQLite-specific runtime branch.
5. Ensure `Microsoft.Data.Sqlite` and `Microsoft.EntityFrameworkCore.Sqlite` do not appear in main CanDoItAll projects/tests.
6. Keep InMemory tests explicit and not as a normal runtime profile unless justified.

## Validation

Run:
```powershell
rg -n -i "sqlite|usesqlite|migrations\.sqlite|managedsqlite|externalsqlite|importedsqlite|snapshotcache|ipfssnapshot|sqlitewritecoordination" src tests *.slnx
dotnet test .\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-build -v:minimal
dotnet test .\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --no-build -v:minimal
```


## Proof artifacts

Write:

- `proof/SB04/manifest.md`
- `proof/SB04/semantic-invariants.md`
- relevant logs under `evidence/SB04/`

## Acceptance criteria

- Residue audit fails if SQLite appears in runtime code/tests.
- Tests prove old catalogs are handled safely.

## Status

- Completed

## Objective

Make retired-provider residue hard to reintroduce in runtime source, tests, and the solution file.

## Covered Inputs

- `bundle://requirements/01-followup-requirements.md`

## Prerequisites

- SB01 through SB03 cleanup completed.

## Exact Source References

- `bundle://scripts/sqlite_residue_audit.ps1`
- `repo://tests/CanDoItAll.Tests.Unit/DatabaseProfileControlPlaneTests.cs`

## Deliverables

- Windows-safe residue audit script.
- Unit and component regression tests.
- Generic unsupported-provider test coverage.

## Dependency Impact

- The audit protects main runtime, tests, and solution metadata from residue regressions.

## Validation Depth

- Residue command, unit tests, and Data Sources component tests.

## Implementation Steps

- Fix audit script scope/globbing.
- Add quarantine and unsupported-provider tests.
- Update UI tests to assert absence of retired-provider controls.

## Do Not Do

- Do not keep stringly typed provider branches as compatibility paths.

## Acceptance Checklist

- Residue audit exits successfully with no matches.
- Unit tests pass.
- Data Sources component tests pass.

## Proof Required

- `bundle://proof/SB04/manifest.md`

## Browser Validation Logging

- Browser absence checks are covered by SB02/SB08 proof.

## Progression Gate

- Audit and focused tests must pass before final validation.

## Suggested Agent Prompt

Implement SB04, then run the residue, unit, and component proof commands in `proof/SB04/manifest.md`.

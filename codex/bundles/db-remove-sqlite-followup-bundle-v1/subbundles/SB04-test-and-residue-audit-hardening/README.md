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

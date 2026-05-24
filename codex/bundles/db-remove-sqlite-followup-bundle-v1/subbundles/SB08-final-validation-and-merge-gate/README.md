# SB08 - Final validation and merge gate

## Goal

Produce merge-ready evidence that SQLite is gone from main runtime and PostgreSQL-only operation is stable.

## Context

This is the final proof pass after all cleanup/tuning.

## Required changes

1. Run restore/build/tests.
2. Run residue audit.
3. Run PostgreSQL fresh DB baseline proof.
4. Run browser proof for Data Sources UI.
5. Run concurrency/process workflow tests added in SB06.
6. Update final execution report and manual DB alignment notes.

## Validation

Required:
```powershell
dotnet restore .\CanDoItAll.slnx
dotnet build .\CanDoItAll.slnx -m:1 -v:minimal
dotnet test .\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-build -v:minimal
dotnet test .\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --no-build -v:minimal
dotnet test .\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter "Category!=Browser&Category!=LiveProcess" -v:minimal
rg -n -i "sqlite|usesqlite|migrations\.sqlite|managedsqlite|externalsqlite|importedsqlite|snapshotcache|ipfssnapshot|sqlitewritecoordination" src tests *.slnx
```

Browser proof:
- open Settings/Data Sources,
- create/save/test PostgreSQL profile,
- verify no SQLite UI,
- verify no snapshot runtime controls,
- verify current runtime selection displays PostgreSQL.


## Proof artifacts

Write:

- `proof/SB08/manifest.md`
- `proof/SB08/semantic-invariants.md`
- relevant logs under `evidence/SB08/`

## Acceptance criteria

- All gates pass.
- Final report clearly states any allowed residue.
- Branch is ready for human review/merge.

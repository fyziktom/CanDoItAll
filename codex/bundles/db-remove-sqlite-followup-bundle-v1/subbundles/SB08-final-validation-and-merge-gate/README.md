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

## Status

- Completed

## Objective

Run final validation and record merge-gate evidence for the PostgreSQL-only runtime follow-up.

## Covered Inputs

- `bundle://requirements/01-followup-requirements.md`

## Prerequisites

- SB01 through SB07 completed.

## Exact Source References

- `bundle://proof/SB08/manifest.md`
- `bundle://reviews/01-execution-report.md`

## Deliverables

- Restore/build/unit/component/integration/browser/residue proof.
- Final execution report.
- Residual risk notes.

## Dependency Impact

- Confirms all upstream subbundle work remains coherent after cleanup.

## Validation Depth

- Full final command set with focused component/browser slices and non-quarantined integration tests.

## Implementation Steps

- Run final validation commands.
- Capture evidence logs.
- Update proof manifests and execution report.

## Do Not Do

- Do not claim unrelated failing tests are fixed by this bundle.

## Acceptance Checklist

- Restore, build, unit, in-scope component, integration, browser, residue, and diff-check gates pass.
- Residual risks are documented.
- Proof manifests are no longer placeholders.

## Proof Required

- `bundle://proof/SB08/manifest.md`

## Browser Validation Logging

- Playwright evidence is recorded under `bundle://evidence/SB08`.

## Progression Gate

- Human review can start after final proof and residual-risk review.

## Suggested Agent Prompt

Implement SB08, then run the final validation command set and update `reviews/01-execution-report.md`.

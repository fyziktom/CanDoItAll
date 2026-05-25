# Phase Plan

## Subbundle Dependency Map

```mermaid
gantt
title Processes Hardening Follow-up V2
dateFormat  YYYY-MM-DD
section Contract Foundation
SB01 explicit operation contract :crit, sb01, 2026-05-25, 1d
SB02 tool policy enforcement :crit, sb02, after sb01, 1d
section Lineage and Projection
SB03 recovery lineage :crit, sb03, after sb02, 1d
SB04 workflow subprocess adapters :crit, sb04, after sb03, 1d
section Runtime Resilience
SB05 upstream unblock lifecycle :crit, sb05, after sb04, 1d
SB06 disposition guardrails :crit, sb06, after sb04, 1d
SB07 artifact validation tuning :crit, sb07, after sb03, 1d
SB08 retry/adoption hardening :crit, sb08, after sb07, 1d
section Definition Quality
SB09 lint integration :sb09, after sb01, 1d
section Closure
SB10 red-team validation :crit, sb10, after sb05, 1d
```

## Critical Subbundles

All subbundles are critical except SB09 can be executed slightly later if runtime fixes are urgent. SB09 is still required before closure.

## Phase Gates

### Prepared Gate

Run after placing this bundle in the repo:

```powershell
python codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py --stage prepared codex/bundles/processes-hardening-followup-runtime-resilience-v2
```

Also run the repository bundle validator if available:

```powershell
# Use the CanDoItAll bundle validator skill/tool if installed.
```

### Per-Subbundle Closure Gate

Each subbundle must provide:

- source assertions
- failing-first or red-team proof
- passing proof
- anti-stub audit
- changed-file hashes
- focused tests
- build proof when production code changed
- PostgreSQL-only confirmation when data model changes are introduced

### Final Closure Gate

Run:

```powershell
dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests|FullyQualifiedName~ProcessDefinitionLinterTests"
dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore
dotnet build CanDoItAll.slnx --no-restore
rg -n "Sqlite|SQLite|UseSqlite|Migrations.Sqlite" src tests codex/bundles/processes-hardening-followup-runtime-resilience-v2 -S
```

If UI start/publish surfaces are changed by SB09, add component/browser proof for the lint panel and process start warning.

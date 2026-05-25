# Phase Plan

## Subbundle Dependency Map

```mermaid
gantt
title Runtime Integrity Follow-up Dependency Map
dateFormat  YYYY-MM-DD
section Transaction/Lineage Foundation
SB01 materialization reactivation transaction :crit, sb01, 2026-05-25, 1d
SB02 lineage keys and provenance schema :crit, sb02, after sb01, 1d
section Boundary Enforcement
SB03 script tool boundary policy :crit, sb03, after sb02, 1d
SB04 typed grounding alias trust :crit, sb04, after sb03, 1d
section Artifact Semantics
SB05 storage-backed artifact validation :crit, sb05, after sb02, 1d
SB06 workflow subprocess mapping :crit, sb06, after sb05, 1d
SB07 disposition ownership guardrails :crit, sb07, after sb06, 1d
section Contract/Retry/Quality Gates
SB08 persisted operation contract :crit, sb08, after sb04, 1d
SB09 durable no-progress ledger :crit, sb09, after sb08, 1d
SB10 lint gates and red-team closure :crit, sb10, after sb09, 1d
```

## Critical Subbundles

All subbundles are critical. SB01 and SB02 are the foundation: do not start downstream work if materialization or lineage remains unreliable.

## Phase Gates

### Prepared gate

```powershell
python codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py --stage prepared codex/bundles/processes-hardening-followup-runtime-integrity-v4
```

### Focused runtime tests

```powershell
dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests|FullyQualifiedName~ProcessDefinitionLinterTests"
```

### Focused tool policy tests

```powershell
dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~AgentToolInvocationPolicyTests|FullyQualifiedName~AgentWorkspaceToolAccessMetadataTests"
```

### Full confirmation

```powershell
dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore
dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore
dotnet build CanDoItAll.slnx --no-restore
```

### PostgreSQL-only audit

```powershell
rg -n "Sqlite|SQLite|UseSqlite|Migrations.Sqlite" src tests codex/bundles/processes-hardening-followup-runtime-integrity-v4 -S
```

Expected: no new SQLite runtime or migration dependency introduced.

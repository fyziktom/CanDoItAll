# ACR-001 — Persisted system-managed workbench graph acts as a parallel truth

- Severity: **Critical**
- Skill source: `canonical-model-review`
- Category: Source-of-truth drift
- Phase: **Phase 2**
- Timing: **Now**
- Dependencies: Depends on ACR-003, ACR-004, ACR-011, and the actor-assignment ownership work in ACR-012/ACR-013. Strongly coupled with ACR-006 and ACR-009.

## Problem statement

ProjectWorkbenchService still synchronizes Projects, Resources, Factory, Validation, and TestLab canonical entities into persisted ProjectObjectRecord / ProjectObjectLinkRecord rows, and structure/calendar/Gantt reads still flow through that synced copy. The new CRM/HR overlays would now stack on top of a graph that is already a parallel truth.

## Why this matters now

Every new CRM/HR-linked responsibility, schedule, or agent action will compound the cost of keeping a second persisted graph truthful.

## Deliverables

- CanonicalGraphAssembler (or equivalent) with explicit AssembledProjectGraph output
- Clear distinction between upstream canonical nodes and workbench-owned custom nodes/facets
- Read surfaces updated so structure/calendar/Gantt no longer rely on persisted system-managed graph rows as truth
- Optional cache explicitly marked non-authoritative if retained

## Likely files touched

- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectGanttPreviewService.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectStructureSummaryBuilder.cs`
- `src/CanDoItAll.Modules.Projects/*`
- `src/CanDoItAll.Modules.Resources/*`
- `src/CanDoItAll.Modules.Factory/*`
- `src/CanDoItAll.Modules.Validation/*`
- `src/CanDoItAll.Modules.TestLab/*`
- `tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs`

# ACR-006 — Calendar and Gantt are projections over a persisted projection

- Severity: **High**
- Skill source: `canonical-model-review`
- Category: Projection drift
- Phase: **Phase 2**
- Timing: **Before next feature wave**
- Dependencies: Depends on ACR-001, ACR-003, ACR-004, and ACR-011.

## Problem statement

Calendar and Gantt do not build directly from canonical owners; they depend on workbench structure output that itself depends on SyncGraphAsync. CRM/HR-linked scheduling and ownership overlays would therefore be computed over a projection chain rather than one graph truth.

## Why this matters now

Time semantics and critical-path reasoning should not depend on chained projections once CRM/HR ownership overlays and note evolution become central.

## Deliverables

- StructureProjectionBuilder
- CalendarProjectionBuilder
- Timeline/GanttProjectionBuilder
- Projection equivalence tests over one assembled graph

## Likely files touched

- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectGanttPreviewService.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectStructureSummaryBuilder.cs`
- `tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs`

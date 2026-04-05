# ACR-014 — Node reclassification and typed lifecycle history are insufficient for note→task/decision evolution

- Severity: **High**
- Skill source: `feature-block-architecture-review`
- Category: Lifecycle drift
- Phase: **Phase 2**
- Timing: **Before next feature wave**
- Dependencies: Depends on ACR-003, ACR-008, ACR-011, and should coordinate with ACR-012.

## Problem statement

The product workflow starts with fast brainstorming notes that later become structured tasks, decisions, or other typed nodes. Current reclassification mutates the same row in place, only supports note→block / block→block, and does not preserve typed transition history.

## Why this matters now

This is a core product behavior, not a peripheral feature. If it stays under-modeled, every later AI/compiler flow will fight the data model.

## Deliverables

- Node-kind transition policy
- Node transition/facet history model
- Migration path for current in-place reclassification

## Likely files touched

- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.NodeMutations.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.cs`
- `tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs`
- `tests/CanDoItAll.Tests.Unit/*`

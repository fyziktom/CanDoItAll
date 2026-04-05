# ACR-002 — ProjectObjectRecord is an overloaded universal box

- Severity: **High**
- Skill source: `canonical-model-review`
- Category: Boundary drift
- Phase: **Phase 3**
- Timing: **Before next feature wave**
- Dependencies: Depends on ACR-003 and ACR-004. Informs ACR-008 and ACR-014. Simplifies ACR-009.

## Problem statement

One record mixes node carrier truth, spatial semantics, schedule, markers, route, artifact binding, storage/media references, and metadata-driven subtype payloads. With CRM/HR, the same box also becomes the anchor for party assignment metadata.

## Why this matters now

Without a thinner node carrier, every new typed block or cross-module relation continues to inflate one class and one table.

## Deliverables

- Stable NodeCarrier record/table with minimal canonical workbench-owned identity
- Dedicated companion/facet models for schedule, spatial-semantic state, signals, artifacts, and storage/media
- Clear ownership document for which fields remain canonical on the node carrier

## Likely files touched

- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectStructureNodeDescriptor.cs`
- `tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs`
- `tests/CanDoItAll.Tests.Unit/*`

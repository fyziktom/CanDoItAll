# ACR-007 — Route, artifact binding, and storage/media concerns leak into node truth

- Severity: **Medium**
- Skill source: `canonical-model-review`
- Category: Integration drift
- Phase: **Phase 3**
- Timing: **Later in current stabilization wave**
- Dependencies: Easier after ACR-002 and ACR-014 define the node/facet seam.

## Problem statement

Route strings are rewritten during project moves and storage/media references live inside the main node record, making navigation and attachment concerns look canonical even though they are transport/integration concerns.

## Why this matters now

This concern is not the first blocker, but it becomes harder to extract after more facet and actor metadata is added to nodes.

## Deliverables

- Typed artifact binding model
- Route resolver
- Attachment/media companion model

## Likely files touched

- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs`
- `tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs`

# ACR-008 — Spatial semantics are canonical, but marker ownership is duplicated and under-modeled

- Severity: **High**
- Skill source: `canonical-model-review`
- Category: Boundary drift
- Phase: **Phase 3**
- Timing: **Later in current stabilization wave**
- Dependencies: Depends on ACR-002 and should inform ACR-014 because typed evolution must preserve spatial semantics.

## Problem statement

X/Y position and semantic markers are clearly important to the product, but marker state is still written through both dedicated columns and metadata. The model also needs a sharper split between canonical spatial-semantic state and ephemeral canvas UI state.

## Why this matters now

This is the point where the updated bundle corrects an important assumption: X/Y and semantic markers are not merely cosmetic, so the refactor seam must separate semantic space from ephemeral UI—not from the domain.

## Deliverables

- Explicit canonical spatial-semantic ownership
- Single writable semantic marker owner
- Clear boundary document for what remains in view state

## Likely files touched

- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs`
- `tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs`
- `tests/CanDoItAll.Tests.Unit/*`

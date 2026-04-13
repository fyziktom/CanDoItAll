# Target Solution

## End State

- The processes workspace uses less vertical chrome.
- Summary metrics can opt into a badge-like single-row presentation.
- The definition canvas can execute three recomposition intents from one compact toolbar menu.
- Shared C# recomposition primitives exist in a reusable CanvasLib-facing boundary.
- Process-smart recomposition persists non-overlapping, clearer coordinates back into the process definition.

## Boundaries

### Shared BaseLib

- `SummaryTile` gains a new opt-in visual mode for badge-style rendering.
- Shared styling changes stay additive and must not alter existing callers unless they opt in.

### Shared CanvasLib

- Add a shared recomposition command model and reusable geometry primitives for:
  - rectangle overlap detection
  - collision resolution
  - spacing expansion
  - viewport-fit calculations where width usage is the concern
- Keep the shared layer generic. It should understand nodes, sizes, bounds, gaps, and intents, not process-domain semantics such as roles or branch outcomes.
- Expose enough shared chrome support so workbench toolbars can host a recomposition menu without every module rebuilding the same behavior.

### Processes Module

- Build a process-aware recomposition strategy on top of shared primitives.
- The process strategy owns domain semantics:
  - mainline step ordering
  - role rail or side-branch placement
  - branch routers and dependency fan-out
  - fishbone-style readability choices
- Persist the resulting coordinates through the existing `ProcessDefinitionEditorModel` and process persistence path.

## Recommended Technical Shape

- Shared recomposition types should be simple and strongly typed, for example:
  - an enum representing recomposition intents
  - a request model that contains node geometry and optional domain hints
  - a plan or result model that contains node moves and any fit-target metadata
- Do not introduce interfaces unless there is a real strategy boundary with more than one non-trivial implementation. A static or delegate-driven shared engine is acceptable if it keeps the code smaller and clearer.
- The project-structure engine is a reference for deterministic placement, not a template for radial layout reuse in processes.

## Persistence And Proof Boundary

- The product workflow remains the source of truth for writing coordinates.
- Database inspection is used only to verify the product wrote the expected persisted values after user-visible recomposition.

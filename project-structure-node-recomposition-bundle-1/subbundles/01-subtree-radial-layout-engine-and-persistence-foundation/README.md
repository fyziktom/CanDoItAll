# subtree radial layout engine and persistence foundation

## Status

- `Completed`

## Objective

- Add a deterministic subtree recomposition engine and a batch persistence seam that can reposition a selected hierarchy subtree into a denser radial composition while preserving graph relationships and avoiding collisions.

## Covered Inputs

- `N003` selected node defines the recomposition scope
- `N004` recomposition must not reconnect nodes
- `N006` the result must be collision-free
- `N007` preparation must analyze known approaches and choose an architecture
- `N008` bundle execution needs a trustworthy foundation before UI proof starts

## Prerequisites

- Bundle readiness gate passed
- No earlier implementation subbundle is required

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\CanvasAdapters\ProjectStructurePlacementPolicy.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\CanvasAdapters\ProjectStructureGraphAdapter.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\wwwroot\js\workbenchInterop.js
- C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\CanvasWorkbenchContracts.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProjectWorkbenchServiceIntegrationTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePlacementPolicyTests.cs

## Deliverables

- A C# recomposition engine or helper dedicated to selected-subtree layout
- A single service entry point that persists all recomposed node coordinates in one operation
- Collision detection that checks moved nodes against one another and against untouched nodes
- Automated coverage for deterministic layout, persistence, and unchanged relationships

## Dependency Impact

- Subbundle `02` depends on this foundation for the actual recomposition logic and persisted coordinates.
- Subbundle `03` depends on this proof because browser screenshots are meaningless if the engine can still overlap nodes or drift relationships.
- Weak proof here would invalidate the toolbar workflow because the UI would only expose a broken command faster.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Add a workbench-level recomposition engine that accepts the current structure surface and a selected root node id.
2. Reuse current visual shape data to compute per-node bounds for spacing and collision math.
3. Build deterministic descendant ordering and radial placement that keeps the selected root anchored.
4. Add an outward collision-resolution pass that treats untouched nodes as fixed obstacles.
5. Add a `ProjectWorkbenchService` seam that loads, recomposes, and persists the subtree in one save operation.
6. Add targeted automated tests for scope, unchanged relationships, persistence, and collision-free placement.

## Scope Exceptions

- This phase does not add the toolbar button or user-facing workflow.
- This phase does not change connector routing rules.

## Do Not Do

- Do not add automatic layout on load or sync.
- Do not move nodes outside the selected subtree unless a later design explicitly changes scope.
- Do not reuse create-time placement policy as the subtree recomposition engine.
- Do not mutate parent-child or non-tree link data.

## Acceptance Checklist

- The selected root remains in place after recomposition.
- Only descendants of the selected root receive new coordinates.
- Parent-child and non-tree links are unchanged after recomposition.
- Recomputed positions persist after a fresh `GetStructureAsync` reload.
- Automated collision checks pass for representative wide and deep subtrees.

## Proof Required

- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProjectWorkbenchServiceIntegrationTests"`
- Add targeted assertions that the recomposed subtree persists and keeps links unchanged.
- Add targeted assertions that collision checks reject or resolve overlapping placements.

## Browser Validation Logging

- Route: `/projects/<projectId>/structure` during the dependent UI smoke in subbundle `02`
- Viewport: `1600x1000` desktop smoke is required before bundle closure
- Playwright evidence: subbundle `02` must exercise the persisted service result through the real toolbar command before this foundation is considered trusted end to end
- Screenshots: recorded in subbundle `02` and reviewed again in subbundle `03`

## Progression Gate

- The workbench service must expose a stable subtree recomposition seam with passing automated tests for persistence, selection scope, unchanged relationships, and collision-free placement.

## Suggested Agent Prompt

```text
Implement subbundle 01 only.
Add the subtree recomposition engine and service persistence seam without adding the toolbar command yet.
Keep the selected node anchored, position descendants only, and preserve all links and parents.
Add the targeted automated coverage needed to trust this foundation before the UI workflow starts.
```

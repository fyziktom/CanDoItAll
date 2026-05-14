# Current State

## Repo-Grounded Observations

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Canvas\ProcessCanvasRecompositionService.cs` owns the `ResolveCollisions`, `AddSpaceAround`, and `Recompose` authoring-canvas actions.
- `Recompose` already uses a lightweight layered DAG layout: topological order determines columns, dependency relationships determine lanes, branch routers sit between source and dependent steps, and CanvasLib collision resolution removes overlaps.
- The current lane rule treats any branch outcome dependency as a branch lane. That includes the normalized default route, so the default/main path can be pushed off the spine.
- Role nodes are initially anchored by related step Y values but are then forced into one far-left column. This creates long role-to-step links across the full graph and weakens the "who owns this step" reading.
- The final collision pass resolves steps, roles, and branch routers together as movable boxes. That can move the already-composed step spine while trying to fix role or branch collisions.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Canvas\ProcessCanvasSurfaceFactory.Coordinates.cs` still provides fallback coordinates for first render or non-recomposed surfaces.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Canvas\WebGl\ProcessWebGlLayoutEngine.cs` consumes canvas coordinates as layout offsets for 3D modes; it should not be the primary target for this bundle.
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessCanvasRecompositionServiceTests.cs` already covers collision relief, spacing expansion, branching recomposition, and cyclic graph rejection.

## Algorithm Assessment

The right algorithm for this pass is not a force-directed layout. Force layouts are visually attractive, but they are unstable for authored process diagrams and make default path semantics harder to preserve. The repo already has the correct foundation: a layered directed-acyclic graph layout, similar to a small Sugiyama-style pass. The missing pieces are semantic lane assignment, stable spine protection, and role anchors derived from real bindings.

## Source Snapshot

- CodeAnalytics snapshot: `snap-20260508133610-2a4c6d27`.
- Relevant project dependency: `CanDoItAll.Modules.Processes` references `CanDoItAll.Components.CanvasLib`; `CanDoItAll.Tests.Components` references both.

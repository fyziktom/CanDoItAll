# Target Solution

## Implementation Shape

- Keep the repair inside `ProcessCanvasRecompositionService` because the user asked to tune automatic positions, and that service already owns the `Recompose` action.
- Preserve CanvasLib as the generic collision and box-measurement layer. Do not move process-specific routing rules into CanvasLib.
- Treat the algorithm as a staged layered layout:
  1. Normalize process branching and build a step dependency graph.
  2. Assign columns by topological depth.
  3. Keep default-route and non-branch dependencies on the primary lane.
  4. Assign custom and exception branch routes to alternating side lanes.
  5. Anchor roles near the average position of their assigned or decision steps.
  6. Resolve role and branch collisions against a pinned step spine.

## Boundaries

- `ProcessCanvasSurfaceFactory` remains responsible for projecting process models into workbench nodes and links.
- `ProcessCanvasRecompositionService` remains responsible for mutating authoring coordinates on the editor model.
- `CanvasLayoutCollisionResolver` remains generic and unchanged unless a truly reusable collision feature is required.
- `ProcessWebGlLayoutEngine` remains out of primary scope; it can benefit indirectly because it reads recomposed canvas coordinates.

## Algorithm Decision

Use a deterministic layered DAG layout. It is better than a force-directed layout here because authored process diagrams need stable semantic placement, predictable recomposition, and repeatable tests. The smallest correct change is to improve the existing layered algorithm rather than introducing ELK, Graphviz, or a JavaScript layout runtime.

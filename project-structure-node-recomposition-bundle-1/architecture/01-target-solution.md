# Target Solution

## Recommended Architecture

- Add a dedicated C# layout engine under the workbench module, for example `ProjectStructureSubtreeRecompositionEngine`, instead of embedding geometry math inside `ProjectStructurePage`.
- Add a single service entry point on `ProjectWorkbenchService`, for example `RecomposeSubtreeAsync(Guid projectId, string rootNodeId)`, that:
  - loads the current structure surface
  - computes new positions for the selected subtree only
  - validates collisions against untouched nodes
  - persists the final coordinates in one save operation
- Keep the page orchestration thin:
  - toolbar button
  - disabled state or explicit feedback when recomposition is not meaningful
  - service call
  - reload plus `workflowFeedback`

## Algorithm Choice

- Choose a deterministic radial subtree layout derived from tidy-tree principles.
- Recommended flow:
  1. Build the descendant tree from `ParentId` under the selected node.
  2. Preserve stable sibling order from the current graph order so repeated recomposition is predictable.
  3. Compute leaf spans or subtree weights so siblings occupy contiguous angular sectors.
  4. Keep the selected node anchored at its current coordinate.
  5. Reserve an angular gap toward the selected node’s parent when one exists so the recomposed descendants do not crowd the incoming parent edge.
  6. Place each depth on a ring whose radius is derived from current shape bounds and required arc spacing.
  7. Run an outward collision-resolution pass that treats untouched nodes as fixed obstacles and only pushes recomposed nodes farther from the selected root when necessary.
  8. Round and persist the final coordinates.

## Why This Is The Best Fit

- It satisfies the user’s request for manual, scoped recomposition without introducing unstable global auto-layout.
- It uses the current hierarchy model instead of pretending the canvas is a fully general graph.
- It respects existing manual edits outside the selected subtree.
- It keeps implementation complexity moderate while still giving a strong collision guarantee.

## Rejected Alternatives

- `Force-directed layout`
  Rejected because it is harder to keep deterministic, more likely to disturb user-adjusted layouts, and unnecessary when the user explicitly wants a tree-scoped command.
- `Whole-canvas Sugiyama or layered layout`
  Rejected because it still favors directional flow and does not directly solve the “use the empty space around this selected root” complaint.
- `Page-only client-side recomposition`
  Rejected because positions would drift from persisted state and be lost after reload or MCP-driven refresh.

## Data And Boundary Rules

- Use parent-child hierarchy only for layout.
- Keep extra links rendered but non-authoritative for layout decisions.
- Reuse the current shape vocabulary from `ProjectObjectVisualProfile` and `workbenchInterop.js` so collision math matches the actual renderer.
- Keep the toolbar command in `ProjectStructurePage` and the geometry or persistence logic in workbench services or adapters.

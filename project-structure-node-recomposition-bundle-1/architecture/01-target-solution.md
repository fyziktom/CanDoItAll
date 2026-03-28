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

- Choose a deterministic layered radial subtree layout derived from tidy-tree principles, but with explicit first-ring clock-face slots and branch-sector bubbles.
- Recommended flow:
  1. Build the descendant tree from `ParentId` under the selected node.
  2. Preserve stable sibling order from the current graph order so repeated recomposition is predictable.
  3. Compute subtree weights from each first-layer branch so the first ring can assign balanced clockwise hour-like slots.
  4. Keep the selected node anchored at its current coordinate.
  5. Place first-layer descendants on the first ring in clockwise order so each branch owns a clear hour-like slot and no first-layer cluster stacks on one side.
  6. Allocate every first-layer branch an angular sector or invisible bubble whose width is derived from subtree weight plus readability spacing, not only from minimum collision distance.
  7. Place deeper descendants on later rings while constraining them to the sector of their first-layer branch so branch groups do not cross one another.
  8. Run collision-resolution at two levels:
     - node-vs-node collision inside the same branch bubble
     - branch-bubble vs branch-bubble collision so one branch cannot cut through another
  9. Treat untouched nodes as fixed obstacles and push whole branch sectors outward when necessary instead of collapsing unrelated groups together.
  10. Round and persist the final coordinates.

## Why This Is The Best Fit

- It satisfies the user’s request for manual, scoped recomposition without introducing unstable global auto-layout.
- It uses the current hierarchy model instead of pretending the canvas is a fully general graph.
- It respects existing manual edits outside the selected subtree.
- It prioritizes readable grouped structure over overly aggressive packing, which matches the follow-up feedback about large maps never fitting on one screen anyway.

## Rejected Alternatives

- `Force-directed layout`
  Rejected because it is harder to keep deterministic, more likely to disturb user-adjusted layouts, and unnecessary when the user explicitly wants a tree-scoped command.
- `Whole-canvas Sugiyama or layered layout`
  Rejected because it still favors one dominant direction and does not directly solve the “hour-based first layer around this selected root” complaint.
- `Page-only client-side recomposition`
  Rejected because positions would drift from persisted state and be lost after reload or MCP-driven refresh.

## Data And Boundary Rules

- Use parent-child hierarchy only for layout.
- Keep extra links rendered but non-authoritative for layout decisions.
- Reuse the current shape vocabulary from `ProjectObjectVisualProfile` and `workbenchInterop.js` so collision math matches the actual renderer.
- Keep the toolbar command in `ProjectStructurePage` and the geometry or persistence logic in workbench services or adapters.
- Treat the first layer under the selected root as the branch partitioning layer that defines downstream sectors or bubbles.
- Allow larger overall radius when needed to preserve group readability and branch separation.

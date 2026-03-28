# Assumptions And Risks

## Working Assumptions

- The subtree to recompose is defined only by `ParentId` ancestry below the selected node.
- The selected node stays fixed in place so the command preserves the user’s current anchor and reduces disorientation.
- Shape-derived bounds from `workbenchInterop.js` are stable enough to share with the C# layout engine.
- Recomposition may move both custom and synced nodes, because the page already allows persisted position updates for any visible node.

## Critical Path Risks

- Nodes with extra non-tree links can still produce visually long connectors after recomposition even when node placement is correct.
- A subtree that has many shallow descendants can require large ring radii. If the radius logic is too small, collisions will remain, and if it is too large, the feature will not materially improve space use.
- Multi-parent project hierarchy nodes can place semantic relationships outside the parent-child tree. The engine must avoid treating those as alternate parents because that would accidentally change the user’s mental model.
- If recomposition is implemented only in the page, reloads and MCP refreshes will erase the result. The persistence seam must live in `ProjectWorkbenchService`.

## Validation Risks

- Component tests can prove button wiring and service calls, but they cannot prove rendered overlap or unused-space improvements.
- Browser proof needs more than “button clicked”: it must verify that the command changes positions, that no nodes overlap, and that the resulting composition uses the space around the selected node more effectively.
- Collision detection must be validated against untouched neighbor nodes, not only nodes inside the recomposed subtree.

## Reopen Triggers

- Reopen the foundation subbundle if service-side proof shows overlapping rectangles after recomposition or if untouched nodes are moved unexpectedly.
- Reopen the toolbar workflow subbundle if the command can run without a meaningful selection root, silently no-ops without feedback, or modifies link structure.
- Reopen the closure subbundle if browser screenshots still show large unused space around the selected root or any node collision at desktop or narrower widths.

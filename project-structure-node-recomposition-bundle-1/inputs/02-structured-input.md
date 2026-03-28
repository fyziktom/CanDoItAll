# Structured Input

## Objectives

- Add a new toolbar button on the project structure canvas that manually recomposes the currently selected node and every descendant below it.
- Use a space-efficient radial or circular composition so the subtree uses the available space around the selected node instead of extending mostly in one direction.
- Preserve the existing graph structure. Recomposition changes positions only and must not reconnect or reparent nodes.
- Persist the recomposed coordinates so the result survives reloads.
- Prove that the final layout has no node collisions on the canvas.

## Hard Constraints

- The feature must not run automatically on load, sync, create, or ordinary node movement.
- The command scope is anchored to the selected node only.
- The selected node stays the semantic root of the recomposed subtree.
- Existing links stay intact even when extra non-tree links exist.
- The implementation must follow the bundle workflow: preparation, readiness validation, execution, browser proof, and closure validation.

## Assumptions

- Parent-child hierarchy comes from `ProjectStructureNode.ParentId` and is the only relationship used for layout.
- Non-tree links such as `DependsOn` or extra project-parent links remain rendered but do not drive layout decisions.
- Shape-based node bounds from the current canvas renderer are accurate enough for server-side collision planning.

## Risks

- Multi-parent or synced project nodes can create visual cross-links even when the parent-child tree is laid out cleanly.
- A radial subtree can still collide with untouched neighboring nodes unless the algorithm treats them as fixed obstacles.
- Deep or wide subtrees may need dynamic radius expansion to stay collision-free.

## Validation Expectations

- Add targeted component and integration coverage for the recomposition engine and toolbar workflow.
- Run real browser validation on the project structure page in a large desktop viewport and at a narrower width.
- Capture screenshots and verify that the recomposed subtree uses space more intentionally and does not visually collide.

# P1-02 Viewport Culling And Filtered Scene Projection

## Status
- Lifecycle status: `Ready`

## Objective
- Render only the scene objects needed for the current viewport and interaction context.

## Covered Inputs
- Audit recommendation for viewport culling on large graphs.
- Feature preservation items `F21`, `F26`, and `F30`.

## Prerequisites
- `P1-01` completed with trusted retained-renderer proof.

## Exact Source References
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\canvasWorkbenchInterop.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\CanvasAdapters\ProjectStructureGraphAdapter.cs`

## Deliverables
- Visible-scene projection with overscan.
- Link and frame filtering consistent with visible endpoint and viewport rules.
- Selection and focus behavior preserved for off-screen objects.

## Dependency Impact
- Depends on retained rendering being stable first.
- Feeds later performance and true-canvas comparison work.

## Validation Depth
- Counter-based large-graph proof.
- Browser proof for focus, ensure-visible, and off-screen selection transitions.
- Screenshot review for minimap and visible-scene coherence.

## Implementation Steps
- Add viewport bounds and filtered projection logic.
- Keep selected or focused off-screen nodes behaviorally correct.
- Verify minimap and keyboard behavior after filtering.

## Do Not Do
- Do not drop selection or focus semantics just because elements are temporarily unmounted.
- Do not merge dirty-region drag logic from `P1-03` into this task.

## Acceptance Checklist
- Rendered visible node count is materially smaller than total node count on large graphs.
- Selection and focus still work when selected nodes move into or out of view.

## Proof Required
- Counter evidence from a large-graph scenario.
- Playwright proof for focus and selection behavior.
- Screenshot evidence for visible-scene and minimap state.

## Browser Validation Logging
- Route: ProjectStructure structure route with a sufficiently large graph.
- Viewport: large-screen first.
- Record total versus visible counts, screenshots, and result in `reviews/01-execution-report.md`.

## Progression Gate
- Do not start `P2-01` or `P3-01` until large-graph culling is both measurable and behaviorally correct.

## Suggested Agent Prompt
- Extend the retained renderer with viewport culling and filtered projection while preserving focus, selection, and minimap correctness for off-screen nodes.

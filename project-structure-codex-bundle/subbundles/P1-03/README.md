# P1-03 Dirty-Region Drag Loop Owned By JS

## Status
- Lifecycle status: `Ready`

## Objective
- Keep drag, guides, affected links, and nearby scene updates entirely in JS with minimal patch scope.

## Covered Inputs
- Audit recommendation to reduce drag-loop render scope and server chatter.
- Feature preservation items `F10`, `F28`, `F29`, and `F31`.

## Prerequisites
- `P1-01` completed with trusted retained-renderer proof.

## Exact Source References
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\canvasWorkbenchInterop.js`

## Deliverables
- Dirty-region tracking for moved nodes, affected links, and guides.
- Minimal active drag updates owned by JS.
- Correct frame, snap-guide, and selection-marquee behavior preserved.

## Dependency Impact
- Can reopen `P0-04` if drag-loop optimization reveals batched move or border-adoption defects.
- Downstream performance claims rely on this being measurably smaller than whole-scene patching.

## Validation Depth
- Counter-based drag-session proof.
- Browser proof for multi-select drag, guides, and frame interactions.
- One dependent-flow smoke if border adoption or link-mode behavior is touched.

## Implementation Steps
- Inspect active drag patch scope inside the retained renderer.
- Narrow updates to dirty objects only.
- Recheck guide and frame behavior before closing the task.

## Do Not Do
- Do not drop guide correctness for lower render cost.
- Do not treat a smooth drag visually as sufficient without patch-scope evidence.

## Acceptance Checklist
- Active drag updates only moved nodes, affected links, and active guides.
- Guide rendering stays correct while render cost drops materially.

## Proof Required
- Counter evidence from a drag session.
- Playwright proof for multi-node drag and guide behavior.
- Screenshot evidence when guide visuals change.

## Browser Validation Logging
- Route: ProjectStructure structure route.
- Viewport: large-screen with enough room to exercise drag and frame flows.
- Record counters, screenshots, and gate decision in `reviews/01-execution-report.md`.

## Progression Gate
- Do not start downstream modularization or true-canvas spike work until drag-loop patch scope is measurably narrow and behaviorally correct.

## Suggested Agent Prompt
- Narrow the active drag loop to dirty nodes, links, and guides only, then prove the reduction with counters while preserving guide, frame, and selection behavior.

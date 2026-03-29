# P1-01 Retained DOM-SVG Renderer For Nodes, Links, And Frames

## Status
- Lifecycle status: `Ready`

## Objective
- Convert the current scene renderer from rebuild-heavy behavior to retained patch-based behavior while staying in DOM and SVG.

## Covered Inputs
- Audit recommendation to postpone a true canvas rewrite and first stabilize retained rendering.
- Feature preservation items `F21`, `F31`, and `F32`.

## Prerequisites
- `P0-03` completed with trusted persistence proof.
- `P0-07` completed with trusted counter and screenshot proof.

## Exact Source References
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\canvasWorkbenchInterop.js`

## Deliverables
- Keyed retained maps for scene objects.
- Patch-based updates for nodes, links, and frames.
- Export image path still driven from the retained live scene.

## Dependency Impact
- Critical foundation for `P1-02`, `P1-03`, `P2-01`, and `P3-01`.
- If retained maps are wrong, every downstream performance result becomes untrustworthy.

## Validation Depth
- Counter-based before and after proof.
- Browser proof for create, delete, link, collapse, drag, and pan flows.
- One dependent-flow smoke before later renderer work may continue.

## Implementation Steps
- Audit current render invalidation and layer rebuild behavior.
- Introduce retained element ownership with the smallest stable patch boundary.
- Preserve existing exported image behavior and shared chrome interaction.

## Do Not Do
- Do not jump into true canvas rendering here.
- Do not break the public workbench API while reorganizing internal rendering.

## Acceptance Checklist
- Normal drag and pan no longer clear and rebuild node and link layers.
- Retained element maps stay consistent after create, delete, link, and collapse operations.

## Proof Required
- Browser proof on ProjectStructure with counters visible.
- Screenshot evidence for representative retained-renderer states.
- Shared-canvas smoke if public JS behavior changes.

## Browser Validation Logging
- Route: ProjectStructure structure route.
- Viewport: large-screen plus at least one dependent interaction smoke.
- Record counters, screenshots, and progression decision in `reviews/01-execution-report.md`.

## Progression Gate
- Do not start `P1-02`, `P1-03`, or `P2-01` until retained rendering is stable under representative graph mutations.

## Suggested Agent Prompt
- Use the existing DOM and SVG model, then introduce retained patching only as far as needed to stop full layer rebuilds on normal interaction without breaking export or shared-canvas behavior.

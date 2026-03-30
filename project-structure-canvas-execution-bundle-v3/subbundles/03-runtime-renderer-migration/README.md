# 03 Runtime Renderer Migration

## Status

- Status: `Completed`
- Legacy task coverage: `T10-T15`

## Objective

Ship the active workbench runtime as a canvas-owned scene while keeping the CanvasWorkbench API stable for ProjectStructure and the benchmark surface.

## Covered Inputs

- `R01`
- `R02`
- `R03`

## Prerequisites

- `01-foundation-and-toolbox` is completed.
- `02-structure-and-assets` is completed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js-src\runtime\canvasWorkbenchInterop.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\canvasWorkbenchInterop.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.cs`

## Deliverables

- Canvas stage shell with frame, link, node, and minimap canvases.
- Canvas-owned export composition.
- ProjectStructure delta move handling without unconditional reload.

## Dependency Impact

- This is the critical foundation for the rest of the bundle because all shared consumers depend on the active runtime scene being truly canvas-based.

## Validation Depth

- Direct source audit of the active runtime file.
- ProjectStructure browser regression pack.
- Benchmark artifact capture.

## Implementation Steps

- Build the runtime stage around canvas surfaces while keeping HTML overlays and accessibility mirror layers intact.
- Move links, frames, nodes, minimap, and export composition to canvas-owned rendering.
- Keep ProjectStructure view-state persistence delayed and move adoption delta-first.

## Do Not Do

- Do not reintroduce `.cw-node` DOM cards, `.cw-workbench__links` SVG, or DOM-clone export into the active runtime path.

## Acceptance Checklist

- The active stage creates canvas layers for frames, links, nodes, and minimap.
- Export draws from renderer-owned canvases.
- ProjectStructure move handling only reloads on fallback conditions.
- ProjectStructure browser regressions and benchmark artifacts are green.

## Proof Required

- `AppSmokeTests.Project_structure_artifacts_capture_required_canvas_evidence`
- `SharedCanvasBrowserTests.Shared_canvas_retained_renderer_keeps_node_and_link_layers_stable_during_drag_and_pan`
- `SharedCanvasBrowserTests.Shared_canvas_viewport_culling_reduces_rendered_nodes_without_losing_offscreen_selection`
- `CanvasBenchmarkArtifactBrowserTests.Canvas_benchmark_artifacts_capture_results_and_decision`

## Browser Validation Logging

- Route: `/projects/{id}/structure`, `http://127.0.0.1:5191/groups/canvas/benchmark`
- Viewports: `1900x1200`, `1600x1100`
- Evidence: `output/playwright/bundle-p0-07-project-structure-diagnostics.png`, `output/playwright/bundle-p1-01-retained-drag.png`, `output/playwright/bundle-p1-01-retained-pan.png`, `output/playwright/bundle-p1-02-large-graph-culling.png`, `output/playwright/bundle-p1-02-offscreen-selection.png`, `output/playwright/bundle-p1-03-guide-drag.png`, `artifacts/screenshots/i25`

## Progression Gate

- Passed because the active runtime source audit and ProjectStructure browser pack both prove the canvas-owned scene and export path.

## Suggested Agent Prompt

Validate the active runtime file and ProjectStructure browser pack together. Treat any surviving DOM or SVG scene dependence as a reopen condition.

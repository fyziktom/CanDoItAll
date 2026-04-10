# Canvas renderer scene split

## Status

- `Completed`

## Objective

- Split scene utilities, hit testing, and canvas popover-hover helpers out of `06-canvas-renderers.js` into a dedicated ordered runtime slice so the remaining renderer file focuses on drawing and retained-layer logic.

## Covered Inputs

- `N006` long JS files deserve splitting
- `N007` share helper functions and useful refactoring
- `R009` split the largest verified workbench-runtime hotspots

## Prerequisites

- `04-js-hotspot-inventory-and-boundaries`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\06-canvas-renderers.js`
- `C:\repositories\CanDoItAll\tools\canvaslib\asset-manifest.json`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\Shared\Assets\CanvasLibBodyAssets.razor`

## Deliverables

- A new ordered runtime file for canvas scene utilities and hit testing at `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\06a-canvas-scene-and-hit-testing.js`
- A smaller `06-canvas-renderers.js` focused on drawing responsibilities
- Updated asset ordering so the shared module still loads deterministically

## Dependency Impact

- `06-runtime-entry-splitting-and-regression-proof` depends on this phase because the `07` split assumes the canvas scene helpers already live behind stable shared exports.

## Validation Depth

- `UI runtime structure with workbench smoke`

## Implementation Steps

1. Move scene primitives, palette helpers, hot-zone registration, hit testing, and scene-popover hover logic into a new ordered runtime slice.
2. Keep the shared `canvasWorkbenchModule` exports intact so downstream files continue to late-bind the same surface.
3. Update the runtime asset manifest and generated asset component so the new file loads before `06-canvas-renderers.js`.
4. Run a workbench smoke to confirm the split asset chain still initializes and the hover path still responds.

## Scope Exceptions

- Do not widen this phase into `07-runtime-entry.js` yet.

## Do Not Do

- Do not change the public `canvasWorkbench` API.
- Do not rewrite node rendering behavior while moving helper responsibilities.

## Acceptance Checklist

- `06-canvas-renderers.js` is materially smaller and focused on drawing work.
- Scene-hit and popover-hover helpers live in a dedicated ordered file.
- The real workbench route still loads and responds after the split.

## Proof Required

- Updated asset manifest and generated body-assets component
- Targeted validation on the CanvasLib consumer project or watch session
- A real workbench route smoke after the `06` split

## Browser Validation Logging

- Route under test: `/projects/a17989b9-a8df-4671-9a3a-2d1fdcdfc2fd/structure`
- Required viewport passes: `1600x900`
- Required Playwright actions: load the page, wait for the workbench canvas to render, hover an annotation-bearing hot zone, and confirm no console errors
- Expected screenshots: optional unless the smoke reveals visible regressions
- Required visual review: no blank canvas, no missing interaction chrome, no immediate popover regression

## Progression Gate

- Downstream work may continue only after the split asset chain proves it still initializes and the real workbench route still responds to canvas interaction.

## Closure Note

- Completed on `2026-04-10`.
- `06a-canvas-scene-and-hit-testing.js` now owns scene primitives, hot-zone registration, hit testing, palette helpers, and popover-hover synchronization.
- The runtime asset order was updated and regenerated successfully, and a clean workbench smoke confirmed load, annotation hover response, and zero console errors.

## Suggested Agent Prompt

```text
Implement subbundle 05 only. Split scene utilities and hit testing out of 06-canvas-renderers.js without changing behavior or the shared runtime contract.
```

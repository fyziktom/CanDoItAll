# JS Organization Hotspots

## Inventory Summary

- The largest `CanvasLib` JavaScript files live in the workbench runtime and the calendar runtime, but the workbench runtime is the highest-confidence execution target because it already has a real proof surface and was directly implicated by the earlier popover incident.
- The largest workbench runtime files are:
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\07-runtime-entry.js` at roughly 1784 lines
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\06-canvas-renderers.js` at roughly 1625 lines
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\02-layout-and-legacy-render.js` at roughly 1552 lines
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\05-viewport-and-events.js` at roughly 1142 lines
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\03-interaction-and-state.js` at roughly 1099 lines

## Boundary Decision

- The codebase already uses ordered, shared-module script slices. Introducing inheritance or pseudo-class hierarchies here would add abstraction cost without matching the current runtime model.
- The lowest-risk organization improvement is to keep the shared `root.canvasWorkbenchModule` contract and split the biggest files into smaller feature-slice scripts loaded in deterministic order.
- The chosen execution seams are:
  - split canvas scene utilities, hit testing, and popover-hover helpers out of `06-canvas-renderers.js`
  - split interaction routing and render-pipeline helpers out of `07-runtime-entry.js`

## Deferred Hotspots

- The calendar runtime files are also large, but they are outside the immediate verified workbench path and would widen this bundle beyond what can be safely browser-proved in the same turn.
- `02-layout-and-legacy-render.js` and `05-viewport-and-events.js` remain candidates for a later organization bundle once the current workbench-runtime splits are stable.

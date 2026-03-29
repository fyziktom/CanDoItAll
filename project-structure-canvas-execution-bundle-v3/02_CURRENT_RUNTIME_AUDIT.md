# Current runtime audit

## Evidence-based summary

The current runtime path for `ProjectStructurePage` is still **not** a true live HTML5 canvas renderer.

### Evidence
- `src/CanDoItAll.Components.CanvasLib/Components/CanvasWorkbench.razor:121-126` creates a host `<div class="cw-canvas-host">`.
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js:6018-6121` builds the workbench scene with:
  - `div` frame layer,
  - `svg` link layer,
  - `div` node layer,
  - `div` debug and guide layers,
  - `svg` minimap.
- A true `<canvas>` appears in the export path only:
  - `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js:6254-6290`.

## Important current improvements that should be preserved

### Batched node-move persistence
`ProjectStructurePage` now uses `MoveObjectsAsync(...)` instead of one move call per node:
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor:952-965`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:585-645`

This is good and should remain.

### Partial retained rendering and viewport projection
Current runtime JS contains meaningful retained/culling work already:
- visible/projected nodes:
  - `canvasWorkbenchInterop.js:598-669`
- retained links:
  - `canvasWorkbenchInterop.js:1257-1304`
- retained group frames:
  - `canvasWorkbenchInterop.js:1439-1495`
- retained nodes:
  - `canvasWorkbenchInterop.js:1949-1999`
- dirty drag patching:
  - `canvasWorkbenchInterop.js:2000-2138`

This work should be evolved into a real canvas renderer, not discarded.

### Floating windows have their own JS runtime
The shared floating-window path is already isolated into:
- `src/CanDoItAll.Components.CanvasLib/Components/CanvasFloatingWindow.razor`
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvas-floating-window.js`

That is the correct direction and should be kept.

## Remaining architectural problems

### 1) Full reload after move is still present
`HandleNodesMovedAsync(...)` still does:
- batch move persistence,
- then `ReloadSurfaceAsync()`,
- then border adoption.

Evidence:
- `ProjectStructurePage.razor:952-965`

### 2) View-state persistence is still too eager
The JS side already contains debounce/idle commit helpers:
- `publishState(...)` at `canvasWorkbenchInterop.js:3109-3116`
- `scheduleViewportStateCommit(...)` at `canvasWorkbenchInterop.js:3128-3140`

But the C# side still eagerly persists and refreshes:
- `HandleCanvasStateChangedAsync(...)` at `ProjectStructurePage.razor:968-995`
- `PersistSelectionAsync(...)` at `ProjectStructurePage.razor:1316-1330`
- `PersistCanvasUiStateAsync(...)` at `ProjectStructurePage.razor:1426-1440`

The same problem exists in PromptFactory:
- `PromptFactoryPage.razor:2867-2898`

### 3) Overlay isolation is incomplete
`isOverlayTarget(...)` currently only checks a partial set of overlay selectors:
- `canvasWorkbenchInterop.js:3038-3041`

The wheel handler always prevents default and zooms:
- `canvasWorkbenchInterop.js:5880-5883`

This is a likely reason for odd toolbox behavior.

### 4) The toolbox is not yet product-complete
Current toolbox item markup is still two-line:
- `ProjectStructurePage.razor:160-170`

Accordion state logic is too simple:
- `ProjectStructurePage.ToolWindows.cs:83-91`

The current Playwright tests do not prove browser expand/collapse of a collapsed group.

### 5) CanvasLib still mixes runtime and preview concepts
There are tiny boundary preview JS shims in `CanvasLib` such as:
- `node-card-composer.js`
- `connector-path-primitive.js`
- `group-frame-overlay.js`
- `diagnostics-overlay.js`
- `minimap-overview.js`

These are not the runtime renderer. They are preview/boundary helpers.

PromptFactory support lane still uses several of these preview components:
- `PromptFactoryPage.razor:762-775`

That is fine, but the codebase should separate preview code from runtime code explicitly.

## Shared consumer implications

### PromptFactory
`PromptFactoryPage` uses:
- `CanvasWorkbench`
- `CanvasFloatingWindow`
- preview boundary components

It must remain stable during shared-canvas migration.

### Sandbox benchmark
The sandbox already contains a useful benchmark page:
- `CanvasBenchmark.razor`
- `canvasBenchmarkPage.js`

This should become part of the rollout evidence plan.

### Legacy module-specific canvas path
There is a real-canvas reference in:
- `src/CanDoItAll.Modules.Workbench/Components/ProjectStructureCanvas.razor`
- `src/CanDoItAll.Modules.Workbench/wwwroot/js/workbenchInterop.js`

This is not the active runtime anymore, but it is still useful as reference material.

## What should stay out of the canvas

Do **not** move these to canvas:
- toolbox window,
- health window,
- selection window,
- dialogs,
- summary modal,
- transcript confirmation,
- mermaid viewer,
- upload and form-heavy editors.

These are richer UI surfaces and not the main dense-scene bottleneck.

## What should move into real canvas

These should be canvas-owned in the new runtime:
- links,
- minimap,
- group frame visuals,
- scene diagnostics marks,
- node cards,
- background grid,
- drag/marquee/snap overlays.

Context menus, composers, tooltips, dialogs, and editors should remain HTML overlays positioned from canvas geometry.

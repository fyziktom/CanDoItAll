# Runtime architecture audit

## Scope of this audit

This audit focused on the runtime path centered on:

- `ProjectStructurePage.razor` and its partial classes
- `CanvasWorkbench.razor`
- `CanvasFloatingWindow.razor`
- `canvasWorkbenchInterop.js`
- `canvas-floating-window.js`
- `ProjectStructureGraphAdapter`
- `ProjectWorkbenchService`
- relevant bUnit and Playwright tests
- shared-canvas consumers such as PromptFactory and Sandbox

## Verified runtime truth

## 1) The dense scene is not currently a live `<canvas>` renderer

The live workbench scene is built by `buildWorkbench(state)` in `canvasWorkbenchInterop.js`, which creates:

- a backdrop DOM layer,
- a scene DOM container,
- a frame DOM layer,
- an SVG link layer,
- a debug DOM layer,
- a guide DOM layer,
- a node DOM layer,
- an anchor DOM layer,
- a transform DOM layer,
- separate overlay elements for context menu, diagnostics, minimap, popover, and status notice.

That means the current scene is effectively a **retained scene graph implemented with DOM + SVG**, not a 2D bitmap canvas renderer.

Real `canvas.getContext("2d")` usage was verified only in the export-image path.

## 2) The hot path mixes client rendering with InteractiveServer persistence

The application is configured for interactive server rendering.  
`CanvasWorkbench` receives JS state callbacks, converts them into Blazor state, and `ProjectStructurePage` currently persists view state on those callbacks.

This means viewport and interaction state can cross the browser/server boundary too often for a scene renderer.

## 3) ProjectStructurePage still owns too much orchestration for hot-path behavior

`ProjectStructurePage.razor` currently contains:

- large runtime markup,
- floating windows,
- support panels,
- dialogs,
- selection logic,
- command routing,
- mutation orchestration,
- view-state persistence,
- surface rebuild logic,
- and full-surface reload decisions.

That makes it a high-blast-radius file and increases the chance that UI-only changes cause expensive work.

## 4) The page includes both product runtime UI and support/demo surfaces

The runtime page still renders:
- Outline support,
- Graph health support,
- CanvasBoundaryCard sections for action-catalog and placement-policy explanations.

Those are useful during development, but they blur the production runtime and add avoidable DOM surface.

## 5) Some important overlay behaviors are already HTML for good reasons

Not everything should move into a true bitmap canvas.

The following should remain HTML/Blazor unless later benchmarking proves otherwise:
- toolbox,
- selection/health windows,
- dialogs,
- file upload surfaces,
- media preview,
- transcript confirmation,
- mermaid viewer,
- summary modal,
- general form-heavy UI.

The architectural problem is **not** that these are HTML.  
The problem is that their event ownership and persistence integration are currently too loose.

## Verified shared-surface impact

The shared canvas workbench and floating-window components are also used by:

- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor`
- `src/CanDoItAll.Components.Sandbox/Components/Pages/Canvas.razor`

That means any low-level renderer or floating-window change must be treated as a shared-platform change, not as a ProjectStructure-only change.

## Architecture implication

The correct medium-term direction is a **hybrid architecture**:

- JS owns the hot path and renderer mechanics.
- C# owns the domain, typed models, and meaningful persistence.
- HTML overlays remain first-class UI, but are fully isolated from the scene host.

## Immediate interpretation

The current performance problem is best described as:

> A DOM/SVG scene renderer with too much full rebuild behavior and too much server/persistence chatter.

That diagnosis matters because it changes the order of work:
- first reduce event leakage, reloads, and persistence chatter,
- then make DOM/SVG retained and culled,
- only then decide whether a true-canvas renderer is worth its complexity.

## File areas most likely to change first

### Shared JS hot path
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js`
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvas-floating-window.js`

### Shared Blazor wrappers
- `src/CanDoItAll.Components.CanvasLib/Components/CanvasWorkbench.razor`
- `src/CanDoItAll.Components.CanvasLib/Components/CanvasFloatingWindow.razor`

### ProjectStructure orchestration
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs`
- `src/CanDoItAll.Modules.Workbench/CanvasAdapters/ProjectStructureGraphAdapter.cs`

## Key recommendation from the audit

Move more responsibility to JavaScript **without** moving everything to raw bitmap drawing.

The fastest safe path is:
- well-structured plain JavaScript,
- retained patch-based rendering,
- isolated overlays,
- minimal server sync during active interaction,
- strong browser regression coverage,
- domain logic and typed models still in C#.

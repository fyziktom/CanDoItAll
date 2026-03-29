# Line reference index

## Runtime scene composition
- `src/CanDoItAll.Components.CanvasLib/Components/CanvasWorkbench.razor:121-126`
  - host is still a `div`, not a runtime canvas stack.
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js:6018-6121`
  - current scene is built from `div` and `svg` layers.
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js:6254-6290`
  - export path uses canvas.

## Overlay routing and wheel behavior
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js:3038-3041`
  - partial overlay selector used by `isOverlayTarget(...)`.
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js:5880-5883`
  - wheel handler always zooms the canvas.
- `src/CanDoItAll.Components.CanvasLib/Components/CanvasFloatingWindow.razor:7-11`
  - root has click stopPropagation, but no full wheel/scene isolation contract on its own.

## State publish and persistence
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js:3109-3140`
  - debounced publish and idle viewport commit support already exist in JS.
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor:968-995`
  - eager `SaveViewStateAsync(...)` in `HandleCanvasStateChangedAsync(...)`.
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor:1316-1330`
  - eager state persistence in `PersistSelectionAsync(...)`.
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor:1426-1440`
  - eager state persistence in `PersistCanvasUiStateAsync(...)`.
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor:2867-2898`
  - PromptFactory eager shared-canvas persistence path.

## Move and reload behavior
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor:952-965`
  - batch move persistence exists, but still followed by `ReloadSurfaceAsync()`.
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor:785-838`
  - full surface reload path.
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor:840-899`
  - local patch path exists for simple node updates.

## Toolbox
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor:108-181`
  - toolbox window markup.
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor:160-170`
  - toolbox items are still two-line.
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.ToolWindows.cs:83-91`
  - `ExpandToolboxGroup(...)` sets a key but does not implement proper toggle semantics.

## Shared consumers and references
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor:69-88`
  - PromptFactory `CanvasWorkbench` usage.
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor:166-284`
  - PromptFactory floating toolbox window usage.
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor:762-775`
  - preview-boundary components in PromptFactory support lane.
- `src/CanDoItAll.Components.Sandbox/Components/Pages/CanvasBenchmark.razor:74-90`
  - retained preview vs true-canvas prototype preview.
- `src/CanDoItAll.Modules.Workbench/Components/ProjectStructureCanvas.razor:42-47`
  - legacy real-canvas create/update path.
- `src/CanDoItAll.Modules.Workbench/wwwroot/js/workbenchInterop.js:1-80`
  - legacy canvas renderer reference.
- `src/CanDoItAll.ComponentKit/Canvas/README.md:1-12`
  - legacy/compatibility-only status of ComponentKit canvas tree.

## App asset duplication
- `src/CanDoItAll.Web/Components/App.razor:25-70`
  - long manual script include list.
- `src/CanDoItAll.Components.Sandbox/Components/App.razor:17-56`
  - duplicated long manual script include list.

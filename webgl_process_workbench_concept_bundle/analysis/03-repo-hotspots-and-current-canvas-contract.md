# Repo hotspots and current canvas contract

## Hotspots that argue for isolation

| Path | Approx. lines |
| --- | --- |
| tests/CanDoItAll.Tests.Playwright/AppSmokeTests.cs | 5106 |
| tests/CanDoItAll.Tests.Components/ProcessWorkspaceTests.cs | 1459 |
| src/CanDoItAll.Components.CanvasLib/Components/Workbench/CanvasWorkbench.razor | 943 |
| src/CanDoItAll.Components.CanvasLib/wwwroot/js/runtime/workbench/07a-runtime-interaction-router.js | 899 |
| src/CanDoItAll.Components.CanvasLib/wwwroot/js/runtime/workbench/07-runtime-entry.js | 868 |
| src/CanDoItAll.Components.CanvasLib/wwwroot/js/runtime/workbench/07b-runtime-rendering.js | 578 |
| src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Canvas.Persistence.cs | 520 |
| src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Canvas.Links.cs | 512 |
| tests/CanDoItAll.Tests.Components/ProcessCanvasSurfaceFactoryTests.cs | 495 |
| src/CanDoItAll.Modules.Processes/ProcessCanvasSurfaceFactory.cs | 488 |
| src/CanDoItAll.Modules.Processes/ProcessCanvasCatalog.cs | 486 |
| src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor | 447 |
| src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Canvas.Actions.cs | 409 |
| src/CanDoItAll.Modules.Processes/ProcessCanvasSurfaceFactory.Ports.cs | 378 |
| src/CanDoItAll.Modules.Processes/ProcessCanvasBranching.cs | 374 |

## Current contract patterns worth mirroring

| Area | Observed pattern |
| --- | --- |
| Current canvas API style | create, update, fitView, focusNode, getState, getDiagnostics, getSceneSnapshot, exportImageData, simulateDrag, finishInteraction |
| Current process semantics | stable IDs for roles/steps/branch routers, connection categories, branch outcome ports |
| Current test blueprint | Playwright reads host state and calls semantic helper methods instead of relying on raw pointer-only canvas control |

## Practical implication for the concept

The WebGL concept should **mirror patterns**, not **reuse the existing canvas runtime directly**:

- mirror the typed Blazor wrapper shape,
- mirror the semantic automation namespace style,
- mirror the proof philosophy,
- do not try to retrofit WebGL into the current canvas JS files.

## Files that are especially relevant as design references

- `src/CanDoItAll.Components.CanvasLib/Components/Workbench/CanvasWorkbench.razor`
- `src/CanDoItAll.Components.CanvasLib/Canvas/Workbench/CanvasWorkbenchSurface.cs`
- `src/CanDoItAll.Components.CanvasLib/Canvas/Workbench/CanvasWorkbenchEvents.cs`
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/runtime/workbench/07-runtime-entry.js`
- `src/CanDoItAll.Modules.Processes/ProcessCanvasCatalog.cs`
- `src/CanDoItAll.Modules.Processes/ProcessCanvasBranching.cs`
- `tests/CanDoItAll.Tests.Playwright/AppSmokeTests.cs`

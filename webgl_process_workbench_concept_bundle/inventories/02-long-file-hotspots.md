# Long-file hotspots

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
| src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Canvas.cs | 332 |
| src/CanDoItAll.Modules.Processes/ProcessCanvasSurfaceFactory.Chrome.cs | 273 |
| src/CanDoItAll.Modules.Processes/ProcessDefinitionEditorModels.cs | 253 |
| src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor.cs | 247 |
| src/CanDoItAll.Components.Sandbox/Components/Pages/Canvas.razor | 230 |
| src/CanDoItAll.Modules.Processes/ProcessCanvasSurfaceFactory.Links.cs | 213 |
| src/CanDoItAll.Components.CanvasLib/Canvas/Workbench/CanvasWorkbenchUiState.cs | 185 |
| src/CanDoItAll.Components.CanvasLib/Canvas/Workbench/CanvasWorkbenchNode.cs | 151 |
| src/CanDoItAll.Components.CanvasLib/Canvas/Workbench/CanvasWorkbenchEvents.cs | 49 |
| src/CanDoItAll.Components.CanvasLib/Canvas/Workbench/CanvasWorkbenchSurface.cs | 33 |
| src/CanDoItAll.Components.Sandbox/Program.cs | 21 |

## Why this matters

The concept should prefer **new isolated seams** over broad edits inside the hottest existing production files. The current repository already has several high-entropy hotspots in canvas/runtime and process-workspace areas, so the concept branch should avoid widening them unless a later pilot proves the direction is worth carrying forward.

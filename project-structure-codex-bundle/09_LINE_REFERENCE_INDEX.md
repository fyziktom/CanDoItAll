# Line reference index

This is the fast evidence index for the most important findings.

## Runtime configuration

- `src/CanDoItAll.Web/Program.cs:30-34` — Interactive Server components and SignalR hub options.
- `src/CanDoItAll.Web/Program.cs:108-110` — `AddInteractiveServerRenderMode()`.

## ProjectStructure runtime page

- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor:1205-1257` — `ReloadSurfaceAsync(...)`.
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor:1259-1284` — `RefreshCanvasSurface()`.
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor:1286-1308` — selection callback.
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor:1310-1318` — moved-node callback.
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor:1321-1341` — canvas-state callback and view-state save.
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor:1663-1677` — selection persistence.
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor:1772-1785` — UI-state persistence.
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor:1827-1859` — floating-window state persistence.
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor:804-840` — Outline and Graph Health support cards.
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor:843-889` — CanvasBoundaryCard support/demo sections.

## Toolbox-specific logic

- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.ToolWindows.cs:49-80` — toolbox groups and open-state resolution.
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.ToolWindows.cs:82-90` — `ExpandToolboxGroup(...)`.
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.ToolWindows.cs:103-114` — search matching.

## Shared workbench callbacks

- `src/CanDoItAll.Components.CanvasLib/Components/CanvasWorkbench.razor:415-423` — `OnStateChanged(...)`.
- `src/CanDoItAll.Components.CanvasLib/Components/CanvasWorkbench.razor:675-699` — create dialog and export image surface bridge.

## Shared floating-window behavior

- `src/CanDoItAll.Components.CanvasLib/Components/CanvasFloatingWindow.razor:196-210` — `OnGeometryChanged(...)`.
- `src/CanDoItAll.Components.CanvasLib/Components/CanvasFloatingWindow.razor:257-265` — `PublishStateAsync(...)`.
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvas-floating-window.js:173-180` — scheduled geometry notification debounce.
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvas-floating-window.js:204-223` — pointer move/up geometry notifications.

## Shared renderer structure

- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js:5418-5515` — `buildWorkbench(state)` creates DOM/SVG layers.
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js:1085-1086` — link layer clear.
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js:1195` — frame layer clear.
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js:1638-1639` — node layer clear.
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js:1854` — guide layer clear.
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js:1895` — anchor layer clear.
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js:1991` — transform layer clear.
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js:2106` — debug layer clear.
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js:2515-2517` — current overlay-target selector list.
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js:5154-5226` — pointerdown event flow.
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js:5248-5279` — double-click flow.
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js:5280-5283` — wheel always zooms.
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js:5284-5295` — context menu flow.
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js:5587` — debounced state publish to .NET.
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js:5674-5685` — actual canvas export path.
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js:5788-5952` — global API and `__canvasWorkbenchState`.

## Position ownership

- `src/CanDoItAll.Components.CanvasLib/Canvas/CanvasWorkbenchContracts.cs:134-150` — `ManualPositions`, `WindowStates`, zoom/pan fields.
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js:4766-4774` — drag writes to `state.ui.manualPositions`.
- `src/CanDoItAll.Modules.Workbench/CanvasAdapters/ProjectStructureGraphAdapter.cs:159-160` — node X/Y projected into canvas nodes.

## Service-layer hotspots

- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:283-309` — `GetStructureAsync(...)`.
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:562-577` — `MoveObjectAsync(...)`.
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:1017-1115` — beginning of `SyncGraphAsync(...)`.

## Shared-canvas consumers for regression

- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor:69-88` — shared workbench usage.
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor:166-284` — shared floating-window usage.
- `src/CanDoItAll.Components.Sandbox/Components/Pages/Canvas.razor:28-68` — Sandbox usage.

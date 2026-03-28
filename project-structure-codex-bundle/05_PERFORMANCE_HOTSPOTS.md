# Performance hotspots

This section is the prioritized technical diagnosis behind the task plan.

The goal is not merely to describe what looks slow.  
The goal is to identify the **smallest high-leverage changes** that improve performance without breaking functionality.

## H1 — Runtime renderer is DOM/SVG-based, not a true HTML5 canvas renderer
- **Severity:** P0
- **Evidence:** src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js:5418-5515; src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js:5674-5685
- **Finding:** The dense runtime path builds DOM and SVG layers for nodes, links, frames, and overlays. Real <canvas> drawing appears only in the image export path.
- **Why it matters:** Performance issues are currently dominated by DOM churn, layout, paint, and InteractiveServer chatter, not by canvas drawing limits.
- **Best matching tasks:** P0-03, P1-01, P1-02, P2-01, P3-01

## H2 — Render path fully clears key layers
- **Severity:** P0
- **Evidence:** src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js:1085-1086; src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js:1195; src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js:1638-1639; src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js:1854; src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js:1895; src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js:1991; src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js:2106; src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js:2688-2703
- **Finding:** Links, nodes, frames, guides, anchors, transforms, and debug decorations are repeatedly torn down and rebuilt.
- **Why it matters:** Simple pan, drag, zoom, and selection changes cost far more than necessary and scale poorly with graph size.
- **Best matching tasks:** P1-01, P1-02, P1-03

## H3 — No meaningful viewport culling for rendered scene density
- **Severity:** P1
- **Evidence:** src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js:2688-2703; src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js:1638-1839
- **Finding:** The renderer iterates and rebuilds large visible sets without a strong retained-mode visibility filter.
- **Why it matters:** Large graphs stay expensive even when only a small slice of the graph is on screen.
- **Best matching tasks:** P1-02, P1-03, P2-01

## H4 — Overlay target detection excludes floating windows and several runtime overlays
- **Severity:** P0
- **Evidence:** src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js:2515-2517; src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js:5154-5255; src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js:5284-5287
- **Finding:** Event guards currently ignore only a small set of overlay selectors and do not treat floating windows/toolbox/dialog chrome as fully isolated overlay ownership.
- **Why it matters:** Clicks, double-clicks, context menus, and focus can leak from the toolbox and other overlays into the scene host.
- **Best matching tasks:** P0-01

## H5 — Wheel always zooms the canvas, including when the pointer is inside overlay content
- **Severity:** P0
- **Evidence:** src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js:5280-5283
- **Finding:** The wheel handler always prevents default and applies zoom with no overlay ownership guard.
- **Why it matters:** Scrollable content inside toolbox and floating windows can feel broken because wheel input is stolen by canvas zoom.
- **Best matching tasks:** P0-01

## H6 — Floating window geometry publishes too often and immediately feeds persistence
- **Severity:** P0
- **Evidence:** src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvas-floating-window.js:173-180; src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvas-floating-window.js:204-223; src/CanDoItAll.Components.CanvasLib/Components/CanvasFloatingWindow.razor:196-210; src/CanDoItAll.Components.CanvasLib/Components/CanvasFloatingWindow.razor:257-265; src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor:1827-1859
- **Finding:** Window drag/resize fires frequent geometry updates that immediately bubble to page state and persisted UI state.
- **Why it matters:** Floating windows trigger avoidable rerender and persistence churn, especially bad on InteractiveServer.
- **Best matching tasks:** P0-02

## H7 — Canvas UI state is too chatty on InteractiveServer
- **Severity:** P0
- **Evidence:** src/CanDoItAll.Web/Program.cs:30-34; src/CanDoItAll.Web/Program.cs:108-110; src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js:5587; src/CanDoItAll.Components.CanvasLib/Components/CanvasWorkbench.razor:415-423; src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor:1321-1341
- **Finding:** Pan/zoom/state pings publish frequently, reach Blazor, rebuild surface state, and persist view state.
- **Why it matters:** Viewport interactions are paying SignalR and DB costs that should not exist in the hot path.
- **Best matching tasks:** P0-03, P0-07

## H8 — Node move persistence is N service calls plus a heavy reload
- **Severity:** P0
- **Evidence:** src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor:1310-1318; src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:562-577
- **Finding:** Each moved node is saved separately, followed by a full surface reload and later border-adoption logic.
- **Why it matters:** Multi-select drag scales poorly and amplifies database and requery cost.
- **Best matching tasks:** P0-04, P0-05

## H9 — ReloadSurfaceAsync calls GetStructureAsync, which always calls SyncGraphAsync
- **Severity:** P0
- **Evidence:** src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor:1205-1257; src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:283-309; src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:1017-1115
- **Finding:** Many mutations and refreshes re-enter the full structure load path, which in turn recomputes system-managed graph projection.
- **Why it matters:** Simple mutations pay more I/O and projection work than needed; this becomes obvious on larger graphs.
- **Best matching tasks:** P0-05, P1-04

## H10 — Runtime page mixes production authoring surface with support/demo cards
- **Severity:** P0
- **Evidence:** src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor:804-840; src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor:843-889
- **Finding:** Outline/health support and two CanvasBoundaryCard sections inflate the runtime page and blur authoring vs demo responsibilities.
- **Why it matters:** The runtime DOM is larger than needed and product behavior is harder to reason about.
- **Best matching tasks:** P0-06, P1-04, P2-02

## H11 — ProjectStructure positions have duplicate sources of truth
- **Severity:** P0
- **Evidence:** src/CanDoItAll.Components.CanvasLib/Canvas/CanvasWorkbenchContracts.cs:134-150; src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js:4766-4774; src/CanDoItAll.Modules.Workbench/CanvasAdapters/ProjectStructureGraphAdapter.cs:159-160
- **Finding:** ManualPositions in UI state are updated during drag while ProjectStructure nodes also persist X/Y in domain records.
- **Why it matters:** This increases churn and makes it harder to reason about what is authoritative before and after drag commit.
- **Best matching tasks:** P0-03, P0-04, P2-01

## H12 — Two diverged canvas component trees increase maintenance risk
- **Severity:** P2
- **Evidence:** src/CanDoItAll.ComponentKit/**; src/CanDoItAll.Components.CanvasLib/**
- **Finding:** Canvas-related components exist in two diverged trees; runtime uses CanvasLib, but duplication remains large.
- **Why it matters:** Future fixes can land in the wrong tree or need to be duplicated manually.
- **Best matching tasks:** P3-02


## Summary interpretation

Taken together, the hotspots say:

- the current renderer rebuilds too much,
- the page reloads too much,
- the server hears about too many UI-only changes,
- overlays do not fully own their input,
- and too many responsibilities live in one page.

That is exactly why the recommended path is:
1. input isolation,
2. commit-only persistence,
3. batch persistence,
4. fewer reloads,
5. retained patch-based rendering,
6. viewport culling,
7. optional true-canvas benchmark later.

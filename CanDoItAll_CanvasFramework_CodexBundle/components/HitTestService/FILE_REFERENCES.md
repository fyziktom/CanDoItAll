# HitTestService File References

| Path | Why it matters | Key symbols / areas |
| --- | --- | --- |
| src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099 | Main shared graph runtime. Owns normalization, DOM/SVG rendering, measurement, links, group frames, context menus, create composers, drag, marquee, zoom, pan, maximize, and exported JS interop surface. | mountFloatingInspector, normalizeSurface, buildNodeLookup, computeResolvedNodePositions, renderLinks, renderGroupFrames... |
| src/CanDoItAll.Modules.Workbench/wwwroot/js/workbenchInterop.js#L1-L884 | Legacy graph and calendar runtime. Contains older canvas drawing helpers, hit testing, and duplicate calendar wrapper logic that should not continue as the strategic base. | safeParse, getCanvasNodeBounds, drawRoundedRect, drawDiamond, drawHex, drawShield... |
| src/CanDoItAll.ComponentKit/wwwroot/js/zy-canvas-calendar.js#L1-L3720 | Large specialized calendar widget that already contains its own view renderers, panel, list, editor, export flows, and hit handling. This is a sibling runtime, not a graph scene graph. | CalendarController.prototype.updateOptions, destroy, renderPanel, renderList, persistEvent, render... |
| src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor#L1-L1399 | Primary graph-editor page for project structure. Wires the shared workbench shell, builds nodes/actions, persists positions and view state, hosts inspector panels, and still contains several canvas-specific policies that should move into shared/domain adapter components. | MapCanvasNode, HandleSelectionChangedAsync, HandleNodesMovedAsync, HandleCanvasStateChangedAsync, HandleCreateActionAsync, HandleCanvasActionAsync... |

## Navigation advice

Start with the first file that already owns the most behavior for this component, then inspect the wrapper/page/service files that consume that behavior.

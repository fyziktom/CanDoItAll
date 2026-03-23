# HoverFocusRouter File References

| Path | Why it matters | Key symbols / areas |
| --- | --- | --- |
| src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099 | Main shared graph runtime. Owns normalization, DOM/SVG rendering, measurement, links, group frames, context menus, create composers, drag, marquee, zoom, pan, maximize, and exported JS interop surface. | mountFloatingInspector, normalizeSurface, buildNodeLookup, computeResolvedNodePositions, renderLinks, renderGroupFrames... |
| src/CanDoItAll.ComponentKit/wwwroot/canvas-workbench.css#L1-L2309 | Visual design system for the workbench host, node cards, inspector, context menus, overlays, and supporting panels. | cw-* CSS rules |
| src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.CanvasInspector.cs#L1-L37 | Floating inspector docking logic used by the prompt factory canvas. | DockCanvasInspectorAsync, SyncFloatingInspectorAsync |
| src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.History.cs#L1-L234 | Prompt Factory undo/redo stack and keyboard shortcut registration. Valuable evidence for shared history and shortcut abstractions. | UndoAsync, RedoAsync, OnAfterRenderAsync |

## Navigation advice

Start with the first file that already owns the most behavior for this component, then inspect the wrapper/page/service files that consume that behavior.

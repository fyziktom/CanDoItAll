# JsInteropBridge File References

| Path | Why it matters | Key symbols / areas |
| --- | --- | --- |
| src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099 | Main shared graph runtime. Owns normalization, DOM/SVG rendering, measurement, links, group frames, context menus, create composers, drag, marquee, zoom, pan, maximize, and exported JS interop surface. | mountFloatingInspector, normalizeSurface, buildNodeLookup, computeResolvedNodePositions, renderLinks, renderGroupFrames... |
| src/CanDoItAll.ComponentKit/wwwroot/js/canvasCalendarInterop.js#L13-L335 | Thin bridge between Blazor and the legacy/shared calendar engine. Handles state serialization, typed callback payloads, and JS widget lifecycle. | safeParse, buildContext, buildStateJson, parseViewState, normalizeCalendarEventForDotNet, emitState... |
| src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.CanvasInspector.cs#L1-L37 | Floating inspector docking logic used by the prompt factory canvas. | DockCanvasInspectorAsync, SyncFloatingInspectorAsync |
| src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.History.cs#L1-L234 | Prompt Factory undo/redo stack and keyboard shortcut registration. Valuable evidence for shared history and shortcut abstractions. | UndoAsync, RedoAsync, OnAfterRenderAsync |

## Navigation advice

Start with the first file that already owns the most behavior for this component, then inspect the wrapper/page/service files that consume that behavior.

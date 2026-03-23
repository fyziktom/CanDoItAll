# FloatingInspectorHost File References

| Path | Why it matters | Key symbols / areas |
| --- | --- | --- |
| src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.CanvasInspector.cs#L1-L37 | Floating inspector docking logic used by the prompt factory canvas. | DockCanvasInspectorAsync, SyncFloatingInspectorAsync |
| src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099 | Main shared graph runtime. Owns normalization, DOM/SVG rendering, measurement, links, group frames, context menus, create composers, drag, marquee, zoom, pan, maximize, and exported JS interop surface. | mountFloatingInspector, normalizeSurface, buildNodeLookup, computeResolvedNodePositions, renderLinks, renderGroupFrames... |
| src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866 | Main prompt builder/editor page. Uses the shared workbench shell but still builds graph projection, branch lanes, attachments, and UI-state persistence inline in the page. | BuildCanvasNodes, BuildCanvasLinks, HandleCanvasSelectionChangedAsync, HandleCanvasNodesMovedAsync, HandleCanvasStateChangedAsync, PersistCanvasUiStateAsync... |
| src/CanDoItAll.ComponentKit/Components/CanvasWorkbenchStage.razor#L1-L82 | Shared shell used by Project Structure and Prompt Factory for eyebrow/title copy, stats, canvas slot, inspector slot, and supporting panels. | CanvasWorkbenchStage |

## Navigation advice

Start with the first file that already owns the most behavior for this component, then inspect the wrapper/page/service files that consume that behavior.

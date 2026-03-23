# KeyboardShortcutRouter File References

| Path | Why it matters | Key symbols / areas |
| --- | --- | --- |
| src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.History.cs#L1-L234 | Prompt Factory undo/redo stack and keyboard shortcut registration. Valuable evidence for shared history and shortcut abstractions. | UndoAsync, RedoAsync, OnAfterRenderAsync |
| src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099 | Main shared graph runtime. Owns normalization, DOM/SVG rendering, measurement, links, group frames, context menus, create composers, drag, marquee, zoom, pan, maximize, and exported JS interop surface. | mountFloatingInspector, normalizeSurface, buildNodeLookup, computeResolvedNodePositions, renderLinks, renderGroupFrames... |
| src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor#L1-L572 | Reusable Blazor host for the shared workbench canvas, including toolbar chrome, maximize/focus/fit interactions, JS interop lifecycle, and typed callbacks. | OnParametersSet, OnAfterRenderAsync, OnSelectionChanged, OnNodesMoved, OnStateChanged, ToggleMaximizeAsync... |
| src/CanDoItAll.ComponentKit/wwwroot/js/zy-canvas-calendar.js#L1-L3720 | Large specialized calendar widget that already contains its own view renderers, panel, list, editor, export flows, and hit handling. This is a sibling runtime, not a graph scene graph. | CalendarController.prototype.updateOptions, destroy, renderPanel, renderList, persistEvent, render... |

## Navigation advice

Start with the first file that already owns the most behavior for this component, then inspect the wrapper/page/service files that consume that behavior.

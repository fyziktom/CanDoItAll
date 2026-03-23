# CalendarExportMenu File References

| Path | Why it matters | Key symbols / areas |
| --- | --- | --- |
| src/CanDoItAll.ComponentKit/Components/CanvasCalendar.razor#L1-L258 | Reusable Blazor wrapper around the calendar runtime with typed callbacks, export hook, and JS interop lifecycle. | OnParametersSet, OnAfterRenderAsync, OnSelectionChanged, OnStateChanged, ExportAsync |
| src/CanDoItAll.ComponentKit/wwwroot/js/canvasCalendarInterop.js#L13-L335 | Thin bridge between Blazor and the legacy/shared calendar engine. Handles state serialization, typed callback payloads, and JS widget lifecycle. | safeParse, buildContext, buildStateJson, parseViewState, normalizeCalendarEventForDotNet, emitState... |
| src/CanDoItAll.ComponentKit/Canvas/CanvasCalendarContracts.cs#L3-L223 | Defines the typed wrapper contract for the shared calendar runtime: surface, event model, playlist models, operation context, selection/state callbacks, and export request. | CanvasCalendarSurface, CanvasCalendarEvent, CanvasCalendarPlaylist, CanvasCalendarConnectedEvent, CanvasCalendarChecklistRow, CanvasCalendarOperationContext... |
| src/CanDoItAll.ComponentKit/wwwroot/js/zy-canvas-calendar.js#L1-L3720 | Large specialized calendar widget that already contains its own view renderers, panel, list, editor, export flows, and hit handling. This is a sibling runtime, not a graph scene graph. | CalendarController.prototype.updateOptions, destroy, renderPanel, renderList, persistEvent, render... |

## Navigation advice

Start with the first file that already owns the most behavior for this component, then inspect the wrapper/page/service files that consume that behavior.

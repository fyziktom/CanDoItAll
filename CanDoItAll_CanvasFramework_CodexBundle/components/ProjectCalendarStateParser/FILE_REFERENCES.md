# ProjectCalendarStateParser File References

| Path | Why it matters | Key symbols / areas |
| --- | --- | --- |
| src/CanDoItAll.Modules.Workbench/Pages/ProjectCalendarPage.razor#L1-L161 | Project calendar page that still uses the legacy calendar wrapper and manually parses selected event IDs from JSON view state. | LoadAsync, HandleViewStateChangedAsync, TryReadSelectedEventId |
| src/CanDoItAll.ComponentKit/wwwroot/js/canvasCalendarInterop.js#L13-L335 | Thin bridge between Blazor and the legacy/shared calendar engine. Handles state serialization, typed callback payloads, and JS widget lifecycle. | safeParse, buildContext, buildStateJson, parseViewState, normalizeCalendarEventForDotNet, emitState... |
| src/CanDoItAll.ComponentKit/Canvas/CanvasCalendarContracts.cs#L3-L223 | Defines the typed wrapper contract for the shared calendar runtime: surface, event model, playlist models, operation context, selection/state callbacks, and export request. | CanvasCalendarSurface, CanvasCalendarEvent, CanvasCalendarPlaylist, CanvasCalendarConnectedEvent, CanvasCalendarChecklistRow, CanvasCalendarOperationContext... |
| src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs#L128-L806 | Domain models and service layer for project structure and project calendar, including persistence of links, node movement, UI state, and graph synchronization. | ProjectStructureNode, ProjectStructureLink, ProjectStructureSurface, ProjectCalendarEvent, ProjectCalendarSurface, ProjectObjectCreateRequest... |

## Navigation advice

Start with the first file that already owns the most behavior for this component, then inspect the wrapper/page/service files that consume that behavior.

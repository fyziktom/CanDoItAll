# SerializationPersistencePack File References

| Path | Why it matters | Key symbols / areas |
| --- | --- | --- |
| src/CanDoItAll.ComponentKit/Canvas/CanvasWorkbenchContracts.cs#L5-L340 | Defines the shared graph-workbench DTO layer: surface, nodes, links, UI state, chrome, actions, chips, group frames, and typed callback payloads. | CanvasWorkbenchSurface, CanvasWorkbenchNode, CanvasWorkbenchLink, CanvasWorkbenchUiState, CanvasWorkbenchChrome, CanvasWorkbenchAction... |
| src/CanDoItAll.ComponentKit/Canvas/CanvasCalendarContracts.cs#L3-L223 | Defines the typed wrapper contract for the shared calendar runtime: surface, event model, playlist models, operation context, selection/state callbacks, and export request. | CanvasCalendarSurface, CanvasCalendarEvent, CanvasCalendarPlaylist, CanvasCalendarConnectedEvent, CanvasCalendarChecklistRow, CanvasCalendarOperationContext... |
| src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs#L128-L806 | Domain models and service layer for project structure and project calendar, including persistence of links, node movement, UI state, and graph synchronization. | ProjectStructureNode, ProjectStructureLink, ProjectStructureSurface, ProjectCalendarEvent, ProjectCalendarSurface, ProjectObjectCreateRequest... |
| src/CanDoItAll.Modules.Factory/FactoryDomain.cs#L359-L536 | Prompt Factory domain models, including run node summaries, attachment summaries, setup profile, and editor state persisted with CanvasUiStateJson. | PromptRunNodeSummary, PromptSessionAttachmentSummary, PromptSessionSetupProfile, PromptFactoryEditorModel |
| src/CanDoItAll.Modules.Factory/PromptFactoryService.cs#L238-L715 | Prompt Factory domain service handling recommendations, state persistence, branching, build/export/send flows, and canvas UI-state persistence. | GetRecommendedBlockIdsAsync, SaveSessionStateAsync, SaveCanvasUiStateAsync, BranchNodeAsync, UpdateNodeAsync, SetNodeStateAsync... |
| src/CanDoItAll.Modules.Workbench/Pages/ProjectCalendarPage.razor#L1-L161 | Project calendar page that still uses the legacy calendar wrapper and manually parses selected event IDs from JSON view state. | LoadAsync, HandleViewStateChangedAsync, TryReadSelectedEventId |

## Navigation advice

Start with the first file that already owns the most behavior for this component, then inspect the wrapper/page/service files that consume that behavior.

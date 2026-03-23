# CommandHistoryStore File References

| Path | Why it matters | Key symbols / areas |
| --- | --- | --- |
| src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.History.cs#L1-L234 | Prompt Factory undo/redo stack and keyboard shortcut registration. Valuable evidence for shared history and shortcut abstractions. | UndoAsync, RedoAsync, OnAfterRenderAsync |
| src/CanDoItAll.Modules.Factory/FactoryDomain.cs#L359-L536 | Prompt Factory domain models, including run node summaries, attachment summaries, setup profile, and editor state persisted with CanvasUiStateJson. | PromptRunNodeSummary, PromptSessionAttachmentSummary, PromptSessionSetupProfile, PromptFactoryEditorModel |
| src/CanDoItAll.Modules.Factory/PromptFactoryService.cs#L238-L715 | Prompt Factory domain service handling recommendations, state persistence, branching, build/export/send flows, and canvas UI-state persistence. | GetRecommendedBlockIdsAsync, SaveSessionStateAsync, SaveCanvasUiStateAsync, BranchNodeAsync, UpdateNodeAsync, SetNodeStateAsync... |
| src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs#L128-L806 | Domain models and service layer for project structure and project calendar, including persistence of links, node movement, UI state, and graph synchronization. | ProjectStructureNode, ProjectStructureLink, ProjectStructureSurface, ProjectCalendarEvent, ProjectCalendarSurface, ProjectObjectCreateRequest... |

## Navigation advice

Start with the first file that already owns the most behavior for this component, then inspect the wrapper/page/service files that consume that behavior.

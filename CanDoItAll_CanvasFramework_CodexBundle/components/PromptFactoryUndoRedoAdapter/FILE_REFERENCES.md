# PromptFactoryUndoRedoAdapter File References

| Path | Why it matters | Key symbols / areas |
| --- | --- | --- |
| src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.History.cs#L1-L234 | Prompt Factory undo/redo stack and keyboard shortcut registration. Valuable evidence for shared history and shortcut abstractions. | UndoAsync, RedoAsync, OnAfterRenderAsync |
| src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866 | Main prompt builder/editor page. Uses the shared workbench shell but still builds graph projection, branch lanes, attachments, and UI-state persistence inline in the page. | BuildCanvasNodes, BuildCanvasLinks, HandleCanvasSelectionChangedAsync, HandleCanvasNodesMovedAsync, HandleCanvasStateChangedAsync, PersistCanvasUiStateAsync... |
| src/CanDoItAll.Modules.Factory/FactoryDomain.cs#L359-L536 | Prompt Factory domain models, including run node summaries, attachment summaries, setup profile, and editor state persisted with CanvasUiStateJson. | PromptRunNodeSummary, PromptSessionAttachmentSummary, PromptSessionSetupProfile, PromptFactoryEditorModel |
| src/CanDoItAll.Modules.Factory/PromptFactoryService.cs#L238-L715 | Prompt Factory domain service handling recommendations, state persistence, branching, build/export/send flows, and canvas UI-state persistence. | GetRecommendedBlockIdsAsync, SaveSessionStateAsync, SaveCanvasUiStateAsync, BranchNodeAsync, UpdateNodeAsync, SetNodeStateAsync... |

## Navigation advice

Start with the first file that already owns the most behavior for this component, then inspect the wrapper/page/service files that consume that behavior.

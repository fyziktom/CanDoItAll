# EmptyStateOverlay File References

| Path | Why it matters | Key symbols / areas |
| --- | --- | --- |
| src/CanDoItAll.ComponentKit/Components/CanvasWorkbenchStage.razor#L1-L82 | Shared shell used by Project Structure and Prompt Factory for eyebrow/title copy, stats, canvas slot, inspector slot, and supporting panels. | CanvasWorkbenchStage |
| src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor#L1-L1399 | Primary graph-editor page for project structure. Wires the shared workbench shell, builds nodes/actions, persists positions and view state, hosts inspector panels, and still contains several canvas-specific policies that should move into shared/domain adapter components. | MapCanvasNode, HandleSelectionChangedAsync, HandleNodesMovedAsync, HandleCanvasStateChangedAsync, HandleCreateActionAsync, HandleCanvasActionAsync... |
| src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866 | Main prompt builder/editor page. Uses the shared workbench shell but still builds graph projection, branch lanes, attachments, and UI-state persistence inline in the page. | BuildCanvasNodes, BuildCanvasLinks, HandleCanvasSelectionChangedAsync, HandleCanvasNodesMovedAsync, HandleCanvasStateChangedAsync, PersistCanvasUiStateAsync... |
| docs/canvases-improvements/01-reference-and-gap-analysis.md#L1-L203 | Existing internal analysis of reference workbench behavior and gaps. | Reference capability inventory, Page shell and layout, Canvas host and chrome |

## Navigation advice

Start with the first file that already owns the most behavior for this component, then inspect the wrapper/page/service files that consume that behavior.

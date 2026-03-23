# PromptFactoryCatalogToolbox File References

| Path | Why it matters | Key symbols / areas |
| --- | --- | --- |
| src/CanDoItAll.Modules.Factory/PromptFactoryCanvasCatalog.cs#L1-L645 | Prompt Factory domain create/action catalog used to populate canvas context actions and create menus. | BuildSessionContextActions, BuildSelectionContextActions, BuildComponentNodeActions, BuildInputNodeActions |
| src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866 | Main prompt builder/editor page. Uses the shared workbench shell but still builds graph projection, branch lanes, attachments, and UI-state persistence inline in the page. | BuildCanvasNodes, BuildCanvasLinks, HandleCanvasSelectionChangedAsync, HandleCanvasNodesMovedAsync, HandleCanvasStateChangedAsync, PersistCanvasUiStateAsync... |
| src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.Catalog.cs#L1-L1090 | Selection graph builder and catalog behavior for the prompt factory canvas. | BuildSelectionGraph, HandleCatalogContextActionAsync, HandleCatalogCreateActionAsync |
| src/CanDoItAll.ComponentKit/Canvas/CanvasWorkbenchContracts.cs#L5-L340 | Defines the shared graph-workbench DTO layer: surface, nodes, links, UI state, chrome, actions, chips, group frames, and typed callback payloads. | CanvasWorkbenchSurface, CanvasWorkbenchNode, CanvasWorkbenchLink, CanvasWorkbenchUiState, CanvasWorkbenchChrome, CanvasWorkbenchAction... |

## Navigation advice

Start with the first file that already owns the most behavior for this component, then inspect the wrapper/page/service files that consume that behavior.

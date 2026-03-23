# ProjectStructureActionCatalogAdapter File References

| Path | Why it matters | Key symbols / areas |
| --- | --- | --- |
| src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.cs#L1-L326 | Domain action catalog and label resolver for Project Structure create flows and inspector create groups. | ResolveNodeLabel, BuildMenuCreateActions, BuildInspectorCreateGroups, TryResolveCreateDefinition |
| src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor#L1-L1399 | Primary graph-editor page for project structure. Wires the shared workbench shell, builds nodes/actions, persists positions and view state, hosts inspector panels, and still contains several canvas-specific policies that should move into shared/domain adapter components. | MapCanvasNode, HandleSelectionChangedAsync, HandleNodesMovedAsync, HandleCanvasStateChangedAsync, HandleCreateActionAsync, HandleCanvasActionAsync... |
| src/CanDoItAll.ComponentKit/Canvas/CanvasWorkbenchContracts.cs#L5-L340 | Defines the shared graph-workbench DTO layer: surface, nodes, links, UI state, chrome, actions, chips, group frames, and typed callback payloads. | CanvasWorkbenchSurface, CanvasWorkbenchNode, CanvasWorkbenchLink, CanvasWorkbenchUiState, CanvasWorkbenchChrome, CanvasWorkbenchAction... |

## Navigation advice

Start with the first file that already owns the most behavior for this component, then inspect the wrapper/page/service files that consume that behavior.

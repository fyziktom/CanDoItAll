# Current State

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\ContextualAgentWorkspaceWindows.razor` owns the contextual floating agent list plus the contextual chat floating window used by both project structure and processes.
- Agent list rows are currently single `<button>` elements; adding a nested icon button requires restructuring to avoid invalid nested interactive markup.
- The contextual chat path already calls `WorkspaceService.ExecuteRunAsync` with project/process invocation context and reloads the selected chat workspace after a successful send or approval continuation.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor` reloads project structure data through `ReloadSurfaceAsync` and rebuilds the shared `CanvasWorkbenchSurface` through `RefreshCanvasSurface`.
- Project structure stores persisted/live canvas state in `currentViewStateJson`, including `CanvasWorkbenchUiState.WindowStates`, pan, zoom, minimap, diagnostics, and selected nodes.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.CanvasState.cs` stores separate definition/runtime `CanvasWorkbenchUiState` instances and rebuilds the process canvas surface from the process editor or run details.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\Workbench\CanvasWorkbench.razor` has JS interop for state, selection, fit, focus, and image export, but no public method to return the current full state JSON to a parent before external reload.
- Agent workspace APIs already expose chat session lists, selected chat workspace snapshots, execution run queries, execution run details, and tool receipts through `IAgentFrameworkWorkspaceService`.

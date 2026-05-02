# Target Solution

- Add a small refresh event contract to `ContextualAgentWorkspaceModels.cs` and an `EventCallback` parameter to `ContextualAgentWorkspaceWindows`. The shared component invokes it after a successful contextual run or approval continuation, passing workspace kind, target id, agent id, session id, and execution run id.
- Add a public `GetStateJsonAsync` method to `CanvasWorkbench` so project/process hosts can capture live JS state immediately before reload. This prevents pending debounce timing from losing the latest pan or zoom.
- Wire project structure to handle refresh requests by capturing the current workbench state into `currentViewStateJson`, then calling the existing `ReloadSurfaceAsync` path.
- Wire processes to handle refresh requests by capturing the current workbench state into the active definition/runtime `CanvasWorkbenchUiState`, then calling the existing `LoadWorkspaceAsync` path.
- Restructure contextual agent rows from a single all-content button into an article with a main open button and a compact history icon button. Keep the visual style close to the current row.
- Add an `AgentThreadHistoryDialog` component that receives an agent plus recent session summaries and returns a selected session id through `DialogService.CloseAsync`.
- Add a contextual chat export builder inside the shared window that queries latest 25 full sessions and execution run details, then downloads indented JSON through a small static JS Blob helper.
- Keep provider/runtime execution behavior unchanged; this bundle only refreshes UI surfaces and exposes existing session/history data.

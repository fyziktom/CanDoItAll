# Structured Input

## Core Objective

- Make contextual agent work inside project-structure and process canvas floating windows easier to continue and debug: refresh the canvas after agent runs, reopen recent agent threads from the agent list, and export recent thread/runtime/tool history as JSON.

## Hard Constraints

- Preserve canvas location, zoom, selection, and open floating-window state during automatic refresh.
- Support both project-structure and process contextual agent windows.
- Show the latest 25 threads for the chosen agent.
- Double-clicking a history row opens that agent chat floating window on that exact thread.
- JSON export must include runtime evidence such as execution log, metrics, approvals, artifacts, checkpoints, and tool receipts where available.
- Keep controls tiny/icon-style and aligned with the existing BaseLib/CanvasLib UI.

## Source Artifacts

- `inputs/00-original-request.md`
- Existing shared contextual agent window: `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\ContextualAgentWorkspaceWindows.razor`
- Existing project canvas host: `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- Existing process canvas host: `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceStepsTab.razor`

## Input Coverage Signals

- N001 auto-refresh after contextual agent changes in project structure and processes.
- N002 preserve location, zoom, and open floating windows during refresh.
- N003 agent row/card compact history button opens latest 25 threads.
- N004 double-clicking a history thread opens the floating agent chat on that thread.
- N005 chat floating window compact JSON download includes tool/runtime debug history.

## Dependency And Sequencing Signals

- Refresh preservation is the foundation; history and export UI should not destabilize canvas state.
- Thread history dialog depends on the existing workspace chat-session projection.
- JSON export depends on the existing execution-history reader APIs and browser download interop.

## Validation Expectations

- Prepared bundle validator must pass before implementation.
- Component tests should cover the new history dialog and JSON export projection where practical.
- Targeted build/test commands must pass.
- Browser proof must open a contextual agents floating window, show the history dialog, open a thread, and verify refresh-related windows remain open after simulated or real reload behavior where feasible.

## UI Validation Strategy

- Use Playwright MCP against a project-structure or process canvas route at a large viewport first. Open the agents window, inspect the compact history and export buttons, open the history dialog, and verify dialog readability/layering. Follow with a narrower viewport if the controls wrap or clip.

## Browser Validation Analytics

- Record route, viewport, actions, screenshots, and result in `reviews/01-execution-report.md` for each UI-relevant subbundle. The open-state history dialog and chat floating-window button must be included in proof.

## Working Assumptions

- Contextual agent runs complete through `ContextualAgentWorkspaceWindows.SendMessageAsync` or approval continuation in the same component.
- `CanvasWorkbenchUiState` is the source for pan, zoom, selection, minimized state, and window geometries.
- The latest 25 threads are determined by `UpdatedAtUtc` descending.
- Exporting the latest 25 threads satisfies the debug request for practical browser downloads.

## Primary Risks

- Rebuilding `CanvasWorkbenchSurface` could accidentally reset live JS pan/zoom if the latest browser state is not captured.
- Adding a button inside an existing button-like agent row would create invalid markup and click conflicts.
- JSON export can become large; it should use a Blob download rather than a data URL.

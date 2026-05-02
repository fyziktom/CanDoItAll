# Canvas Refresh Callback

## Status

- Status: `Completed`

## Objective

- Add a shared contextual-agent refresh callback and wire project-structure/process canvas hosts to reload data after a successful contextual run while preserving live canvas state.

## Covered Inputs

- N001 automatic refresh after contextual agent changes in project structure or processes.
- N002 preserve canvas location, zoom, selection, and open floating windows.

## Prerequisites

- Prepared bundle validator passes.
- No earlier implementation subbundle is required.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\ContextualAgentWorkspaceWindows.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\ContextualAgentWorkspaceModels.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\Workbench\CanvasWorkbench.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.AgentWindows.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.CanvasState.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.StepsPresenter.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceStepsTab.razor

## Deliverables

- Refresh request contract and callback parameter on the shared contextual agent window.
- Project-structure refresh handler that captures current workbench state and calls the existing reload path.
- Process refresh handler that captures current workbench state and calls the existing load path.
- Public `CanvasWorkbench` state-read helper for parent preservation.

## Dependency Impact

- Subbundles 02 and 03 share the same contextual component; if refresh wiring destabilizes component state, history and export UI proof is unreliable.
- The requested preservation behavior is user-visible and critical; weak proof here invalidates final closure.

## Validation Depth

- Critical UI foundation with component/build validation and browser proof.

## Implementation Steps

1. Add a refresh event model to `ContextualAgentWorkspaceModels.cs`.
2. Add an `EventCallback` to `ContextualAgentWorkspaceWindows`.
3. Invoke the callback after successful contextual send and approval continuation.
4. Add `CanvasWorkbench.GetStateJsonAsync`.
5. Wire project-structure parent handler.
6. Wire process parent presenter and workspace handler.
7. Add or update focused tests where the codebase allows.

## Scope Exceptions

- None planned.

## Do Not Do

- Do not change agent runtime execution semantics.
- Do not force a fit-view refresh.
- Do not discard unsaved canvas UI state.
- Do not implement history dialog or export controls in this subbundle.

## Acceptance Checklist

- Project contextual runs request a surface refresh after successful completion/continuation.
- Process contextual runs request a surface refresh after successful completion/continuation.
- Live canvas state can be captured before reload.
- Reload uses existing data-loading paths and preserves UI state.

## Proof Required

- Targeted `dotnet test` for affected component/process/project tests, or document exact blockers.
- `dotnet build` or targeted project build.
- Browser proof on a canvas route showing agents window remains open and viewport state is preserved across a refresh-triggering action or simulated handler.

## Browser Validation Logging

- Route: project-structure canvas route or process steps canvas route with contextual agents available.
- Viewport: large desktop first; narrower width only if changed controls affect layout.
- Actions: open agents floating window, pan/zoom canvas, trigger or simulate contextual refresh, assert agents window remains open and canvas state is not reset.
- Screenshots: record in `output/playwright/` when available.
- Review: no clipped floating windows, no unexpected fit-view, no closed agent/chat windows.

## Progression Gate

- Downstream subbundles may continue only after the refresh callback compiles and preservation proof is recorded or a concrete environment blocker is documented.

## Suggested Agent Prompt

```text
Implement the refresh callback and parent preservation handlers only. Do not add history dialog or JSON export controls yet.
```

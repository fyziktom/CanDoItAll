# Process Workspace Integration

## Status

- Status: `Completed`

## Objective

- Add the contextual Agents launcher and chat windows to the process definition canvas.

## Covered Inputs

- R2: Processes page exposes the Agents floating-window toggle.
- R7-R9 and R11: Double-click agent, new thread, existing chat functions, review-role prompt proof.

## Prerequisites

- `01-shared-contextual-agent-window-contract` completed and trusted.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Canvas.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.StepsPresenter.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceStepsTab.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessCanvasToolbarActions.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\CanDoItAll.Modules.Processes.csproj

## Deliverables

- Process canvas toolbar Agents icon.
- Process canvas agent window state.
- Shared contextual component rendered in the Steps canvas `OverlayContent`.
- Selected process definition id passed for access filtering when available.

## Dependency Impact

- Validation depends on this integration to test the review-role process prompt flow.
- Incorrect presenter plumbing can break the process Steps canvas toolbar.

## Validation Depth

- UI host integration with build and browser proof.

## Implementation Steps

1. Add AgentFramework component reference to Processes if needed.
2. Add window state and toggle methods to `ProcessWorkspace.Canvas.cs`.
3. Expose state/toggle/process id through `ProcessWorkspaceStepsTabPresenter`.
4. Add Agents toolbar button to `ProcessCanvasToolbarActions`.
5. Render `ContextualAgentWorkspaceWindows` in `ProcessWorkspaceStepsTab.razor`.

## Scope Exceptions

- Runtime Runs canvas is not in scope for this request; the requested process mindmap validation uses the Steps definition canvas.

## Do Not Do

- Do not change role/step editor semantics.
- Do not change process access metadata storage.

## Acceptance Checklist

- Agents icon appears in process Steps canvas toolbar.
- Clicking icon opens the launcher.
- Double-clicking an allowed agent opens chat.
- Existing toolbox, selection, and editor windows still work.

## Proof Required

- Build success.
- Playwright screenshot of launcher and chat on process route.

## Browser Validation Logging

- Record route, viewport, actions, assertions, screenshot paths, and result in `reviews/01-execution-report.md`.

## Progression Gate

- Process launcher/chat works without clipping or z-order problems and downstream validation can send the review-role prompt.

## Suggested Agent Prompt

```text
Integrate the shared contextual agent component into the process Steps canvas using the shared implementation prompt.
```

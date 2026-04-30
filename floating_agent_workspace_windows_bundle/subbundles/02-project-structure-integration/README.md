# Project Structure Integration

## Status

- Status: `Completed`

## Objective

- Add the contextual Agents launcher and chat windows to the project structure canvas.

## Covered Inputs

- R1: Project structure canvas exposes the Agents floating-window toggle.
- R7-R10: Double-click agent, new chat thread, existing chat functions, calculator-roadmap prompt proof.

## Prerequisites

- `01-shared-contextual-agent-window-contract` completed and trusted.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.ToolWindows.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureToolbarActions.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\CanDoItAll.Modules.Workbench.csproj

## Deliverables

- Project structure toolbar Agents icon.
- Persisted project structure agent window state.
- Shared contextual component rendered in project structure `OverlayContent`.
- Project id passed to the shared component for access filtering.

## Dependency Impact

- Validation depends on this integration to test the calculator roadmap project flow.
- Broken project host integration should not block process implementation if the shared component remains correct, but final closure cannot pass.

## Validation Depth

- UI host integration with build and browser proof.

## Implementation Steps

1. Add AgentFramework component reference to Workbench.
2. Add toolbar visibility/toggle parameters.
3. Add window state key and toggle/state handlers.
4. Render `ContextualAgentWorkspaceWindows` in project structure overlay content.
5. Pass `ContextualAgentWorkspaceKind.ProjectStructure` and `ProjectId`.

## Scope Exceptions

- The projects card board does not receive the launcher; the requested working context is project structure/mindmap.

## Do Not Do

- Do not alter existing toolbox, signals, health, or selection behavior.
- Do not hide existing toolbar actions to make room.

## Acceptance Checklist

- Agents icon appears in project structure toolbar.
- Clicking icon opens the launcher.
- Double-clicking an allowed agent opens chat.
- Existing structure toolbox still opens.

## Proof Required

- Build success.
- Playwright screenshot of launcher and chat on project structure route.

## Browser Validation Logging

- Record route, viewport, actions, assertions, screenshot paths, and result in `reviews/01-execution-report.md`.

## Progression Gate

- Project launcher/chat works without clipping or z-order problems and downstream validation can send the calculator-roadmap prompt.

## Suggested Agent Prompt

```text
Integrate the shared contextual agent component into ProjectStructurePage using the shared implementation prompt.
```

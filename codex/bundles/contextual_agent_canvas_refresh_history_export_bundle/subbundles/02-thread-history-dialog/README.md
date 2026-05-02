# Thread History Dialog

## Status

- Status: `Completed`

## Objective

- Add a compact history icon on each contextual agent row and a dialog listing the latest 25 threads for that agent; double-clicking a row opens the contextual chat floating window on that thread.

## Covered Inputs

- N003 history icon and latest 25 thread dialog.
- N004 double-click history thread opens the agent floating chat on that thread.

## Prerequisites

- `subbundles/01-canvas-refresh-callback` completed or not blocking shared contextual component edits.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\ContextualAgentWorkspaceWindows.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\ContextualAgentWorkspaceWindows.razor.css
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\AgentSwitchDialog.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Contracts\Contracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Conversations\ConversationModels.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\AgentChatModalTests.cs

## Deliverables

- New `AgentThreadHistoryDialog` component and scoped CSS if needed.
- Agent-row markup refactor with separate main open button and compact history icon button.
- History dialog loading path using existing workspace chat summaries.
- Thread selection path that loads the selected session and opens the chat floating window.

## Dependency Impact

- Export controls share the contextual chat header and selected session state; thread reopening must load the intended session before export proof can be trusted.

## Validation Depth

- UI workflow with component-test and browser open-state proof.

## Implementation Steps

1. Add history dialog component that accepts agent and session summaries and returns a session id.
2. Refactor agent row markup to avoid nested interactive controls.
3. Add compact icon button with tooltip/title/aria label.
4. Load latest 25 sessions for the selected agent and open the dialog.
5. On dialog double-click result, load the selected chat session and open the floating chat window.
6. Add focused component tests for dialog rendering, sorting, and double-click result.

## Scope Exceptions

- None planned.

## Do Not Do

- Do not change the default row double-click behavior that opens a new contextual thread.
- Do not add JSON export behavior in this subbundle.
- Do not render buttons inside buttons.

## Acceptance Checklist

- Each agent row/card exposes a tiny history icon action.
- History dialog shows at most 25 threads, newest first.
- Double-clicking a thread returns its id and opens the chat floating window on that thread.
- Empty history state is readable.
- Existing select and double-click new-thread behavior still works.

## Proof Required

- Component test for `AgentThreadHistoryDialog`.
- Targeted build/test command.
- Browser proof opening the contextual agent window, opening history dialog, and verifying open dialog layering/readability.

## Browser Validation Logging

- Route: project-structure or process canvas with contextual agents.
- Viewport: large desktop, narrower width if row actions wrap.
- Actions: open agents window, click history icon, inspect dialog, double-click a row if seed data exists.
- Screenshots: open history dialog state.
- Review: content readable, no clipping, no lateral overflow, dialog layers above floating windows.

## Progression Gate

- Proceed after component tests pass and dialog open-state proof is captured or blocked with a concrete seed-data/provider note.

## Suggested Agent Prompt

```text
Implement the agent-row history icon and latest-25 thread history dialog only. Preserve existing new-thread behavior.
```

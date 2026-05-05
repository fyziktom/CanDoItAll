# Manager Chat UI

## Status

- `Completed`

## Objective

Add the `Manager chat` tab after `Exchange`, reuse `ChatWorkspacePanel`, and provide a modal for selecting the process run the user wants to discuss.

## Covered Inputs

- Add tab after Exchange.
- Standard chat with responsible manager agent.
- Button opens modal to choose a process run.

## Prerequisites

- Manager chat architecture boundary is accepted.
- Process page can load definitions and run summaries.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\ChatWorkspacePanel.razor`

## Deliverables

- New tab label and backing detail-tab key.
- Manager chat panel with agent badges and selected run badge.
- Run selector modal with selectable run summaries.
- Prompt/invocation context includes selected run.

## Dependency Impact

- Browser validation depends on this change.
- Small-app validation can use the manager chat to ask for blockers.

## Validation Depth

- Targeted process module build.
- Browser proof of the tab and modal.

## Implementation Steps

1. Add `Manager chat` after `Exchange`.
2. Add component-local manager chat state and service calls.
3. Add run selector modal.
4. Handle send, approve, rename, and refresh through AgentFramework APIs.

## Do Not Do

- Do not add a separate manager-chat persistence model.
- Do not load all run details just to populate the selector.

## Acceptance Checklist

- Tab appears after Exchange.
- Chat opens with the responsible manager when bound.
- Run selector opens and selects a run.
- Prompt context includes process and selected run details.

## Proof Required

- Build proof.
- Browser screenshot/action proof for tab and modal.

## Browser Validation Logging

- Record route, viewport, actions, assertions, screenshot paths, and result.

## Progression Gate

- Continue only when the browser proof shows readable, unclipped tab and modal content.

## Suggested Agent Prompt

Add the process manager chat tab and run selector modal using existing CanDoItAll components and AgentFramework chat services. Keep state local and strongly typed.

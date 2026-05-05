# Manager Chat Architecture

## Status

- `Completed`

## Objective

Design manager chat so process UI can talk to the responsible process manager without duplicating chat persistence, manager assignment state, or run state.

## Covered Inputs

- Add standard manager chat in process detail.
- Select a specific process run for manager conversation.
- Prevent split source of truth with many process/subprocess runs.

## Prerequisites

- Existing subprocess/manager work is present.
- Existing AgentFramework chat contracts are available.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\ChatWorkspacePanel.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Contracts\Contracts.cs`

## Deliverables

- Manager resolution rule documented in code.
- UI state kept component-local.
- AgentFramework chat remains canonical.

## Dependency Impact

- Manager chat UI depends on this source-of-truth decision.
- Real validation depends on invocation metadata being process/run-aware.

## Validation Depth

- Source inspection plus targeted build.
- Confirm no process-specific chat persistence is introduced.

## Implementation Steps

1. Resolve the responsible manager technical agent from run, override, or manager directory option.
2. Represent selected run as prompt/invocation context only.
3. Use AgentFramework chat session APIs for all chat messages and execution logs.

## Do Not Do

- Do not add process chat tables.
- Do not auto-select an unrelated manager if no bound technical agent is available.

## Acceptance Checklist

- Manager chat source of truth is AgentFramework.
- Process runtime remains the source of process/run state.
- Missing manager binding is visible and actionable.

## Proof Required

- Build proof for process module.
- Source review note in the execution report.

## Browser Validation Logging

- No browser proof required for architecture-only work.

## Progression Gate

- Continue only when no duplicate chat persistence or manager state is added.

## Suggested Agent Prompt

Implement manager-chat architecture boundaries inside the process workspace. Reuse AgentFramework chat APIs and keep selected process run as context metadata only.

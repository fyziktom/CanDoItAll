# Thread History JSON Export

## Status

- Status: `Completed`

## Objective

- Add a compact JSON download action to the contextual agent chat floating window that exports recent agent thread history with runtime/tool evidence for debugging.

## Covered Inputs

- N005 download all agent thread history as JSON, including tool calls and runtime evidence.

## Prerequisites

- `subbundles/01-canvas-refresh-callback` complete.
- `subbundles/02-thread-history-dialog` complete or not blocking contextual chat selected-session state.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\ContextualAgentWorkspaceWindows.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Contracts\Contracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Conversations\ConversationModels.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\App.razor
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\AgentChatModalTests.cs

## Deliverables

- Compact icon-style export button in contextual chat floating-window header actions.
- Export payload builder for all saved selected-agent sessions and per-run details.
- Static JS Blob download helper loaded by the web app.
- Tests or targeted validation for payload shape and button rendering where practical.

## Dependency Impact

- This is the final debug-output feature. Weak proof leaves the user unable to provide future high-quality diagnostics.

## Validation Depth

- Debug-data closure with component/build validation and browser action proof.

## Implementation Steps

1. Add a compact export button beside runtime details in the contextual chat header actions.
2. Build a JSON payload that includes agent metadata, workspace context, export timestamp, all saved selected-agent sessions, messages, runs, execution logs, metrics, approvals, artifacts, checkpoints, and tool receipts.
3. Add a JS Blob download helper and load it from the app shell.
4. Wire notification and disabled/busy states.
5. Add payload-shape tests if a pure helper is introduced, otherwise use targeted build plus browser proof.

## Scope Exceptions

- Export is intentionally broader than the dialog: it includes all saved selected-agent threads because the raw request asked for all agent thread history for debugging.

## Do Not Do

- Do not add server-side file persistence for the export.
- Do not expose credentials or provider secrets.
- Do not omit tool receipts or approvals from available run details.

## Acceptance Checklist

- Button is compact/icon-style with accessible label/title.
- Button is disabled when no agent/session context exists.
- Downloaded JSON includes session messages and run evidence.
- Tool receipts are present for runs that have them.
- Export filename is agent/thread-context friendly and ends in `.json`.

## Proof Required

- Targeted build/test command.
- Inspect generated JSON payload in a test or browser-evaluated download helper hook.
- Browser proof showing export button in the floating chat window without clipping.

## Browser Validation Logging

- Route: project-structure or process canvas with a contextual chat window.
- Viewport: large desktop.
- Actions: open agents window, open chat, assert export button exists, click if JS download can be safely observed.
- Screenshots: contextual chat header with export button.
- Review: button does not overcrowd or overlap runtime details/header actions.

## Progression Gate

- Final closure may proceed only when payload shape includes all requested runtime/tool sections and UI proof is recorded.

## Suggested Agent Prompt

```text
Implement the JSON export button, payload, and browser download helper only. Keep export scoped to latest 25 threads.
```

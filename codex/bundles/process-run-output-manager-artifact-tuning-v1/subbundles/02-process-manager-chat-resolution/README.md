# Process Manager Chat Resolution

## Status

- `Completed`

## Objective

- Make the Processes page Manager tab resolve the selected run's manager using the same assignment-aware logic as live process manager chat.

## Success Criteria

- A selected run with `ManagerAgentName = Default process manager`, no configured manager id, multiple manager-like options, and one unique manager assignment resolves a technical manager agent.
- Ambiguous manager assignments or fallbacks remain unresolved.
- The manager chat load path no longer uses a stale local resolver that diverges from `ProcessManagerChatService`.

## Covered Inputs

- R2 Manager Chat Resolution.
- Raw note 2.

## Prerequisites

- Prepared bundle validator passes.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.ManagerChat.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\Observation\ProcessManagerChatService.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessRunAutomationDispatchServiceTests.cs`

## Deliverables

- Shared manager resolution helper or equivalent deduplication.
- Processes page manager tab uses configured manager, run assignment, and scored fallback precedence.
- Targeted tests for unique assignment and ambiguity.

## Dependency Impact

- Users depend on this to inspect completed process runs and discuss artifacts with the manager.
- Weak proof would leave the Processes page inconsistent with the live dashboard.

## Validation Depth

- Critical UI/runtime support.

## Implementation Steps

1. Add or expose tests around manager resolution for assignment-based selection and ambiguity.
2. Extract shared resolver logic from `ProcessManagerChatService` if this keeps the code smaller and less divergent.
3. Update `ProcessWorkspace.ManagerChat` to load selected run details and resolve through the shared precedence.
4. Run targeted tests and, if possible, open the Manager tab against the live run.

## Scope Exceptions

- Does not change manager assignment creation or HR matching.

## Do Not Do

- Do not silently choose a manager from ambiguous candidates.
- Do not require a new user configuration step for runs that already have a unique assigned manager.

## Acceptance Checklist

- Assignment-scored manager beats ambiguous fallback.
- Exact configured manager id/name still wins.
- Manager chat load error is not shown for the live run shape.

## Proof Required

- Targeted .NET tests for manager resolver behavior.
- Browser/API smoke for the Processes page Manager tab if the app is available.

## Browser Validation Logging

- Route: `/projects/7330105d-8450-4c80-923b-5c27d8e63d6c/processes?processId=672935c3-f687-4255-b8bf-90528248c642&runId=801f259d-8a52-41b8-a99f-cc96a2fc1947`
- Viewport: large desktop.
- Actions: open Manager tab, select run if needed, assert manager chat composer or agent label appears and unresolved-manager error does not.
- Screenshot: optional evidence path under bundle proof if browser tool is available.

## Progression Gate

- Passed. Resolver tests pass and live UI smoke resolved the selected completed run to `Delivery Manager` with a ready composer.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```

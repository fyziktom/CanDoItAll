# 04 Outbox Deadletter And Run Health Operations

## Status

- `Implemented and validated`

## Objective

Expose automation outbox health in Process Workspace so pending, retrying, leased, failed, and dead-lettered dispatch records become actionable process run state.

## Covered Inputs

- REQ-002: UI exposes actionable run state.
- REQ-004: Outbox status is visible.
- REQ-009: Dead-letter state creates process health signal.
- REQ-012: Existing component patterns are preserved.

## Prerequisites

- Subbundle 01 operator health surface is complete.
- Existing outbox integration tests are passing before changes.
- Process service boundaries for read-only operational state are agreed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessOutbox.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRunRecoveryWorker.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Runtime.RunStart.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Runtime.StepTransitions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.RuntimeReadQuery.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunDetailsLoader.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunsActiveSection.razor`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessOutboxIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessWorkspaceTests.cs`

## Deliverables

- Process runtime read model for automation outbox records linked to a process run and step.
- UI indicators for pending, next retry, leased, completed, and dead-lettered records.
- Dead-letter health signal on run/activity views with last error and attempted command.
- Operator action design for retry/requeue or acknowledge, implemented only if backend policy is clear and tested.
- Tests proving dead-lettered automation is visible and does not look like normal active work.

## Dependency Impact

- Completes the infrastructure health picture required by subbundle 05.
- Must align with subbundle 03 so manual rerun and outbox requeue do not conflict.

## Validation Depth

- Integration tests for outbox read model and dead-letter projection.
- Component tests for UI status rendering.
- Negative tests for pending backoff vs dead-letter state.

## Implementation Steps

1. Add a read-only outbox query for process-run automation records.
2. Include outbox health in selected run details or active run summaries.
3. Render outbox health in Activity/Execution UI without overwhelming the normal happy path.
4. Define whether requeue is in scope; if added, make it auditable and idempotent.
5. Add tests for pending, retrying, leased, completed, and dead-lettered states.
6. Confirm backend E2E still has no dead-lettered outbox records on happy path.

## Do Not Do

- Do not delete dead-lettered records from UI actions.
- Do not hide dead-letter state behind generic failed process status.
- Do not add requeue without concurrency/idempotency tests.
- Do not treat outbox completion as proof the process step succeeded.

## Acceptance Checklist

- Dead-lettered automation is visible on the selected run.
- Pending/backoff automation shows next attempt state.
- Outbox health can be correlated with a step and trigger.
- Happy path remains visually quiet when outbox records complete.
- Tests cover dead-letter display and do not require logs.

## Proof Required

- `ProcessOutboxIntegrationTests` or equivalent focused tests.
- Process Workspace component tests for outbox health rendering.
- Existing deterministic process mock E2E remains green.

## Closure Proof

- Added process-run outbox read models, selected-run outbox ledger, active run outbox counts, and dead-letter run health signals.
- Dead-lettered automation remains visible with command, trigger, attempts, and last error; completed outbox records stay quiet but inspectable.
- Passed `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessRuntimeOperatorReadModelTests"` with 3 tests.
- Passed `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessMockAgentRuntimeIntegrationTests|FullyQualifiedName~ProcessRunAutomationDispatchServiceTests|FullyQualifiedName~ProcessOutboxIntegrationTests"` with 137 tests.

## Browser Validation Logging

- Not required for this subbundle unless requeue controls are added.
- Browser proof for dead-letter state belongs to subbundle 05.

## Progression Gate

- Subbundle 05 may proceed only after outbox retry/dead-letter state is available to the UI and covered by tests.

## Suggested Agent Prompt

```text
Implement subbundle 04 only. Add process-run outbox health read models and UI rendering for pending/retrying/leased/dead-lettered automation dispatch. Do not add destructive outbox actions; add requeue only if it is fully audited and tested.
```

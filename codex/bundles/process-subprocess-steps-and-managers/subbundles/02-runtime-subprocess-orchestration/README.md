# runtime subprocess orchestration

## Status

- `Completed`

## Objective

- Start and observe subprocess child runs idempotently through existing process runtime and dispatch services.

## Covered Inputs

- Subprocess as process step must run as part of a parent process.
- Parent process must report subprocess progress.
- Many process trees may run concurrently, so orchestration must reuse outbox/lease behavior.

## Prerequisites

- `subbundles/01-architecture-source-of-truth-and-schema`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessesService.Runtime.RunStart.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessesService.Runtime.Operations.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessesService.Runtime.StepTransitions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessesService.RuntimeReadQuery.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessRuntimeStateOverviewService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessOutbox.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.Dispatch.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.Execution.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessesServiceIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessRunAutomationDispatchServiceTests.cs`

## Deliverables

- Child run start request support for parent run/step metadata.
- Idempotent subprocess child run creation for subprocess steps.
- Status synchronization from child run outcome to parent subprocess step.
- Runtime read projections that expose child run summary for parent steps.
- Cycle and hierarchy-depth guardrails.

## Dependency Impact

- Manager reports, UI runtime chips, and validation scenarios rely on this subbundle. Incorrect idempotency can create duplicate child runs.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Extend run start request/context for parent metadata.
2. Add idempotent child run lookup/start service method.
3. Update dispatch so subprocess steps use child process orchestration instead of AgentFramework direct execution.
4. Add parent step status synchronization.
5. Update runtime read projections and state overview.
6. Test retry/idempotency, completion, blocked/failure, and cycle handling.
7. Perform architecture revalidation before subbundle 03/04.

## Scope Exceptions

- Manager override selection belongs to subbundle 03.
- Canvas editing belongs to subbundle 04.

## Do Not Do

- Do not create long-lived observer threads per subprocess.
- Do not duplicate canonical child status on `ProcessStepRun`.
- Do not silently start a child run when the subprocess target is missing.

## Acceptance Checklist

- Dispatching the same subprocess step twice creates one child run.
- Parent run views include child run status.
- Child completion completes the parent subprocess step.
- Child blocked/failed/cancelled state is visible to the parent.
- Cycle/depth protections fail predictably.

## Proof Required

- Targeted integration tests for child run creation and parent projection.
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter Process`
- Execution report update with revalidation note.

## Browser Validation Logging

- `N/A`

## Progression Gate

- Continue only after the runtime can run a subprocess independently and as a child without duplicate child runs.

## Suggested Agent Prompt

```text
Implement only runtime subprocess orchestration. Reuse existing process outbox/dispatch loops, prove idempotent child run creation, and update parent projections without adding observer threads.
```

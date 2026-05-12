# 03-process-and-workflow-run-adapters

## Status

- `Completed`

## Objective

- Implement typed SchedulerPlanner launch adapters that start process and workflow runs from scheduled fires and write clear run correlation back to history.

## Success Criteria

- Process schedules launch through `ProcessesService.StartRunAsync`.
- Workflow schedules launch through the AgentFramework execution service.
- Each scheduled fire records the target run id, target run kind, status, timestamps, and failure details.
- Launch behavior is idempotent for duplicate scheduled fire requests.
- Failure paths are explicit and visible in schedule run history.

## Covered Inputs

- SPM-R001, target execution side
- SPM-R009, real target launch side
- SPM-R010, target run correlation side
- SPM-R011
- SPM-R012, adapter logging side

## Prerequisites

- `01-scheduler-domain-and-persistence` complete.
- `02-quartz-db-recovery-and-fire-dispatch` complete through durable fire handler and launcher contract.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessesService.Runtime.RunStart.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessWorkflowRunCoordinator.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Contracts\Contracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Execution\AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Execution\AgentFrameworkWorkspaceExecutionService.Helpers.cs`
- `C:\repositories\CanDoItAll\.codex\bundles\scheduler-planner-module\architecture\01-target-solution.md`

## Deliverables

- `ISchedulerTargetLauncher` implementation that dispatches by typed target kind.
- `ProcessSchedulerTargetLauncher` mapping SchedulerPlanner schedule metadata to `ProcessRunStartRequest`.
- `WorkflowSchedulerTargetLauncher` mapping SchedulerPlanner schedule metadata to `ExecutionRunRequest` or the existing workflow execution contract.
- Explicit error handling for missing target, disabled target, invalid schedule metadata, launch failure, and duplicate fire.
- History updates that store target run ids and status transitions.
- Integration tests with seeded process/workflow targets.

## Dependency Impact

- Subbundle 04 depends on target selector shape, launch status labels, and history projection.
- Final validation depends on real scheduled target execution proof.
- Weak adapter typing here would make history and UI misleading.

## Validation Depth

- `Critical application foundation`

## Implementation Steps

1. Inspect process run-start contract and required fields.
2. Inspect AgentFramework workflow/execution contract and the existing `SchedulerRunId` correlation hook.
3. Define adapter input/output records if not already present.
4. Implement process launcher with explicit validation and schedule correlation.
5. Implement workflow launcher with explicit validation and schedule correlation.
6. Wire concrete launchers into SchedulerPlanner DI.
7. Extend fire handler tests from subbundle 02 to use real adapters with seeded targets.
8. Add failure-path tests for missing targets and launch exceptions.
9. Update execution report with target contract decisions and proof.

## Scope Exceptions

- UI target pickers belong to subbundle 04.
- Quartz persistent-store configuration belongs to subbundle 02.
- Do not redesign process or AgentFramework run APIs unless a minimal correlation parameter is missing.

## Do Not Do

- Do not construct process/workflow requests from unvalidated JSON blobs.
- Do not swallow target launch failures and mark runs successful.
- Do not create a generic reflection-based launcher.
- Do not log sensitive launch metadata.

## Acceptance Checklist

- Process schedule fire starts a process run and records the process run id.
- Workflow schedule fire starts an execution run and records the execution run id.
- Missing target fails predictably with searchable history.
- Duplicate fire request does not start duplicate target runs.
- Logs include schedule id, plan run id, target kind, target id, and dedupe key.

## Proof Required

- `dotnet build C:\repositories\CanDoItAll\CanDoItAll.slnx`
- Integration tests for process scheduled launch.
- Integration tests for workflow scheduled launch.
- Integration tests for duplicate and failure paths.

## Browser Validation Logging

- N/A. This subbundle does not add browser-visible UI.

## Progression Gate

- UI implementation may expose schedule creation only after both target kinds can be launched and correlated from a scheduled fire with deterministic idempotency.

## Suggested Agent Prompt

```text
Implement subbundle 03 only. Add typed process and workflow launch adapters for scheduled fires, preserve explicit failure states, and prove target run correlation. Do not build UI or alter Quartz store configuration except where required to consume existing contracts.
```

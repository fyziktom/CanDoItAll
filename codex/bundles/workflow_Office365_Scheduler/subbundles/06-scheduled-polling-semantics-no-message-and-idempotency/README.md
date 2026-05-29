# 06-scheduled-polling-semantics-no-message-and-idempotency

## Status

- Status: `Completed`

## Objective

Make recurring Office365 email polling safe, no-action friendly, and idempotent.

## Covered Inputs

- R3: no matching email returns no-op success.
- R5: summary workflow writes under configured project/node and then marks processed.
- R6: task workflow writes under configured project/node and then marks processed.
- R7: project writes are idempotent by Office365 message id.
- R10: Scheduler dispatch records NoMessages separately from failures.

## Prerequisites

- SB02 executor output includes route/idempotency/processing context.
- SB03 templates write project output before category mutation.
- SB04/SB05 produce valid Scheduler input JSON for the templates.

## Exact Source References

- `repo://src/CanDoItAll.Modules.SchedulerPlanner/SchedulerPlannerService.cs`
- `repo://src/CanDoItAll.Modules.SchedulerPlanner/SchedulerPlannerModels.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/ProjectStructureWorkflowExecutor.cs`
- `repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/WorkbenchProjectStructureRuntimeGateway.cs`
- `repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureWorkflowNodeService.cs`
- `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365WorkflowExecutor.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProjectStructureWorkflowScenarioHarnessTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/SchedulerPlannerIntegrationTests.cs`

## Scope

- Add explicit no-message route/status semantics that Scheduler treats as successful no-action.
- Add idempotency keys `office365:<message-id>:summary` and `office365:<message-id>:tasks` to project writes.
- Ensure retry after mark-processed failure does not duplicate project outputs.
- Ensure concurrent dispatches for the same message cannot create duplicate project nodes/assets.

## Dependency Impact

- SB07 uses the route/status/idempotency result to display history, retry, and approval state correctly.
- SB08 end-to-end proof depends on this behavior to be safe for recurring schedules.

## Validation Depth

- Critical semantic proof for no-message success, duplicate prevention, write-before-mark retry, and concurrent dispatch.
- Integration tests for fake Graph summary/task workflows and Scheduler no-message dispatch.
- Source assertions proving idempotency key usage in production write paths, not only tests.

## Implementation Steps

1. Normalize no-message workflow result into Scheduler non-failure route/status.
2. Add project write metadata/idempotency support for summary assets and task nodes.
3. Make template/project write paths use `office365:<message-id>:summary` and `office365:<message-id>:tasks`.
4. Add retry and concurrency tests.
5. Record manifest, semantic invariants, source assertions, and anti-stub audit.

## Do Not Do

- Do not retry no-message runs.
- Do not mark the message processed before the project write succeeds.
- Do not use manually seeded success signals as proof of production idempotency.

## Acceptance Checklist

- [x] No-message run is success/no-action and does not update error state.
- [x] First matching run creates project output before the existing mark-processed step.
- [x] Retry after mark failure does not duplicate output.
- [x] Already processed messages remain ignored by the SB02 executor behavior.
- [x] Concurrent dispatches for the same message do not duplicate project outputs.

## Completion Notes

- Scheduler now records `SchedulerPlanRunDispatchStatus.NoMessages` from completed workflow output payloads with `noMessages: true` or `route: "no_messages"`.
- Trigger dedupe treats `NoMessages` as a successful terminal state, so empty polls do not retry as failures.
- Project-structure executor resolves idempotency keys from `$.runContext.office365Processing.idempotencyKey` and appends stable `summary` or `tasks` suffixes.
- Runtime project writes persist `workflowProjectWrite.idempotencyKey` metadata and replay existing nodes for duplicate keys.
- Workbench metadata normalization now preserves the workflow project-write metadata envelope across node families.
- Proof is recorded in `bundle://proof/SB06/manifest.md`.

## Proof Required

- `bundle://proof/SB06/manifest.md`
- `bundle://proof/SB06/semantic-invariants.md`
- Failing-first duplicate/no-message transcripts.
- Passing integration transcripts.
- Source assertion and anti-stub audit transcripts.

## Browser Validation Logging

- N/A unless no-message/idempotency status becomes visible in Scheduler UI during this subbundle. SB07/SB08 own UI history proof.

## Progression Gate

- Continue to SB07 only after Scheduler and project write paths prove no-message and retry/idempotency semantics with production producers and consumers.

## Suggested Agent Prompt

Implement no-message and Office365 message-id idempotency semantics across scheduler/project workflow paths, prove retry and concurrency behavior, and leave artifact-backed proof before observability work.

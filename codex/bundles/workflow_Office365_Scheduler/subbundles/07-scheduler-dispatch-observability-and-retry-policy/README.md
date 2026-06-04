# 07-scheduler-dispatch-observability-and-retry-policy

## Status

- Status: `Completed`

## Objective

Make Scheduler Planner observability, retry, and approval/preapproval behavior fit recurring Office365 polling.

## Covered Inputs

- R3: no matching email is not a failure.
- R10: Scheduler dispatch records NoMessages separately from failures.
- R11: approval/preapproval semantics for scheduled Office365 category mutation are explicit and auditable.

## Prerequisites

- SB06 no-message and idempotency semantics passed closure.
- Existing workflow approval policy and external request runtime behavior are reviewed.

## Exact Source References

- `repo://src/CanDoItAll.Modules.SchedulerPlanner/SchedulerPlannerService.cs`
- `repo://src/CanDoItAll.Modules.SchedulerPlanner/SchedulerPlannerModels.cs`
- `repo://src/CanDoItAll.Modules.SchedulerPlanner/Pages/SchedulerPlannerPage.razor`
- `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowRuntimeManager.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowExternalRequestRuntime.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowExecutorObservability.cs`
- `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365WorkflowExecutor.cs`
- `repo://tests/CanDoItAll.Tests.Integration/SchedulerPlannerIntegrationTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/WorkflowExecutorPolicyObservabilityTests.cs`

## Scope

- Normalize Scheduler run summaries for `processed`, `no_messages`, `failed`, and `waiting_for_approval`.
- Store workflow run id and route in scheduler run details.
- Avoid letting no-message overwrite `LastError`.
- Add retry distinctions for Graph/network failure, no-message, approval waiting, and project write failure.
- Add explicit, scoped, auditable preapproval support only if required for unattended category mutation.

## Dependency Impact

- SB08 final proof depends on run history and retry policy matching the business workflow.
- Approval decisions affect whether scheduled category mutation can be automatic or must pause waiting for input.

## Validation Depth

- Critical semantic proof for route/status normalization, no-message non-failure, retry decisions, approval waiting, and preapproval scope mismatch.
- Unit/integration tests for Scheduler history and approval policy.
- Source assertions proving no silent Scheduler-based approval bypass.

## Implementation Steps

1. Extend Scheduler run detail/summary model as minimally as possible.
2. Normalize workflow route outcomes into Scheduler-visible summaries.
3. Add retry policy distinctions.
4. Implement explicit preapproval only with narrow scope and audit events, or document waiting-for-approval behavior if that is the chosen product contract.
5. Add tests and proof artifacts.

## Do Not Do

- Do not bypass Office365 external-write approval based only on Scheduler launch.
- Do not retry no-message or waiting-for-approval runs as failures.
- Do not erase last error on no-message success.

## Acceptance Checklist

- [x] Scheduler history shows no-message as non-failure/no-action.
- [x] Processed and no-message timestamps are distinguishable from last error.
- [x] Waiting approval run is not retried as a failure.
- [x] Graph/network failure retry behavior remains explicit.
- [x] Preapproval scope mismatch blocks mark-processed or the run waits for approval.

## Closure Notes

- Scheduler run summaries now persist and display route plus retry policy: `processed`, `no_messages`, `failed`, and `waiting_for_approval`.
- `NoMessages` and `WaitingForApproval` are terminal non-retry statuses for Scheduler dedupe.
- No-message runs preserve prior `LastError`; only a successful dispatched run clears it.
- Graph/network failures classify as `TransientExternalFailure`; project-structure write failures classify as `ProjectWriteFailure`.
- Office365 mark-processed remains approval-required for external effects; scheduled workflow launches wait for approval rather than bypassing that policy.
- Browser proof captured `/scheduler` history surface at desktop width; row-level route/policy rendering is covered by component proof because the local development database had no run-history rows.

## Proof Required

- `bundle://proof/SB07/manifest.md`
- `bundle://proof/SB07/semantic-invariants.md`
- Failing-first status/retry/approval transcripts.
- Passing unit/integration transcripts.
- Source assertion and anti-stub audit transcripts.
- Browser proof if history UI changes.

## Browser Validation Logging

- Record `/scheduler` desktop proof if this subbundle changes visible run-history, status badge, or retry controls.

## Progression Gate

- Continue to SB08 only after Scheduler history, retry, and approval behavior can be proven without live Office365 credentials.

## Suggested Agent Prompt

Implement Scheduler route/status/retry/approval observability for recurring Office365 polling, keeping external write approval explicit and audited, then prove the behavior with targeted tests.

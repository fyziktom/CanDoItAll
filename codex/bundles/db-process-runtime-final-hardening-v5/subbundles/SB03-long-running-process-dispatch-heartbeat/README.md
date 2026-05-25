# SB03 - Long-running process dispatch heartbeat

## Status

Completed.

## Objective

Ensure process step dispatch claim and outer process outbox lease are renewed continuously during long AgentFramework/workflow execution.

## Covered inputs

- User asked to remove DB bottlenecks while preserving canonicality.
- Current dispatch claim renewal is callback-driven and may not run while `workspaceService.ExecuteRunAsync(...)` blocks.

## Exact source references

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs`

## Problem

`ExecuteUntilSettledAsync` calls `renewLeaseAsync` at selected points, but not continuously around the long blocking `workspaceService.ExecuteRunAsync(...)` call. If the execution takes longer than `StepDispatchClaimLeaseDuration`, the durable step claim can expire. The stale worker will later be blocked from final mutation, but duplicate long work can be started.

## Implementation summary

Added a scoped `ProcessDispatchLeaseHeartbeat` and started it immediately after a durable step dispatch claim succeeds. The heartbeat renews through the existing combined callback, so both the step dispatch claim and outer process outbox lease are refreshed while long execution is in flight. Renewal loss cancels the dispatch token and stops canonical mutation.

## Deliverables

1. Introduce a scoped heartbeat around long process dispatch work.
2. Heartbeat renews:
   - process step dispatch claim,
   - outer process outbox lease, if present.
3. Heartbeat failure triggers `ProcessDispatchClaimLostException` or equivalent stop condition.
4. Make heartbeat interval configurable or derived from lease duration.
5. Add tests for long-running execution exceeding lease duration.

## Implementation steps

- Create a reusable `ProcessDispatchLeaseHeartbeat` or similar.
- Start it immediately after durable step claim is acquired and before candidate hydration if hydration can be slow.
- Stop it after final state mutation or abort.
- Ensure lost heartbeat suppresses artifact projection, workflow completion, failed-state transition, and branch outcome selection.
- Add deterministic test with fake clock or short lease duration.

## Do not do

- Do not only increase `StepDispatchClaimLeaseDuration`.
- Do not rely on AgentFramework callbacks to renew DB claims.
- Do not allow stale workers to write any canonical mutation.

## Acceptance checklist

- [x] Long-running execution longer than lease duration does not lose claim if owner is alive.
- [x] Heartbeat loss stops canonical mutation.
- [x] Duplicate worker cannot finalize while first worker heartbeat is healthy.
- [x] Tests cover both success and heartbeat-loss paths.

## Proof required

- `proof/SB03/manifest.md`
- `proof/SB03/semantic-invariants.md`
- `proof/SB03/long-running-dispatch-heartbeat-tests.log`

## Browser validation logging

N/A.

## Progression gate

SB04 and SB07 depend on this.

## Suggested agent prompt

Implement SB03. Add a continuous heartbeat for process dispatch claims and outer outbox leases during long AgentFramework/workflow execution. Prove stale workers cannot mutate canonical state.

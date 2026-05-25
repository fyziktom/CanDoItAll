# Semantic invariants SB03

## SB03-I1: active dispatch ownership is continuously renewed

- Source raw note: long AgentFramework execution can block past the durable process dispatch lease duration.
- Expected behavior: the process step dispatch claim is renewed periodically while long execution is in flight.
- Outer outbox behavior: the same heartbeat callback also renews the process outbox lease when dispatch came from an outbox record.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchLeaseHeartbeat.cs` and `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`.
- Positive proof: `bundle://proof/SB03/long-running-dispatch-heartbeat-tests.log`.
- Source proof: `bundle://proof/SB03/dispatch-heartbeat-source-audit.log`.

## SB03-I2: lost heartbeat stops canonical mutation

- Source raw note: stale workers must not project artifacts, select branch outcomes, or complete steps after claim loss.
- Expected behavior: renewal failure cancels the dispatch token and `ProcessDispatchClaimLostException` becomes the stop condition.
- Disallowed shallow implementation: rely only on final `TransitionStepWithClaimAsync(...)` checks after doing artifact projection or recovery work.
- Negative proof: `ProcessDispatchLeaseHeartbeat_cancels_dispatch_when_renewal_fails` in `bundle://proof/SB03/long-running-dispatch-heartbeat-tests.log`.
- Production guard: `DispatchAsync(...)` checks heartbeat loss after execution and after completion-artifact recovery, and canonical write paths receive the heartbeat cancellation token.

## SB03-I3: lease timing is explicit and validated

- Source raw note: hardening must not hide the issue by only lengthening leases.
- Expected behavior: the claim lease duration and heartbeat interval are runtime options with validation that the interval is positive and shorter than the lease.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeOptions.cs` and `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs`.

## Negative proof

`bundle://proof/SB03/long-running-dispatch-heartbeat-tests-failing-first.log` shows the test surface failed before the production heartbeat existed. `bundle://proof/SB03/long-running-dispatch-heartbeat-tests.log` proves renewal failure cancels dispatch and surfaces `ProcessDispatchClaimLostException`.

## Positive proof

`bundle://proof/SB03/long-running-dispatch-heartbeat-tests.log` proves a simulated long-running dispatch continues to receive both step and outer renewal callbacks past the simulated lease window.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| `ProcessStepRun.AutomationDispatchClaimToken` / `AutomationDispatchLeaseExpiresAtUtc` | Step claim and renew methods in `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchLeaseHeartbeat.cs` | `bundle://proof/SB03/long-running-dispatch-heartbeat-tests.log` | Heartbeat-loss test in `bundle://proof/SB03/long-running-dispatch-heartbeat-tests.log` |
| `ProcessOutboxRecord.LeaseToken` / `LeaseExpiresAtUtc` | Outbox renewal callback in `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs` | `CreateDispatchRenewLeaseCallback(...)` in `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` | `bundle://proof/SB03/dispatch-heartbeat-source-audit.log` | Heartbeat-loss cancellation test |

## Anti-stub proof

The source audit verifies that the production dispatcher starts `ProcessDispatchLeaseHeartbeat`, uses `DispatchCancellationToken`, calls `ThrowIfClaimLost`, and validates configurable heartbeat and lease durations.

# Proof manifest SB03

## Status

Completed.

## Owned requirements

- R4: Long-running process dispatch must keep claims alive.
- R8: Broad validation caveats must be closed or classified.
- R9: Process DB tests must red-team canonicality.

## Semantic invariant contract

`bundle://proof/SB03/semantic-invariants.md`

## Changed files

| File | SHA-256 | Reason |
|---|---:|---|
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchLeaseHeartbeat.cs` | `E0557314887CB2C243AF39EFD85485A23D05E67E62E83DE91D8F6BD29D206AA7` | Adds scoped heartbeat with cancellation-on-claim-loss semantics. |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` | `8205AFB6B9DD9671E6AE3D0AA0A5DF8215BA66DB534D66AA8B4D9AF372DCDC61` | Starts heartbeat immediately after durable step claim and before candidate hydration; uses heartbeat token around long dispatch work and canonical writes. |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.cs` | `D9080A1AC5F934AE44A8964B460ADA2559E1C1E82150E1CF7793F18D508803FE` | Removes fixed static step-claim lease duration. |
| `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeOptions.cs` | `59EA0221EDFD3DA132066566B2D9E2093C957ED6EBD61E3382BC948601A8F176` | Adds configurable step dispatch claim lease and heartbeat interval defaults. |
| `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs` | `2B47BCDDDD9CD9240E1C221937090F2DBC16BA34EBD18AB5E63D55791948A966` | Validates heartbeat and claim lease durations at options startup. |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` | `018AECFA5C3C0C6BBF9307DC8142BB4E266E37BE738812063EE976FCCB2B2CA4` | Adds heartbeat success and loss tests. |

## Validation commands

| Command | Result | Transcript |
|---|---|---|
| Focused heartbeat tests before production type | Failed as expected at compile | `bundle://proof/SB03/long-running-dispatch-heartbeat-tests-failing-first.log` |
| Focused heartbeat tests after production fix | Passed, 2 tests | `bundle://proof/SB03/long-running-dispatch-heartbeat-tests.log` |
| Dispatch heartbeat source audit | Passed | `bundle://proof/SB03/dispatch-heartbeat-source-audit.log` |

## Source assertions

- `ProcessDispatchLeaseHeartbeat.Start(...)` runs a periodic renewal task and exposes `DispatchCancellationToken`.
- `DispatchAsync(...)` starts the heartbeat immediately after `TryClaimStepDispatchAsync(...)` succeeds and before `LoadDispatchCandidateAsync(...)`.
- The heartbeat callback is `CreateDispatchRenewLeaseCallback(...)`, which renews the outer process outbox lease if present and then renews the process step dispatch claim.
- `ExecuteUntilSettledAsync(...)`, artifact projection, recovery projection, and completion transition use the heartbeat cancellation token.
- `StepDispatchClaimLeaseDuration` and `StepDispatchHeartbeatInterval` are explicit `ProcessRuntimeOptions` values and invalid combinations fail validation.

## Semantic adequacy

The shallow-pass trap was to renew only at selected callback points around AgentFramework execution. The production dispatcher now has an independent renewal loop for the long blocking section, and claim loss cancels the dispatch token before artifact projection, completion recovery, or terminal transition can mutate canonical process state.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| `ProcessStepRun.AutomationDispatchClaimToken` / `AutomationDispatchLeaseExpiresAtUtc` | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` claim and renew methods | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchLeaseHeartbeat.cs` periodic callback and dispatch cancellation | `bundle://proof/SB03/long-running-dispatch-heartbeat-tests.log` | `bundle://proof/SB03/long-running-dispatch-heartbeat-tests.log` heartbeat-loss test |
| `ProcessOutboxRecord.LeaseToken` / `LeaseExpiresAtUtc` | `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs` passes outer renewal callback | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` `CreateDispatchRenewLeaseCallback(...)` | `bundle://proof/SB03/dispatch-heartbeat-source-audit.log` | `bundle://proof/SB03/long-running-dispatch-heartbeat-tests.log` |

## Residual risks

The focused build emitted retry warnings because an existing .NET host had copied module assemblies locked; the command retried and completed successfully with both targeted tests passing.

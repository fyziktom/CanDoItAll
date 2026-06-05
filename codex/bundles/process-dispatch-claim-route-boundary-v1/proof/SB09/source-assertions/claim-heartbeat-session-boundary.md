# SB09 Source Assertions

- Added `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchGuardLease.cs` to own the per-step in-memory semaphore wait, idempotent release, and released-guard dictionary cleanup.
- `DispatchAsync` now obtains the in-memory guard through `ProcessDispatchGuardLease.WaitAsync`, releases it immediately after durable claim acquisition, and relies on the lease to release/remove guards when no durable claim is acquired.
- Existing durable claim methods remain in `ProcessRunAutomationDispatchService.Dispatch.cs`: `TryClaimStepDispatchAsync`, `RenewStepDispatchClaimAsync`, `EnsureStepDispatchClaimHeldAsync`, and `ReleaseStepDispatchClaimAsync`.
- Existing `ProcessDispatchLeaseHeartbeat` continues to own heartbeat renewal, dispatch cancellation, and `ProcessDispatchClaimLostException` surfacing.
- Focused tests cover guard serialization/removal, empty step id rejection, heartbeat renewal, and claim-lost cancellation.
- No Process Core, production process driver API, UI, or viewport proof artifacts were introduced.

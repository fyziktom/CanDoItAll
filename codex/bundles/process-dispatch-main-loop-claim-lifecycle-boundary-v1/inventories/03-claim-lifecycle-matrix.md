# Claim Lifecycle Matrix

| Claim operation | Current method | New target | Side effects |
| --- | --- | --- | --- |
| Try claim | `TryClaimStepDispatchAsync` | `ProcessDispatchClaimStore.TryClaimAsync` + coordinator | EF ExecuteUpdate increments attempt count |
| Renew | `RenewStepDispatchClaimAsync` | `ProcessDispatchClaimStore.RenewAsync` | EF ExecuteUpdate lease expiry |
| Is held | `IsStepDispatchClaimHeldAsync` | `ProcessDispatchClaimStore.IsHeldAsync` | EF read |
| Ensure held | `EnsureStepDispatchClaimHeldAsync` | `ProcessDispatchClaimCoordinator.EnsureHeldAsync` | Throws claim lost |
| Release | `ReleaseStepDispatchClaimAsync` | `ProcessDispatchClaimStore.ReleaseAsync` | EF ExecuteUpdate clear token |
| Heartbeat | `ProcessDispatchLeaseHeartbeat.Start` call in loop | `ProcessDispatchHeartbeatCoordinator` | renew callback and cancellation token |

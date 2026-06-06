# Claim Lifecycle Matrix

| Lifecycle step | Current owner | Must preserve |
| --- | --- | --- |
| In-memory per-step guard | `DispatchAsync` + `ProcessDispatchGuardLease` | Prevent parallel local dispatch |
| Durable claim acquire | `ProcessDispatchClaimCoordinator` / store | Only Ready/WaitingApproval/InProgress and expired/no lease |
| Durable claim renew | `ProcessDispatchClaimCoordinator` | Extends lease before expiry |
| Heartbeat start/dispose | `RunClaimedDispatchAsync` | Dispose in finally |
| Claim held check | claim coordinator / wrappers | Must precede transition/finalizer/projection side effects |
| Claim release | claim coordinator | Always in finally, tolerate cancellation |
| Claim-lost closure | exception closure | No failure transition after claim loss |

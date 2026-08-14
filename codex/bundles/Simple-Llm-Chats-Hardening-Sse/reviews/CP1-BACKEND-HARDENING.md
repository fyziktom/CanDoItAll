# CP1 — Backend hardening review

State: **Locked until SB06**

## Canonical model

| Criterion | Result | Evidence |
|---|---|---|
| One writable conversation metadata owner | Pending | |
| Create/rename atomic | Pending | |
| Admission atomic | Pending | |
| Success finalization atomic | Pending | |
| Failure/cancel compensation atomic or RecoveryRequired | Pending | |

## Operation lifecycle

| Criterion | Result | Evidence |
|---|---|---|
| Same-ID replay resolved before mutable lifecycle checks | Pending | |
| Cancellation before finalization cannot succeed | Pending | |
| Direct/restart/recovery use one reducer | Pending | |
| Archive cannot race active work | Pending | |
| Attempts have real ordinals and deterministic outcomes | Pending | |

## Runtime/profile

| Criterion | Result | Evidence |
|---|---|---|
| Whole use case is profile fenced | Pending | |
| Durable execution lease supports two hosts | Pending | |
| Client disconnect does not own operation lifetime | Pending | |
| Uncertain possible dispatch is never auto-redispatched | Pending | |
| Cross-instance cancellation works | Pending | |

## Scalability and architecture

| Criterion | Result | Evidence |
|---|---|---|
| Bounded context load | Pending | |
| SQL/keyset transcript and list paging | Pending | |
| No forbidden project references/cycles | Pending | |
| No new service partial cluster | Pending | |
| Focused behavioral tests green | Pending | |

Decision:

- `Ready — unlock SB07 streaming`
- `Not Ready — streaming remains locked`

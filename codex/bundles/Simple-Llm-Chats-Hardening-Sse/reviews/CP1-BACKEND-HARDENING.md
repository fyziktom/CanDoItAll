# CP1 — Backend hardening review

State: **Ready at `a820b867fcf34cd07a93d201a9ffc492c243e647`**

## Canonical model

| Criterion | Result | Evidence |
|---|---|---|
| One writable conversation metadata owner | Ready | SB01 canonical row/transcript ownership plus CP1 22-case Integration union |
| Create/rename atomic | Ready | PostgreSQL failure injection and current-head conversation transaction cases |
| Admission atomic | Ready | SB02 transaction owner and current-head turn transaction cases |
| Success finalization atomic | Ready | Assistant, usage, active-turn clear, and operation state share the fenced UoW |
| Failure/cancel compensation atomic or RecoveryRequired | Ready | Reducer/compensation Unit cases and PostgreSQL rollback cases |

## Operation lifecycle

| Criterion | Result | Evidence |
|---|---|---|
| Same-ID replay resolved before mutable lifecycle checks | Ready | Unit/API idempotency cases in the current-head unions |
| Cancellation before finalization cannot succeed | Ready | Cancellation/reducer Unit cases and real-host PostgreSQL API case |
| Direct/restart/recovery use one reducer | Ready | One application transition reducer; 87-case Unit union |
| Archive cannot race active work | Ready | Locked turn-state/archive cases in the current-head union |
| Attempts have real ordinals and deterministic outcomes | Ready | Invocation audit Unit cases and real-host retained evidence |

## Runtime/profile

| Criterion | Result | Evidence |
|---|---|---|
| Whole use case is profile fenced | Ready | Corrected read-store composition fixture and PostgreSQL profile-switch case |
| Durable execution lease supports two hosts | Ready | Direct two-root PostgreSQL claim/heartbeat cases |
| Client disconnect does not own operation lifetime | Ready | Real-host request-disconnect case in the 22-case union |
| Uncertain possible dispatch is never auto-redispatched | Ready | Lease/recovery Unit cases fail closed to RecoveryRequired |
| Cross-instance cancellation works | Ready | Second-context cancellation observed by durable owner heartbeat |

## Scalability and architecture

| Criterion | Result | Evidence |
|---|---|---|
| Bounded context load | Ready | 2,000-message context case returns 12 entries with fixed command count |
| SQL/keyset transcript and list paging | Ready | Definition/list/transcript keyset command-count case |
| No forbidden project references/cycles | Ready | Source guard plus CodeAnalytics `snap-20260815041852-376a68b7`, zero cycles/diagnostics/errors |
| No new service partial cluster | Ready | Production partial guard passes |
| Focused behavioral tests green | Ready | Unit 87/87; Integration 22/22 at the post-cleanup head |

Decision: **Ready — unlock SB07 streaming.**

The obsolete `ILlmChatConversationEngine.SendAsync`/`SendCoreAsync` path was removed during this
checkpoint. Provider invocation is reachable only through the durable dispatcher-owned
`LlmChatOperationExecutor`; the cross-instance heartbeat context factory remains lifecycle
infrastructure and is not a product-command transaction path.

# PostgreSQL and real-host API evidence

All final commands ran from `C:\repositories\CanDoItAll` against local ephemeral PostgreSQL databases.

| Focused slice | Exit | Result |
|---|---:|---|
| `LlmChatTurnTransactionIntegrationTests` plus `LlmChatOperationDispatchClaimIntegrationTests` | 0 | 4 passed, 0 failed, 0 skipped |
| `LlmChatsApiPostgreSqlIntegrationTests.RealHostPostgreSqlApi_PreservesRevisionsIdempotencyEffortAuditCancellationAndRecovery` | 0 | 1 passed, 0 failed, 0 skipped after the architecture split |

## Failure-injection assertions

- admission rollback removes the operation, pending user message, active turn, and admission evidence;
- success-finalization rollback removes the assistant/result evidence and preserves the active,
  nonterminal operation;
- compensation rollback preserves the active, nonterminal operation rather than exposing a false
  terminal state;
- two independent PostgreSQL repositories admit and claim exactly once.

## Bug caught by the real-host proof

The first real-host run returned HTTP 500 for a stale transcript revision because the refactored
admission facade lost the typed conflict mapping. The exception is now mapped back to the stable
application error and the final real-host proof passes 1/1. The first sandboxed invocation was also
blocked by the LocalAppData control-plane lock; the permitted rerun used the same test and inputs.

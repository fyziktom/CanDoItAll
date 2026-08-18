# SB05 — Operations, idempotency, cancellation, recovery, and audit

Proof tier: **Governed**

## Objective

Make every paid turn durable, retry-safe, cancellable, auditable, and crash-reconcilable.

## Scope

- Implement operation admission with canonical request fingerprint and an atomic dispatch claim.
- Persist monotonic turn-admitted, dispatch-started/returned, and transcript-completed evidence.
- Set operation ID as generic turn ID.
- Implement same-ID/same-request replay and same-ID/different-request conflict.
- Implement durable cancellation request plus in-process cancellation registry.
- Persist immutable invocation records for success and known failure usage.
- Persist nullable requested and effective thinking effort in invocation audit while preserving the
  distinction between provider default and explicit `None`.
- Implement reconciliation of transcript evidence to operation state.
- Expose exact active-turn abandonment in application service.
- Test crash windows without heuristic data deletion.

## Expected change surface

- operation service and state machine
- cancellation registry
- invocation audit adapter
- reconciliation service
- focused tests

## Targeted validation

- LlmChatOperationIdempotencyTests
- LlmChatOperationCancellationTests
- LlmChatOperationRecoveryTests
- LlmChatOperationDispatchClaimTests
- LlmChatInvocationAuditTests

All test commands must comply with `test-budget.json`. Record exact commands and results in the proof
manifest.

## Acceptance criteria

- [x] Duplicate retry does not invoke provider twice.
- [x] Conflicting reuse fails before provider dispatch.
- [x] Cancellation is persisted and reaches current process.
- [x] Failed usage is retained outside transcript.
- [x] Invocation audit preserves requested/effective thinking effort without conflating provider default and explicit `None`.
- [x] Crash after assistant commit reconciles to success.
- [x] A retry never redispatches when durable evidence says provider dispatch may have started.
- [x] Crash before dispatch and crash after dispatch are distinguished without guessing.
- [x] Active pending turn becomes RecoveryRequired and needs exact turn ID.

## Forbidden work

- background heuristic abandonment
- raw prompt in logs or fingerprint errors
- best-effort in-memory-only idempotency

## Handoff

Complete `SESSION-HANDOFF.md` and `proof/proof-manifest.json`. Do not unlock the next subbundle until
all acceptance criteria and any checkpoint are satisfied.

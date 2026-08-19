# SB08 — HTTP turn, operation, cancel, and recovery API

Proof tier: **Governed**

## Objective

Expose retry-safe turn execution and durable operation management.

## Scope

- Implement send-turn endpoint with expected revision and mandatory operation/idempotency identity.
- Implement operation status endpoint.
- Implement cancellation endpoint.
- Implement exact active-turn abandonment/recovery endpoint gated by durable RecoveryRequired state and absence of a live execution owner.
- Map completed inline to 200 and admitted/running to 202 using one operation response schema.
- Expose stable retryability/error codes and operation location.
- Reject unsupported context/attachment/channel/model-override inputs explicitly rather than ignoring unmapped JSON members; keep strictness local to these DTOs.

## Expected change surface

- turn and operation route/mapping files
- ProblemDetails mapping additions
- focused HTTP tests

## Targeted validation

- LlmChatsTurnApiIntegrationTests
- LlmChatsIdempotencyApiIntegrationTests
- LlmChatsCancellationApiIntegrationTests
- LlmChatsRecoveryApiIntegrationTests

All test commands must comply with `test-budget.json`. Record exact commands and results in the proof
manifest.

## Acceptance criteria

- [x] Same operation retry returns existing result.
- [x] Conflicting retry is 409 without provider dispatch.
- [x] Stale transcript revision is 409.
- [x] Cancellation and recovery are durable.
- [x] Recovery cannot abandon a turn still owned by a live execution lease.
- [x] Provider failure is sanitized and includes operation ID.
- [x] Unsupported or unknown turn fields are rejected rather than silently ignored.
- [x] No agent execution run is created.

## Forbidden work

- streaming/SSE
- background worker
- tool approvals
- Project Structure context endpoint

## Handoff

Complete `SESSION-HANDOFF.md` and `proof/proof-manifest.json`. Do not unlock the next subbundle until
all acceptance criteria and any checkpoint are satisfied.

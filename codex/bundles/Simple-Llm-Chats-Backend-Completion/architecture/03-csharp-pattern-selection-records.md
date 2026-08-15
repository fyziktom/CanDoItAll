# C# Pattern Selection Records

## PSR-01 — Endpoint Ownership Extraction

- Problem: one Web type owns definition and conversation route families plus handlers.
- Selected: a thin stable `MapLlmChatsApi` coordinator with separate internal definition/conversation endpoint owner types in the same project.
- Rejected: partial classes, a new project, per-handler interfaces, or a generic endpoint framework.
- Reason: local responsibility separation improves navigation/test ownership without changing dependencies or public routes.

## PSR-02 — Transactional Definition Pin And CAS

- Problem: application pre-checks can become stale before commit.
- Selected: explicit repository/unit-of-work operations that lock or conditionally update the authoritative row and return typed outcomes; translate `DbUpdateConcurrencyException` at the persistence boundary.
- Rejected: retrying silently, catching everything in Web, or accepting last-write-wins.
- Reason: the database is canonical and the API promises stable optimistic concurrency.

## PSR-03 — Provider Task Supervision

- Problem: a control-loop failure can leave a provider task alive.
- Selected: structured task ownership in the executor with linked cancellation and a `finally` path that cancels when necessary and always awaits/observes the provider task before releasing the operation registration/scope.
- Rejected: fire-and-forget, disposal-only cancellation, or background continuation logging.
- Reason: paid external work and operation ownership require deterministic lifetime.

## PSR-04 — Evidence-Based Recovery

- Problem: post-dispatch crashes are not always safe to replay.
- Selected: explicit manage-scoped reconcile command using the existing reducer and durable transcript/invocation/dispatch evidence. Proven outcomes settle; ambiguous outcomes remain recovery-required; no redispatch.
- Rejected: automatic retry after any expired lease or operator-only database edits.

## PSR-05 — Durable Event High-Water

- Problem: retained-row `MAX(sequence)` regresses after deletion.
- Selected: high-water on the operation aggregate/row, incremented atomically while appending and backfilled by migration.
- Rejected: never deleting the last event, synthetic sequence reconstruction, or treating reset-to-zero as acceptable.
- Reason: the documented client cursor is monotonic independent of retention.

## PSR-06 — Coherent Replay Read

- Problem: multiple read-committed queries can observe different commits.
- Selected: one short bounded read-only repeatable-read snapshot, or a demonstrably equivalent single database statement, for operation/events/range/count/high-water.
- Rejected: compensating comparisons after unrelated queries or eventual consistency in a terminal envelope.

## PSR-07 — Event-Rooted Retention

- Problem: operation-root batches starve and can delete unbounded event rows.
- Selected: order/select eligible event-row keys and delete at most the configured row batch; terminal operation eligibility remains joined/guarded.
- Rejected: `take` operation IDs, full-table cleanup, or deleting canonical transcript/audit.

## PSR-08 — Bounded Database-Backed Workers

- Problem: one serial loop starves unrelated conversations and lacks age/duration policy.
- Selected: a configured number of hosted workers over existing durable claim semantics, with typed queue-age and operation-duration policies.
- Rejected: an in-memory channel shadow queue, unbounded `Task.Run`, or bypassing database leases.
- Default: preserve concurrency `1` unless configuration opts into more; all values validate on startup.

## PSR-09 — Safe Provider Logging

- Problem: driver exceptions may contain raw provider bodies.
- Selected: allowlisted structured fields plus typed failure classification; do not attach/log the exception object or its messages on this boundary.
- Rejected: regex redaction of arbitrary exception text, truncation-only, or suppressing the entire event.

## PSR-10 — Public Sensitive Projections

- Problem: read DTOs leak system prompt/internal fingerprint while operation audit is missing.
- Selected: query-level exclusion of system transcript entries, a separate manage-scoped editor DTO, removal of request fingerprint, and an allowlisted bounded invocation DTO.
- Rejected: returning one “full” DTO to both policies or serializing internal domain records.

## PSR-11 — Partial Class And Large-Type Policy

- No affected product owner may be split with `partial`.
- `LlmChatConversationEngine` is retained as one type because the audit found size, not a proven second responsibility.
- If implementation discovers a distinct responsibility, stop and record owner, dependency, consumer, and direct testability before extracting it; do not hide growth behind helpers or partial files.

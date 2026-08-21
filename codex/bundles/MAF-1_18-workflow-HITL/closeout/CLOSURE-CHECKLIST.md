# Closure Checklist

**State:** Closed — Proven

## Wave A

- [x] Stable packages resolve to 1.18.0.
- [x] A2A preview packages resolve to 1.18.0-preview.260818.1.
- [x] No active 1.17 MAF package remains.
- [x] Actual breaking changes are resolved.
- [x] Tool invocation is explicitly serial.
- [x] Order/overlap test is meaningful and passing.
- [x] Approval/session regressions pass.
- [x] Upgrade diff is independently reviewable.

## Wave B runtime

- [x] HumanInput uses native MAF request protocol.
- [x] Approval-required executor uses native request protocol.
- [x] Real MAF checkpoint JSON is persisted.
- [x] Checkpoint index ordering is correct.
- [x] Disposed-run rehydration succeeds.
- [x] Exact workflow version and topology are verified.
- [x] Consecutive requests work.
- [x] Approval denial is governed and typed.
- [x] Cancellation and failure races are covered.
- [x] Missing/corrupt/legacy checkpoints fail closed.
- [x] In-process backend remains non-durable.
- [x] Resume capability is advertised accurately.

SB03 supplies the native request/checkpoint foundation. SB04 supplies authoritative
PostgreSQL persistence and proves cancellation/failure races plus missing, corrupt, and
legacy checkpoint handling. In-memory stores remain proof-only, process-local,
non-durable, and non-snapshot-isolated; they do not establish host-restart or multi-host
correctness.

## Wave B persistence/API

- [x] Response operation is atomically claimed.
- [x] Idempotency replay and conflict semantics pass at the persistent operation boundary.
- [x] Crash recovery passes.
- [x] Participating governed executor deduplication passes.
- [x] Existing API routes are reused.
- [x] Typed JSON body is documented.
- [x] Actor comes from trusted identity.
- [x] Authorization matrix passes.
- [x] Self-approval is rejected.
- [x] Audit/redaction proof passes.
- [x] Operation/request status is observable.
- [x] Migration is safe for legacy rows.

Checked persistence items are SB04 scope. SB05 proves trusted service/API authorization,
validation, durable-grant reconstruction, idempotency binding, safe projections, and
status. The precise guarantee is exactly-once response acceptance and deduplicated
participating governed effects; no arbitrary external exactly-once guarantee is claimed.

## Proof and documentation

- [x] Every focused test through SB06 records discovered count.
- [x] No selected test run discovered zero tests.
- [x] Governed proof manifests and append-only reproof supplements are present without rewriting frozen parent evidence.
- [x] FG-01 ran once against the valid frozen state.
- [x] API/control-plane docs are updated.
- [x] Package/version docs are updated.
- [x] Every RQ row is Proven or honestly Blocked.
- [x] Original user request is closed note by note.
- [x] No required work is hidden as residual-risk prose.

## Final state

Closed. SB00–SB06 are Proven and CP-WB1–CP-WB4 are Pass. The final valid frozen FG-01
checkpoint at `af425ac371b251447f9858b15476092531c686da` passed both restores, both
Release builds at 0W/0E, and the exact Stable selector at 8,471/8,471 with zero failed or
skipped tests. The 17-row restartable E2E matrix, final documentation validation,
traceability audit, and note-by-note input closure pass. There are no remaining blockers.

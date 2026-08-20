# Closure Checklist

**State:** Open

## Wave A

- [ ] Stable packages resolve to 1.18.0.
- [ ] A2A preview packages resolve to 1.18.0-preview.260818.1.
- [ ] No active 1.17 MAF package remains.
- [ ] Actual breaking changes are resolved.
- [ ] Tool invocation is explicitly serial.
- [ ] Order/overlap test is meaningful and passing.
- [ ] Approval/session regressions pass.
- [ ] Upgrade diff is independently reviewable.

## Wave B runtime

- [ ] HumanInput uses native MAF request protocol.
- [ ] Approval-required executor uses native request protocol.
- [ ] Real MAF checkpoint JSON is persisted.
- [ ] Checkpoint index ordering is correct.
- [ ] Disposed-run rehydration succeeds.
- [ ] Exact workflow version and topology are verified.
- [ ] Consecutive requests work.
- [ ] Approval denial is governed and typed.
- [ ] Cancellation and failure races are covered.
- [ ] Missing/corrupt/legacy checkpoints fail closed.
- [ ] In-process backend remains non-durable.
- [ ] Resume capability is advertised accurately.

## Wave B persistence/API

- [ ] Response operation is atomically claimed.
- [ ] Idempotency replay and conflict semantics pass.
- [ ] Crash recovery passes.
- [ ] Governed executor deduplication passes.
- [ ] Existing API routes are reused.
- [ ] Typed JSON body is documented.
- [ ] Actor comes from trusted identity.
- [ ] Authorization matrix passes.
- [ ] Self-approval is rejected.
- [ ] Audit/redaction proof passes.
- [ ] Operation/request status is observable.
- [ ] Migration is safe for legacy rows.

## Proof and documentation

- [ ] Every focused test records discovered count.
- [ ] No selected test run discovered zero tests.
- [ ] Governed proof manifests are complete.
- [ ] FG-01 ran once after freeze.
- [ ] API/control-plane docs are updated.
- [ ] Package/version docs are updated.
- [ ] Every RQ row is Proven or honestly Blocked.
- [ ] Original user request is closed note by note.
- [ ] No required work is hidden as residual-risk prose.

## Final state

Not closed.

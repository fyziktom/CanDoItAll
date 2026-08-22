# Closure Checklist

**State:** Incomplete — Wave C/SB07 is implemented and technically validated;
`GOVERNED_PROOF_INCOMPLETE`

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
- [x] Every RQ row records its evidence-backed state; SB07 rows remain Implemented.
- [x] Original user request is closed note by note.
- [x] No required work is hidden as residual-risk prose.

The checked proof/documentation statements above describe the historical SB00–SB06 closure.

## Wave C standalone sample and browser E2E

- [x] Direct source inspection confirms .NET 10 and zero product project references.
- [x] Direct source inspection confirms exactly twenty SimWiki JSON articles.
- [x] Governed SB07 proof, transcript, browser, and raw-result directories are scaffolded.
- [x] Freeze the final Wave C executable/test source set after recovery repairs.
- [x] Capture the final sample Release build, exact 61-case list, and exact 61-case test transcript.
- [x] Capture affected product build transcripts.
- [x] Add and pass PostgreSQL replay-versus-mutation and native-link uniqueness race tests.
- [x] Add migration `20260822013043_AddWorkflowNativeCheckpointRequestUniqueness` and pass
  migration application/no-pending-model validation.
- [x] Rerun the expanded 71-case Unit and 64-case Integration focused transcripts after race
  repairs.
- [x] Complete terminal live workflows through canonical SSE without polling fallback.
- [x] Complete and inspect direct-hit, later-hit, and three-miss Playwright journeys.
- [x] Complete all three terminal Playwright journeys against the final frozen source digest.
- [x] Capture sanitized desktop screenshots for all three browser journeys.
- [x] Complete credential, protected-state, anti-stub, and polling-fallback scans.
- [x] Preserve `BG-SB07-01` as invalidated by later persistence/schema repairs.
- [x] Complete and record replacement `BG-SB07-02`, including the once-only full
  Integration-project result.
- [x] Freeze and independently verify the ownership and evidence hash ledgers.
- [x] Rerun the post-fix technical verifier.
- [ ] Supply an authentic failing-first test transcript for the behavior-changing work. None was
  captured before repair; the pre-fix review is not relabeled as RED evidence.
- [x] Mark RQ-046 through RQ-054 Implemented after technical validation.

## Final state

Wave A/Wave B are historically closed. SB00–SB06 are Proven and CP-WB1–CP-WB4 are Pass.
The final valid frozen FG-01
checkpoint at `af425ac371b251447f9858b15476092531c686da` passed both restores, both
Release builds at 0W/0E, and the exact Stable selector at 8,471/8,471 with zero failed or
skipped tests. The 17-row restartable E2E matrix, final documentation validation,
traceability audit, and historical note-by-note input closure pass.

Wave C implementation and focused/browser proof pass. Response replay now serializes through a
request-row `FOR UPDATE` lock, and a filtered PostgreSQL unique index prevents two same-session
checkpoint links from claiming the same native request/port tuple. Sample 61/61, Unit 71/71,
focused Integration 64/64, Playwright 3/3 against the frozen source set, and five Release
builds pass. The full Integration project passed 982/983 with zero failures in 1h24m; the sole
skip is the declared opt-in live Ollama catalog test requiring additional installed model families.

`BG-SB07-01` remains invalidated historical evidence and `BG-SB07-02` remains Pass.
Post-fix technical verification, final ownership/evidence hashes, and both validators pass, but no
authentic failing-first test transcript exists. SB07 and the current parent are therefore
Implemented with `GOVERNED_PROOF_INCOMPLETE`, not Proven. Historical FG-01 remains Pass at its
original HEAD, counts, timestamps, and sibling pins.

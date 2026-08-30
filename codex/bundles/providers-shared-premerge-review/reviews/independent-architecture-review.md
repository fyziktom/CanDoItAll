# Independent Architecture and Readiness Review

Reviewed the prepared root, requirements, phase/validation/handoff plans, architecture records and SB01-SB06 against the inspected source. Execution-report/traceability scaffold completion was intentionally left to the bundle owner. No production change, application run, build, test, or benchmark was performed. Existing source/test paths in the reviewed units were checked and exist.

Result: **ready after the bounded amendments below are reconciled**. The chosen repair scope and project boundaries are appropriate; no larger refactor or new project is justified by this review.

## Required amendments

1. **Make SB05 prerequisites consistent.** `plan/architecture-checkpoints.md` requires CP-HISTORY (SB03 + SB04) to unlock SB05, but the root, phase graph and SB05 list only SB01/SB02/SB04. Either add SB03 consistently or explicitly narrow the checkpoint's SB05 unlock. An executor must not be able to satisfy one entry list while violating another.
2. **Freeze both public streaming operation paths.** SB01's five named failure scenarios do not establish Chat Completions and Responses coverage. Require Responses failed/incomplete (2), plus timeout/malformed/disconnect through both Chat and Responses (6), at least eight pinned-SDK cases. The actual response must fail at the downstream consumer, not only in the private Completion task. Retain first-chunk/disposal/caller-cancellation tests.
3. **Protect the null-error repair with terminal-status negatives.** SB01 currently requires only a non-null error buffered negative. Add explicit HTTP-200 failed/incomplete Responses envelopes with null error, and malformed/missing completion-state cases according to the chosen supported subset, so a one-line null check cannot silently record non-completed operations as success. Keep buffered and streaming outcome policy consistent.
4. **Require an unknown-cancellation negative.** SB03 describes separate independent cancellation handling but omits it from required named cases. Add OCE with active caller token and no explicit timeout cause/deadline; it must not become TimedOut solely because the caller token is active. Include a known HttpClient timeout shape and a known wrapped SDK timeout. Existing terminal evidence must remain authoritative.
5. **Correct the capacity baseline wording.** `plan/02-validation-strategy.md` says the default maintenance batch is 100. The actual default policy BatchSize is 500; source maintenance separately caps work at 100. Keep outbox and source cleanup ceilings distinct (25/s and 5/s respectively before work/time-budget effects). These are static scheduler ceilings, not measured requirements or performance failures.

## Positive readiness conclusions

- SB01 ownership stays in existing HTTP response/SSE policies and the Web writer. SDK client tests counter the existing shallow internal-completion-only assertions.
- SB02 preserves canonical source policy, DNS/address enforcement and default loopback authority without silently promoting stored flags.
- SB03 correctly limits redaction to known secrets/documented patterns, keeps plaintext test data synthetic, and requires persisted/decrypted proof. It does not promise universal DLP or reinterpret all independent cancellation as timeout.
- SB04 accurately addresses unreferenced zero-payload input tombstones, preserves retained retry references and requires bounded PostgreSQL concurrency/quota proof. It does not erase canonical history.
- SB05 requires before/after data and preserves cross-instance revocation plus final persisted-target validation. Cached static sets and owned memory are suitable small repairs. Deferred eligibility work must remain bound to persisted freshness; do not optimize by replacing it with process-only invalidation.
- SB06 keeps schema mapping in Web and neutral protocol types framework-independent. Exactly five shared-provider operations are implemented; no source-administration/history HTTP endpoint is invented.
- The architecture records avoid speculative extraction, broad sealing/LINQ edits, new runtime partials, and fake boundary splits. Small pure helpers can be directly tested in current owners.
- The handoff preserves original three-application proof and Docker authority limits. New repairs do not reset historical budgets or convert old results to current passes. The manual merge remains the user's action.
- Recovery overwrite and opaque SDK retry concerns are explicitly rejected as blocking findings for the correct reasons: optimistic concurrency and the amended application-visible-attempt contract.

## Remaining execution obligations

These plans are not passing proof. Freeze actual discovery counts and source/package identities at entry; run failing-first regressions; revalidate affected consumers after fixes; bind exports to the final running-host identity; and reconcile the historical hosted gate under existing authorization. The pinned package found in the current MAF assets is OpenAI 2.12.0. No universal performance speedup or all-branch correctness certification is established here.

## Final resolution

Rechecked the five requested amendments against the corrected files. **Independent semantic/architecture preparation review: PASS.** CP-CAPTURE and CP-RETENTION now separate the SB03 and SB04 unlocks consistently with SB05's SB01/SB02/SB04 prerequisites. SB01 requires eight cross-protocol streaming failure cases and four buffered unsuccessful/invalid-status cases. SB03 now explicitly requires unknown-cancellation, known SDK timeout-wrapper and fast HttpClient deadline coverage. Capacity wording correctly distinguishes policy batch 500 from source cap 100.

All required amendments above are resolved. This pass establishes implementation-plan readiness only; product execution, regression proof, exports, hosted requirements and merge readiness remain pending their declared gates.

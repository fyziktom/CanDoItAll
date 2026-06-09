# SB051 Semantic Invariants

## Invariant SB051-FAKE-PROOF-TRAPS-ARE-REJECTED
- Invariant ID: `SB051-FAKE-PROOF-TRAPS-ARE-REJECTED`
- Source raw note: `Stable Process Core with domain drivers`
- Expected behavior: Final red-team review rejects report-only closure, happy-path-only proof, status-only rows, runtime-host drift, mutation side effects, prose-only samples, and unbacked API claims.
- Disallowed shallow implementation: Marking rows passed without transcript-backed proof, relying on report text only, or accepting docs that are not tied to source/tests.
- Failing-first test: No production failure was introduced; red-team scans fail if trap rejection rows are missing, subbundle rows collapse, critical manifests are missing, runtime hooks drift in, or UI/media drift appears.
- Passing test: bundle://proof/SB051/transcripts/p17-red-team-trap-scans.txt and bundle://proof/SB051/transcripts/full-unit-p17.txt
- Changed source files: none in P17; review/proof/status files only.
- Production assertions: Runtime-host and side-effect tokens remain absent from the scoped read-only pipeline, and source-backed tests still guard gateway/docs/process orchestration claims.
- Red-team negative case: Completed-stage preflight rejects pending final handoff work rather than allowing fake final closure.
- Downstream dependency check: SB052-SB054 can proceed with explicit proof that final validation is not being faked before handoff.

## Invariant SB051-COMPLETED-VALIDATOR-IS-DEFERRED-UNTIL-FINAL-HANDOFF
- Invariant ID: `SB051-COMPLETED-VALIDATOR-IS-DEFERRED-UNTIL-FINAL-HANDOFF`
- Source raw note: `Prepare bundle zip`
- Expected behavior: Prepared validation passes during P17, but completed-stage validation is allowed to pass only after SB052-SB054 close and zip handoff proof exists.
- Disallowed shallow implementation: Treating the completed validator preflight failure as final success or suppressing the failure by marking future subbundles complete without roadmap/zip proof.
- Failing-first test: bundle://proof/SB051/transcripts/completed-validator-preflight-expected-pending.txt records the expected completed-stage rejection before SB052-SB054.
- Passing test: Prepared validator proof and final completed validator are owned by SB054 after final handoff closure.
- Changed source files: none in P17; review/proof/status files only.
- Production assertions: No production behavior changed; validator sequencing is metadata/proof governance only.
- Red-team negative case: The preflight transcript proves the validator catches pending subbundle rows and raw note closure debt.
- Downstream dependency check: SB054 must rerun completed validation after roadmap handoff and zip generation.

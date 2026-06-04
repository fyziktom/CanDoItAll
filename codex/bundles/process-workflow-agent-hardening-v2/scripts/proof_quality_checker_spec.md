# Proof Quality Checker Specification

This is the production proof-quality checker that Codex should implement or wire into the existing completed-stage validator.

## Critical Process E2E Failure Conditions

Fail critical process E2E closure when any of these are true:

1. The proof script or proof artifacts contain `suppressAutomationDispatch = true` for claimed production path proof.
2. `agent-execution-runs.json` is missing, empty, or explicitly says no CanDoItAll provider execution runs were created.
3. `usage-summary.json` says provider usage is unavailable while the subbundle claims real provider process automation proof.
4. The proof harness writes scenario application source files directly while claiming process/agent app-generation proof.
5. Process step completion is done only by manual API transition without an execution run, unless the proof is explicitly classified as migration/backfill/manual fixture.
6. Required current-run lineage fields are absent: process run id, step run id, execution run id, artifact id/path, and provider response id when provider usage exists.
7. Critical proof contains no failing-first transcript.
8. Critical proof contains no adversarial negative test.
9. Critical proof contains only counts/status rows/non-empty files and no behavior-level verification.

## Expected Regression

The checker must fail the previous V1 SB08 proof because it manually transitions steps, suppresses automation dispatch, generates app code in the harness, and records no provider execution runs.

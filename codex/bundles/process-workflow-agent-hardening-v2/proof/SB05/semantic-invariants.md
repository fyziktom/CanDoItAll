# SB05 Semantic Invariants

## Invariants

- `SB05-INV-001`: Production process E2E proof must use schema `candoitall.sb04.realProcessE2E.v1` and exactly five scenario entries.
- `SB05-INV-002`: Critical E2E proof fails if the script or proof contains manual transitions, suppressed automation dispatch, or harness app source generation.
- `SB05-INV-003`: Every scenario fails unless it has current-run process detail, non-empty process-step execution runs requested by automation dispatch, tool receipts bound to execution ids, provider usage observations, generated root/layout, build transcript, and desktop/mobile browser summary.
- `SB05-INV-004`: The generated app root must be bound to the process run id and end in `GeneratedBlazorApp`.
- `SB05-INV-005`: Completed-stage bundle validation must run the SB04 process E2E proof-quality checker.

## Evidence

- `bundle://proof/SB05/transcripts/expected-failure-v1-sb08-proof.txt`
- `bundle://proof/SB05/transcripts/passing-new-sb04-proof.txt`
- `repo://codex/bundles/process-workflow-agent-hardening-v2/scripts/validate_bundle.py`
- `bundle://proof/SB04/manifest.json`

## Residual Risk

The checker intentionally validates the proof shape and artifact linkage. It does not re-run the full process E2E itself during completed-stage validation because those runs are expensive and provider-dependent.

# SB09 Fake-Proof Resistance Report

## Decision

Pass.

## Old Proof Attack

Input:

- `repo://codex/bundles/process-workflow-agent-hardening-v1/proof/SB08`
- `repo://codex/bundles/process-workflow-agent-hardening-v1/scripts/run_sb08_multidomain_e2e.ps1`

Result:

- Rejected by `validate_bundle.py --check-process-e2e-proof`.
- Transcript: `bundle://proof/SB09/transcripts/proof-quality-old-v1-expected-failure.txt`.

Rejected signals:

- Wrong schema.
- Missing scenario count.
- Manual transition/suppressed automation bypass.
- Harness-owned generated app paths.
- Missing tool receipts.
- Missing generated-source-root/layout proof.
- Missing browser summary/build proof in required shape.
- Empty provider execution runs.
- Provider usage not observed.
- Harness app scaffold transcripts.

## New Proof Acceptance

Input:

- `bundle://proof/SB04`
- `repo://codex/bundles/process-workflow-agent-hardening-v2/scripts/run_sb04_real_process_e2e.ps1`

Result:

- Accepted by the same checker.
- Transcript: `bundle://proof/SB09/transcripts/proof-quality-new-sb04-pass.txt`.

## Residual Risk

The checker validates proof shape and linkage, not semantic quality of generated app code. Browser and build proof cover generated app runtime behavior; future quality expansion can add per-domain semantic browser assertions.

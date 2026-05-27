# SB10: 10-process-artifact-validation-live-run-regression

## Goal

Prove the failed live-run artifact binding failure is fixed by production code.

## Required work

- Reconstruct the failed run artifact case from captured evidence.
- Assert current-run org-scoped artifact path is accepted when run/step/expectation/execution/content are valid.
- Assert stale run, wrong step, wrong expectation, wrong execution run, and unreadable content are rejected with distinct statuses.
- Do not rely only on mock harness; include integration-path tests.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Explicit classification: package-only / adapter-level / process-level / UI-level.
- If MAF related: state whether this actually adopts a MAF 1.6 feature or only preserves compatibility.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB10` are updated and downstream subbundles can rely on the behavior.

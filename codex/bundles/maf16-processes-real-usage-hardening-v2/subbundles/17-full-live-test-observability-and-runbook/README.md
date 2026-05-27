# SB17: 17-full-live-test-observability-and-runbook

## Goal

Prepare full live process observability and user runbook.

## Required work

- Add a runbook for the next real test: import/select live profile, start run, observe step 0, continue to implementation/QA/writeback.
- Ensure UI/API exposes current step, required artifacts, validation details, pending approvals, tool receipts, diagnostics, and next recovery action.
- Add Playwright/component smoke test for run detail observability.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Explicit classification: package-only / adapter-level / process-level / UI-level.
- If MAF related: state whether this actually adopts a MAF 1.6 feature or only preserves compatibility.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB17` are updated and downstream subbundles can rely on the behavior.

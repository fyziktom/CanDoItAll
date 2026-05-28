# SB13: 13-observability-dashboard-and-operator-debugging

## Goal

Improve operator debugging of process runs.

## Required work

- Ensure UI/API shows: run health, artifact status matrix, validation diagnostics, output roots, manager resolution reason, tool receipts, pending approvals, recovery recommendations.
- Add deep links from process run to project-structure run folder and artifacts.
- Add compact post-run summary artifact generated from run evidence.
- Add component/browser smoke tests for completed, blocked, failed, and recovered run states.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: runtime / API / UI / docs / template / test-harness / MAF.
- Note whether this closes previous proof debt.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB13` are updated and the next dependent workstream can rely on it.

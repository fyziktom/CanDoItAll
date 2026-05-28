# SB15: 15-test-taxonomy-timeout-and-proof-harness-refactor

## Goal

Refactor proof/test harness to avoid broad timeouts.

## Required work

- Replace one huge integration filter with named categories and scripts.
- Mark slow/live/browser tests separately from deterministic unit/integration tests.
- Add quarantine/explanation for known long-running tests.
- Create a proof collector script that stores transcripts in bundle proof folders.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: runtime / API / UI / docs / template / test-harness / MAF.
- Note whether this closes previous proof debt.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB15` are updated and the next dependent workstream can rely on it.

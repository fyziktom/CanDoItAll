# SB10: 10-artifact-contracts-lineage-and-current-run-proof

## Goal

Make required evidence robust enough for the live test.

## Work items

- Ensure Tetris/Blazor required artifacts include implementation change set, implementation self-review, runtime evidence pack, validation self-review, run evidence index, project-structure writeback summary.
- Require current-run projection lineage for automation artifacts.
- Ensure screenshots and console proof are managed artifacts, not chat-only text.
- Add tests for stale artifact rejection and missing screenshot/console blocker.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- A note explaining how this improves readiness for the real UI-driven Blazor WASM PWA Tetris test.
- A note explaining how generic process behavior remains protected.

## Closure criteria

This subbundle is complete only when its proof manifest is updated and the next subbundle can rely on the result.

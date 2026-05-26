# SB06: 06-refactor-checkpoint-a-contract-normalization

## Goal

Refactor operation contract normalization into one authoritative service.

## Required work

- Expand `ProcessStepOperationContractState` beyond sorting/deduping.
- Centralize target-scope implied operations, invalid combinations, default operation sets by step kind, and strict validation.
- Use the same normalizer in editor save, import/export, template projection, lint, dispatch metadata, and tests.
- Remove or clearly mark legacy text inference as fallback only.
- Run focused tests before continuing.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path test.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on whether this affects the planned Blazor WASM PWA/Tetris UI test.

## Closure criteria

This subbundle is not complete until the proof files under `proof/SB06` are updated and the next dependent subbundle can rely on the behavior without reinterpreting prose.

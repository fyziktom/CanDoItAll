# SB03: 03-template-inventory-and-governance-matrix

## Goal

Build an explicit matrix for every template in `Templates/Processes/manifest.json`.

## Required work

- List every template key from the manifest.
- For every step, record whether it has `AllowedOperations`, `OperationTargetScope`, branch outcomes, required artifacts, artifact inputs, and exception policy.
- Mark templates as ready/not-ready for strict typed governance.
- Fail the subbundle if any template still relies on prose-only operation boundaries without a planned migration.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path test.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on whether this affects the planned Blazor WASM PWA/Tetris UI test.

## Closure criteria

This subbundle is not complete until the proof files under `proof/SB03` are updated and the next dependent subbundle can rely on the behavior without reinterpreting prose.

# SB11: 11-refactor-checkpoint-b-runtime-validation-services

## Goal

Refactor validation, lineage, and transition services for maintainability.

## Required work

- Split oversized partial classes where needed.
- Create service boundaries for operation contract resolution, artifact validation, projection lineage, block/recovery routing, and template validation.
- Keep tests proving behavior unchanged after refactor.
- Do not move functionality into UI or template-only code.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path test.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on whether this affects the planned Blazor WASM PWA/Tetris UI test.

## Closure criteria

This subbundle is not complete until the proof files under `proof/SB11` are updated and the next dependent subbundle can rely on the behavior without reinterpreting prose.

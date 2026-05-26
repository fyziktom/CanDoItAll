# SB10: 10-unified-artifact-validation-for-api-transitions

## Goal

Prevent manual/API transition from being weaker than automation finalization.

## Required work

- Extract finalizer-grade artifact validation into a shared service.
- Use it from automation finalizer and `TransitionStepAsync` when completing a step through API/manual routes.
- Ensure transition validation checks content, lineage, producer kind, current-run binding, placeholder/gap markers, and managed evidence.
- Keep exception/disposition branch behavior explicit and typed.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path test.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on whether this affects the planned Blazor WASM PWA/Tetris UI test.

## Closure criteria

This subbundle is not complete until the proof files under `proof/SB10` are updated and the next dependent subbundle can rely on the behavior without reinterpreting prose.

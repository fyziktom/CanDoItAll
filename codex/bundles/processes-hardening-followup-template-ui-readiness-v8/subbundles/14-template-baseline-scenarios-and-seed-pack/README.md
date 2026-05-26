# SB14: 14-template-baseline-scenarios-and-seed-pack

## Goal

Extend baseline scenarios to cover typed templates.

## Required work

- Add or update baseline scenarios for Blazor WASM PWA/Tetris, customer onboarding, business plan, incident response, release readiness, and architecture decision governance.
- Each baseline should exercise artifact creation, branch selection, block/recovery state, and typed operation contracts.
- Ensure scenario data is generic and reusable.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path test.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on whether this affects the planned Blazor WASM PWA/Tetris UI test.

## Closure criteria

This subbundle is not complete until the proof files under `proof/SB14` are updated and the next dependent subbundle can rely on the behavior without reinterpreting prose.

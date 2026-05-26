# SB06: 06-refactor-checkpoint-a-template-skill-contracts

## Goal

Refactor after template/skill work before runtime UI preflight.

## Work items

- Extract repeated Blazor template contract phrases into shared template resources where appropriate.
- Refactor template validation helpers if repeated checks were added.
- Ensure docs and skills reference common terminology for operations and target scopes.
- Run focused template and skill source audits before continuing.

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

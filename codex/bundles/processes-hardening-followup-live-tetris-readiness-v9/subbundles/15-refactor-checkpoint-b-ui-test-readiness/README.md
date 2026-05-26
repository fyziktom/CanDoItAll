# SB15: 15-refactor-checkpoint-b-ui-test-readiness

## Goal

Final refactor checkpoint before live-test closure.

## Work items

- Clean up helper duplication in template profile selection, live scenario metadata, and skill/tool readiness checks.
- Ensure UI/API/test code names are generic: live-run profile, scenario acceptance, operation contract, not Tetris-specific runtime logic.
- Run focused tests before final red-team.

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

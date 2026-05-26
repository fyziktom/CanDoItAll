# SB11: 11-project-structure-writeback-proof

## Goal

Make final writeback step verifiable.

## Work items

- Ensure final writeback requires `project_structure_node_create` and `project_structure_asset_create` receipts when applicable.
- Ensure project-structure tools require `ExecuteExternalAction` and are not available to read-only QA unless explicitly allowed.
- Add readback validation that created node/asset ids are recorded in final artifact.
- Add tests for writeback missing receipts causing Blocked/OwnOutput, not silent success.

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

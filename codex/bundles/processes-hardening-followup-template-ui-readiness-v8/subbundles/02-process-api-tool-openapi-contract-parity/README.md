# SB02: 02-process-api-tool-openapi-contract-parity

## Goal

Ensure public API/tool surfaces expose the new runtime governance model.

## Required work

- Inventory `ProcessesApi`, MAF process tools, request/response DTOs, read models, import/export envelopes, and Swagger/OpenAPI-visible schemas.
- Verify that API save/import/export round-trips `ContractMode`, `AllowedOperations`, `OperationTargetScope`, artifact workflow mapping fields, subprocess child mapping fields, `BlockCause`, `ProjectionLineage`, `ProjectionIdentityHash`, recovery options, and block reason codes.
- Add API integration tests that write through nested routes and read the values back from run/detail endpoints.
- Ensure MAF process tools use the same model behavior as HTTP API.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path test.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on whether this affects the planned Blazor WASM PWA/Tetris UI test.

## Closure criteria

This subbundle is not complete until the proof files under `proof/SB02` are updated and the next dependent subbundle can rely on the behavior without reinterpreting prose.

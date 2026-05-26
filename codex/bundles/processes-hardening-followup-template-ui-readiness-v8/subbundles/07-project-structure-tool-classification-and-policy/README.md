# SB07: 07-project-structure-tool-classification-and-policy

## Goal

Make project-structure tools first-class governed external-action tools.

## Required work

- Inventory all `project_structure_*` tools available to agents.
- Register/classify project-structure mutation tools in `AgentToolInvocationPolicyMetadata`.
- Require `ExecuteExternalAction` for project-structure mutations and treat read-only project-structure inspection separately.
- Ensure template writeback steps include the correct operations.
- Add red-team tests proving validation or architecture steps cannot call project-structure mutation tools unless their contract allows it.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path test.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on whether this affects the planned Blazor WASM PWA/Tetris UI test.

## Closure criteria

This subbundle is not complete until the proof files under `proof/SB07` are updated and the next dependent subbundle can rely on the behavior without reinterpreting prose.

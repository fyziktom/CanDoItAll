# Codex task prompt — ACR-013

Implement finding `ACR-013` from this subbundle.

## Required stance

- follow the bundle architecture
- do not solve this by introducing a new parallel truth
- keep changes aligned with `Phase 0`
- preserve node-as-carrier and canonical spatial semantics where relevant
- add required positive and negative tests
- run the validation commands
- produce evidence for QA

## Finding summary

ProjectPartyAssignment stores NodeKey as a plain string and SaveAssignmentAsync validates only project and party existence. There is no visible check that the referenced node exists, belongs to the same project, or allows the requested role.

## Ordered implementation steps

- Introduce explicit assignment scope semantics (`Project`, `Node`, and later aggregate scopes) instead of a free `NodeKey` string.
- For node scope, validate that the referenced canonical node exists, belongs to the same project, and allows the requested role.
- If feasible, store a real FK-like reference (`NodeId`/`WorkbenchNodeId`) for workbench-native nodes.
- Add negative tests for orphan scope keys, wrong-project scope keys, and illegal role/kind combinations.

## Guardrails

- Do not silently auto-create or auto-assume missing nodes from NodeKey strings.
- Do not accept a string FK without runtime validation once node-scoped assignments are first-class.

## Done means

- Node-scoped assignments fail fast when node keys are missing, cross-project, or role-incompatible.
- Tests prove project-scoped and node-scoped assignments are distinguished correctly.

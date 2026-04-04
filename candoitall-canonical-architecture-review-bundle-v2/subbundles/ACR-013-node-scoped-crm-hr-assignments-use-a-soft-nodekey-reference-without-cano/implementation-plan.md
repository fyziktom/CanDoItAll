# Implementation plan

## Remediation goal

Validate node-scoped assignments against the canonical graph or node carrier before persistence; reject orphan node keys, mismatched project scope, and illegal role-kind combinations. Consider a stronger target reference over time.

## Ordered steps

- Introduce explicit assignment scope semantics (`Project`, `Node`, and later aggregate scopes) instead of a free `NodeKey` string.
- For node scope, validate that the referenced canonical node exists, belongs to the same project, and allows the requested role.
- If feasible, store a real FK-like reference (`NodeId`/`WorkbenchNodeId`) for workbench-native nodes.
- Add negative tests for orphan scope keys, wrong-project scope keys, and illegal role/kind combinations.

## Guardrails

- Do not silently auto-create or auto-assume missing nodes from NodeKey strings.
- Do not accept a string FK without runtime validation once node-scoped assignments are first-class.

## Acceptance criteria

- Node-scoped assignments fail fast when node keys are missing, cross-project, or role-incompatible.
- Tests prove project-scoped and node-scoped assignments are distinguished correctly.

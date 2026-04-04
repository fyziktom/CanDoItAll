# Codex task prompt — ACR-004

Implement finding `ACR-004` from this subbundle.

## Required stance

- follow the bundle architecture
- do not solve this by introducing a new parallel truth
- keep changes aligned with `Phase 1`
- preserve node-as-carrier and canonical spatial semantics where relevant
- add required positive and negative tests
- run the validation commands
- produce evidence for QA

## Finding summary

ParentNodeKey and hierarchy link rows both describe parentage, while dependency analysis folds ancestry into prerequisites and inverse blocking semantics. As CRM/HR links and critical-path logic grow, blurred graph semantics become more dangerous.

## Ordered implementation steps

- Choose one canonical owner for containment (likely `ParentNodeKey` or a dedicated containment table) and remove duplicated hierarchy storage.
- Keep non-hierarchy edges in an explicit edge model with separate policies for dependency, association, and derivation.
- Refactor dependency analysis so ancestor chain is not silently treated as a generic dependency unless a dedicated structural-gate policy says so.
- Add an invariant service that validates parent, edge kind, and allowed source/target combinations through the registry.

## Guardrails

- Do not keep hierarchy duplicated in both a parent field and a writable generic relation table.
- Do not let dependency analysis infer semantics that should be explicit edges.

## Done means

- Hierarchy and non-hierarchy relations have separate canonical meanings.
- A node move updates one canonical hierarchy owner.
- Dependency analysis no longer needs to guess which edges are structural versus executional.

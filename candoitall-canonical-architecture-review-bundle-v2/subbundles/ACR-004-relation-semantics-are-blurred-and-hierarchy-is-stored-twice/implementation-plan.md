# Implementation plan

## Remediation goal

Choose one canonical hierarchy owner for workbench-authored nodes, model dependency/association edges explicitly, and define a relation policy matrix by node kind.

## Ordered steps

- Choose one canonical owner for containment (likely `ParentNodeKey` or a dedicated containment table) and remove duplicated hierarchy storage.
- Keep non-hierarchy edges in an explicit edge model with separate policies for dependency, association, and derivation.
- Refactor dependency analysis so ancestor chain is not silently treated as a generic dependency unless a dedicated structural-gate policy says so.
- Add an invariant service that validates parent, edge kind, and allowed source/target combinations through the registry.

## Guardrails

- Do not keep hierarchy duplicated in both a parent field and a writable generic relation table.
- Do not let dependency analysis infer semantics that should be explicit edges.

## Acceptance criteria

- Hierarchy and non-hierarchy relations have separate canonical meanings.
- A node move updates one canonical hierarchy owner.
- Dependency analysis no longer needs to guess which edges are structural versus executional.

# Implementation plan

## Remediation goal

Introduce a canonical NodeKindRegistry that defines allowed facets, transitions, relation policy, actor role policy, time semantics, execution semantics, and UI descriptors from one place.

## Ordered steps

- Create `NodeKindRegistry` / `NodeKindDefinition` keyed by a stable kind identifier rather than raw enum+string combinations.
- Move allowed transitions, relation policy, actor-role policy, time semantics, execution semantics, and UI descriptors into the registry.
- Refactor UI catalog code so create definitions are generated from the registry instead of owning semantic truth.
- Replace subtype string branching in services/pages with registry lookups and policy checks.

## Guardrails

- Do not let the registry become a dumping ground for UI-only strings or runtime flags.
- Do not preserve subtype string inference as the hidden real owner after introducing the registry.

## Acceptance criteria

- There is one canonical place to answer allowed relations, actor roles, and transitions for a node kind.
- UI creation definitions can be derived from registry semantics.
- Subtype-driven role inference is removed or routed through the registry.

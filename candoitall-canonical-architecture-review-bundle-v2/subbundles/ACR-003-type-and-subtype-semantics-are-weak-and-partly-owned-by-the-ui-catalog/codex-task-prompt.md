# Codex task prompt — ACR-003

Implement finding `ACR-003` from this subbundle.

## Required stance

- follow the bundle architecture
- do not solve this by introducing a new parallel truth
- keep changes aligned with `Phase 1`
- preserve node-as-carrier and canonical spatial semantics where relevant
- add required positive and negative tests
- run the validation commands
- produce evidence for QA

## Finding summary

ProjectObjectType is broad, subtypes are strings, metadata validation is shallow, and the canvas catalog carries substantial semantic truth about participants, work items, decisions, and other node kinds. CRM/HR role semantics are partly inferred from subtype strings rather than a canonical registry.

## Ordered implementation steps

- Create `NodeKindRegistry` / `NodeKindDefinition` keyed by a stable kind identifier rather than raw enum+string combinations.
- Move allowed transitions, relation policy, actor-role policy, time semantics, execution semantics, and UI descriptors into the registry.
- Refactor UI catalog code so create definitions are generated from the registry instead of owning semantic truth.
- Replace subtype string branching in services/pages with registry lookups and policy checks.

## Guardrails

- Do not let the registry become a dumping ground for UI-only strings or runtime flags.
- Do not preserve subtype string inference as the hidden real owner after introducing the registry.

## Done means

- There is one canonical place to answer allowed relations, actor roles, and transitions for a node kind.
- UI creation definitions can be derived from registry semantics.
- Subtype-driven role inference is removed or routed through the registry.

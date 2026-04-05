# Codex task prompt — ACR-002

Implement finding `ACR-002` from this subbundle.

## Required stance

- follow the bundle architecture
- do not solve this by introducing a new parallel truth
- keep changes aligned with `Phase 3`
- preserve node-as-carrier and canonical spatial semantics where relevant
- add required positive and negative tests
- run the validation commands
- produce evidence for QA

## Finding summary

One record mixes node carrier truth, spatial semantics, schedule, markers, route, artifact binding, storage/media references, and metadata-driven subtype payloads. With CRM/HR, the same box also becomes the anchor for party assignment metadata.

## Ordered implementation steps

- Extract a narrow `NodeCarrier` owner for stable node identity, basic text, and containment/backing metadata only.
- Move schedule, spatial semantics, markers, artifact/storage bindings, and typed details to companion records/facets with explicit owners.
- Update mappings so UI surfaces read an assembled DTO rather than the persistence record directly.
- Plan a migration path that preserves existing node identity and existing data while splitting concerns.

## Guardrails

- Do not demote semantically meaningful X/Y and semantic markers to disposable UI state.
- Do not keep multiple writable copies of the same schedule/signal/assignment fact.
- Prefer compatibility shims over a big-bang table rewrite.

## Done means

- NodeCarrier no longer mixes route/storage/media/signal/schedule/facet payloads into one universal record.
- Spatial semantics remain canonical where the product requires them.
- Typed facet data can evolve without inflating the node carrier again.

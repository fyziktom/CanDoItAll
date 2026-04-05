# Implementation plan

## Remediation goal

Keep Node as the stable workbench-authored carrier, but split schedule, spatial-semantic state, signals, attachment/storage bindings, and typed facet data into clearly owned companion records or facet tables.

## Ordered steps

- Extract a narrow `NodeCarrier` owner for stable node identity, basic text, and containment/backing metadata only.
- Move schedule, spatial semantics, markers, artifact/storage bindings, and typed details to companion records/facets with explicit owners.
- Update mappings so UI surfaces read an assembled DTO rather than the persistence record directly.
- Plan a migration path that preserves existing node identity and existing data while splitting concerns.

## Guardrails

- Do not demote semantically meaningful X/Y and semantic markers to disposable UI state.
- Do not keep multiple writable copies of the same schedule/signal/assignment fact.
- Prefer compatibility shims over a big-bang table rewrite.

## Acceptance criteria

- NodeCarrier no longer mixes route/storage/media/signal/schedule/facet payloads into one universal record.
- Spatial semantics remain canonical where the product requires them.
- Typed facet data can evolve without inflating the node carrier again.

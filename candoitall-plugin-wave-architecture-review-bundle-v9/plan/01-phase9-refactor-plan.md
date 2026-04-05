# Phase9 refactor plan
## Phase 1 — retire legacy node carrier
- remove active legacy carrier fields from ProjectObjectRecord
- migrate / backfill binding records
- remove ProjectObjects carrier columns
- update mapping/tests

## Phase 2 — retire read-time normalization
- move binding/marker cleanup to one-shot maintenance migration
- remove SaveChangesAsync from NormalizeAndHydrateAsync
- remove load-path calls from ProjectStructureAssemblyService.LoadAsync

## Phase 3 — single marker truth
- choose canonical marker persistence
- delete scalar marker persistence
- update graph mapping/tests/search helpers

## Phase 4 — plugin-first editor/runtime
- introduce generic connector config state bag
- build generic field renderer by `ConnectorConfigFieldType`
- add integration tests with an unknown plugin manifest

## Phase 5 — legacy enum demotion
- make plugin key authoritative
- remove fallback enum persistence for custom plugins
- demote or retire ProviderKind/ResourceKind in active flows

## Phase 6 — extensible node references
- replace closed-world enum/property-bag reference model
- migrate existing first-party references
- add plugin-defined reference tests

## Phase 7 — durable connector command boundary
- design and implement generic write-side connector execution boundary
- add retry/idempotency/replay tests

# Codex task prompt — ACR-009

Implement finding `ACR-009` from this subbundle.

## Required stance

- follow the bundle architecture
- do not solve this by introducing a new parallel truth
- keep changes aligned with `Phase 4`
- preserve node-as-carrier and canonical spatial semantics where relevant
- add required positive and negative tests
- run the validation commands
- produce evidence for QA

## Finding summary

A 2900+ line service owns graph sync, CRUD, transfer, media save, view state, command translation, DTO mapping, and now indirectly participates in party integration flows, making safe change difficult.

## Ordered implementation steps

- Split `ProjectWorkbenchService` into focused services: graph assembly, node mutation, edge mutation, facet mutation, projection building, artifact/media binding, and transfer/subproject operations.
- Keep a thin facade if needed for compatibility, but move logic into cohesive services with clear test boundaries.
- Refactor dependency injection and tests around the new seams.
- Use the split to remove duplicated sync/projection responsibilities from the mutation paths.

## Guardrails

- Do not split the service cosmetically without real ownership boundaries.
- Do not move business rules into Blazor pages or DTO mappers.

## Done means

- Hotspot service line count and dependency surface shrink materially.
- Extracted collaborators own coherent responsibilities and are independently testable.

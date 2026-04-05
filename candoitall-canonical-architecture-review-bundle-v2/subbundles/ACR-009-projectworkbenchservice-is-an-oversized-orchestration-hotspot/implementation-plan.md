# Implementation plan

## Remediation goal

Split graph assembly, mutation invariants, projection builders, attachment/media handling, typed-facet lifecycle, and view-state handling into focused services behind a thin façade.

## Ordered steps

- Split `ProjectWorkbenchService` into focused services: graph assembly, node mutation, edge mutation, facet mutation, projection building, artifact/media binding, and transfer/subproject operations.
- Keep a thin facade if needed for compatibility, but move logic into cohesive services with clear test boundaries.
- Refactor dependency injection and tests around the new seams.
- Use the split to remove duplicated sync/projection responsibilities from the mutation paths.

## Guardrails

- Do not split the service cosmetically without real ownership boundaries.
- Do not move business rules into Blazor pages or DTO mappers.

## Acceptance criteria

- Hotspot service line count and dependency surface shrink materially.
- Extracted collaborators own coherent responsibilities and are independently testable.

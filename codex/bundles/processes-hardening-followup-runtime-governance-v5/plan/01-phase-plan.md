# Phase Plan

## Execution Order

1. Persisted step operation contract fields.
2. Operation-aware tool policy.
3. Trusted grounding source ledger.
4. Storage-backed artifact validation.
5. Artifact lineage identity.
6. Workflow/subprocess output mapping.
7. Recovery continuation.
8. Runtime invariant audit.
9. Typed blocked/failed lifecycle.
10. Generic scenario harness.

## Closure Gate

The bundle is complete only when:

- focused unit/integration tests pass,
- process definition linter tests pass,
- operation-aware tool policy tests pass,
- workflow/subprocess mapping tests pass,
- artifact validation tests pass,
- run-start/publish lint gate tests pass,
- no SQLite runtime is reintroduced,
- full solution build passes,
- generic red-team scenarios pass.

# Implementation prompt

Implement exactly one selected subbundle at a time.

## Mandatory read order

1. Root `README.md`
2. `requirements/01-normalized-requirements.md`
3. `requirements/02-execution-invariants.md`
4. `architecture/01-target-solution.md`
5. `plan/01-phase-plan.md`
6. the selected subbundle README
7. `traceability/01-requirement-traceability.md`
8. `reviews/01-execution-report.md`

## Execution rules

- Do not start a subbundle before its prerequisite gate has passed.
- Keep `ProcessesService` as a thin façade if that reduces churn, but move behavior into smaller internals where the bundle requires it.
- Preserve one source of truth per concept.
- Do not hide compatibility fallback logic in multiple helpers.
- Do not let validation mutate state.
- Do not mark a subbundle complete until its proof contract and progression gate are satisfied.
- If a later discovery weakens an earlier foundation, reopen the earlier subbundle or trigger a corrective subbundle.

## Documentation rules

- Update the selected subbundle status as execution progresses.
- Update `reviews/01-execution-report.md` while proof is fresh.
- Update `reviews/02-architecture-gate-memo-log.md` at every gate.
- Update traceability if corrective work changes the plan.

# Implementation plan

## Remediation goal

Create an architectural test ring for invariants, relation policy, assembled graph equivalence, cache non-authoritativeness, node-assignment integrity, and node-evolution history.

## Ordered steps

- Add canonical invariant tests for containment, dependency, kind policy, assignment policy, and node transition history.
- Add projection-equivalence tests for structure/calendar/Gantt over assembled graph output.
- Add negative tests for invalid node-scoped assignments, illegal transitions, and metadata/assignment divergence.
- Make these tests a gate before any future feature wave touching the project graph or CRM/HR overlays.

## Guardrails

- Do not wait until after big refactors to add the guardrail tests.
- Do not count positive happy-path UI tests as sufficient canonical-model coverage.

## Acceptance criteria

- Every major stabilization phase adds or updates guardrail tests before code churn.
- The bundle can be executed phase-by-phase with increasing confidence rather than one risky big bang.

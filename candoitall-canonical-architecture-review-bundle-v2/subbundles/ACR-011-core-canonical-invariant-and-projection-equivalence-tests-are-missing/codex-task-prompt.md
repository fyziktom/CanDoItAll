# Codex task prompt — ACR-011

Implement finding `ACR-011` from this subbundle.

## Required stance

- follow the bundle architecture
- do not solve this by introducing a new parallel truth
- keep changes aligned with `Phase 0`
- preserve node-as-carrier and canonical spatial semantics where relevant
- add required positive and negative tests
- run the validation commands
- produce evidence for QA

## Finding summary

The repo has meaningful tests, but it lacks a dedicated ring that protects truth ownership, relation invariants, node-kind registry behavior, node-assignment integrity, node-evolution history, and multi-projection equivalence during architectural stabilization.

## Ordered implementation steps

- Add canonical invariant tests for containment, dependency, kind policy, assignment policy, and node transition history.
- Add projection-equivalence tests for structure/calendar/Gantt over assembled graph output.
- Add negative tests for invalid node-scoped assignments, illegal transitions, and metadata/assignment divergence.
- Make these tests a gate before any future feature wave touching the project graph or CRM/HR overlays.

## Guardrails

- Do not wait until after big refactors to add the guardrail tests.
- Do not count positive happy-path UI tests as sufficient canonical-model coverage.

## Done means

- Every major stabilization phase adds or updates guardrail tests before code churn.
- The bundle can be executed phase-by-phase with increasing confidence rather than one risky big bang.

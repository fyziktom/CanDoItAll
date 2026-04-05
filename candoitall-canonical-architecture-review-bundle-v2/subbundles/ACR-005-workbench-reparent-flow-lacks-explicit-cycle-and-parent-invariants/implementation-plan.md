# Implementation plan

## Remediation goal

Centralize graph mutation invariants in ProjectStructureInvariantService and enforce them on create/reparent/link/move operations with dedicated tests.

## Ordered steps

- Introduce explicit reparent invariants: no self-parent, no descendant-parent, same-project only, and allowed parent kind.
- Validate against the current canonical containment graph before saving.
- Ensure edge/hierarchy updates happen transactionally so failed reparenting cannot partially mutate state.
- Add targeted regression tests for cycle attempts and invalid parent kinds.

## Guardrails

- Do not rely on UI constraints alone.
- Do not leave separate mutation entry points with different invariant logic.

## Acceptance criteria

- Self-parent and descendant-parent attempts are rejected consistently.
- Cross-project or illegal relation moves are rejected consistently.
- All mutation entry points use the same invariant service.

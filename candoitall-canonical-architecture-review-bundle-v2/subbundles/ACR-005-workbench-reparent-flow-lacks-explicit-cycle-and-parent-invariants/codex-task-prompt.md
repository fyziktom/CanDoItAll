# Codex task prompt — ACR-005

Implement finding `ACR-005` from this subbundle.

## Required stance

- follow the bundle architecture
- do not solve this by introducing a new parallel truth
- keep changes aligned with `Phase 0`
- preserve node-as-carrier and canonical spatial semantics where relevant
- add required positive and negative tests
- run the validation commands
- produce evidence for QA

## Finding summary

Projects module rejects hierarchy cycles, but workbench reparent flow updates parent/link data without a visible equivalent invariant guard for node graph cycles or self-parenting.

## Ordered implementation steps

- Introduce explicit reparent invariants: no self-parent, no descendant-parent, same-project only, and allowed parent kind.
- Validate against the current canonical containment graph before saving.
- Ensure edge/hierarchy updates happen transactionally so failed reparenting cannot partially mutate state.
- Add targeted regression tests for cycle attempts and invalid parent kinds.

## Guardrails

- Do not rely on UI constraints alone.
- Do not leave separate mutation entry points with different invariant logic.

## Done means

- Self-parent and descendant-parent attempts are rejected consistently.
- Cross-project or illegal relation moves are rejected consistently.
- All mutation entry points use the same invariant service.

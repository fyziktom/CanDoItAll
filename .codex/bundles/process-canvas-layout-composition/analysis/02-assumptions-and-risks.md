# Assumptions And Risks

## Assumptions

- "Main path" means the default route from a decision step plus non-branch structural dependencies.
- Roles, agents, and executors are represented in the authoring canvas by role requirement nodes and their responsibility or decision-authority links.
- The immediate user request is about automatic recomposition, not manual drag behavior or visual styling.

## Critical Path Risks

- If the default route is not modeled explicitly, complex decision processes will still look chaotic because the primary route and exception routes receive equal visual treatment.
- If role nodes remain in one global column, responsibility links will continue to cross unrelated steps in larger process definitions.
- If collision cleanup can move steps after lane assignment, tests may pass "no overlap" while the flow becomes less readable.

## Validation Risks

- Component tests can prove geometry constraints but cannot prove final visual clarity alone.
- Browser proof depends on a local app route with enough process data to show a meaningful canvas.
- Existing screenshot artifacts can guide expectations, but they are not proof that the new implementation renders correctly.

## Reopen Triggers

- Reopen layout tuning if browser proof shows the default path is not visually dominant.
- Reopen role anchoring if roles are still far from their assigned or decision steps.
- Reopen branch placement if branch routers overlap the spine or create ambiguous connection direction.
- Reopen the bundle if final validation cannot launch or inspect a process canvas route and no explicit validation gap is recorded.

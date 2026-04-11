# Target Process Scenarios

## Scenario A: Code Review Repair Loop

- Developer implements change.
- Reviewer inspects change.
- Branch outcomes:
  - `Approved` routes to QA.
  - `Needs repair` routes back to developer repair work.
  - `Blocked` routes to architecture or product clarification.
  - `Default` covers incomplete reviewer choice handling.
  - `Error` covers failed review execution or invalid branch selection.

## Scenario B: QA Rework Loop

- QA validates the repaired or approved change.
- Branch outcomes:
  - `Passed` routes to merge approval.
  - `Failed` routes back to developer repair.
  - `Needs reviewer confirmation` routes back to review.
  - `Default` and `Error` remain explicit branch-node outputs.

## Scenario C: Merge Approval Chain

- Release or merge approver validates readiness.
- Security or release role may feed decision input into the branch decision.
- Branch outcomes:
  - `Approved for merge` routes to done.
  - `Needs security fixes` routes to security or implementation work.
  - `Needs more QA` routes back to QA.

## Scenario D: Review Evidence Aggregation

- Multiple upstream review lanes produce artifacts for one downstream decision or router input.
- Example sources:
  - Security review emits risk findings.
  - Architecture review emits design findings.
  - QA emits validation evidence.
- The downstream review-disposition or merge-readiness decision consumes the combined inputs instead of overwriting one earlier dependency.

## Scenario E: Layout Persistence Round Trip

- A role node and a router node are moved on the canvas.
- A later interaction such as double-click editor open, selection change, or canvas rebuild occurs.
- The moved positions remain stable after that interaction and after a reload or reread of the persisted state.

## Purpose

- These scenarios make the user’s requested loops concrete before implementation.
- Later browser proof should show at least one of these scenarios rendered with visible branch nodes and readable loop or join edges.
- Scenario D exists to force an honest answer on many-to-many join semantics instead of leaving them implicit.
- Scenario E exists to force an honest answer on canonical layout persistence instead of trusting transient canvas movement.

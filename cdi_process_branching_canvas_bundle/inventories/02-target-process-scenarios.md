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

## Purpose

- These scenarios make the user’s requested loops concrete before implementation.
- Later browser proof should show at least one of these scenarios rendered with visible branch nodes and readable loop edges.

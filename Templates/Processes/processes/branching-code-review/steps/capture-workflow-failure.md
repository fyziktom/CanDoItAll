# Capture workflow failure and recovery path

**Process:** `branching-code-review` / Branching code review and merge governance  
**Step key:** `capture-workflow-failure`  
**Step kind:** End  
**Target lead hours:** 2

## Summary
Explicit error handling

## Notes
Escalate malformed or contradictory workflow states into a durable error lane with recovery instructions.

## Contracts
- Input contract: Failing branch-router state, canvas or runtime symptom, and any partial decision evidence.
- Output contract: Workflow failure record with recommended recovery path.
- Evidence contract: Failing state, visible symptom, and accountable recovery owner.

## Governance
- Decision rights: Review lead owns the escalation into the error lane until recovery ownership is explicit.
- Exception policy: Do not guess a lane when the workflow state is broken; capture failure explicitly.
- Requires approval: False
- Requires decision record: False

## Dependencies
- route-review-disposition / error

## Role assignments
- `review-lead` / Review lead => Responsible; required=True; fallback-order=0; rebind=The review lead remains accountable for the error lane until recovery is assigned.

## Artifact expectations
- `review-workflow-failure-record` -> `review-workflow-failure-record` / Review workflow failure record | kind=Decision | trust=HumanApproved | sensitivity=Internal | validation=Must capture the failing state, user-visible symptom, and the recommended recovery path.

## Artifact inputs
- From step `route-review-disposition` expectation `review-routing-decision-record`

## Branch outcomes
- No explicit branch outcomes.


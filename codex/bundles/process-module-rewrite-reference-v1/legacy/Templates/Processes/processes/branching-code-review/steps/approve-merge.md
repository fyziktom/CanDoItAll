# Approve direct merge route

**Process:** `branching-code-review` / Branching code review and merge governance  
**Step key:** `approve-merge`  
**Step kind:** Approval  
**Target lead hours:** 2

## Summary
Direct merge gate

## Notes
Approve or reject merge when the review lead explicitly routed the change straight to merge.

## Contracts
- Input contract: Review routing decision, residual risk framing, and release-note completeness for the direct merge route.
- Output contract: Approved or rejected direct merge decision with explicit rationale.
- Evidence contract: Direct merge note, residual risks, and next action if the direct merge route is blocked.

## Governance
- Decision rights: Merge approver owns the direct merge route and must keep the rationale explicit.
- Exception policy: Reject the direct merge route when review routing evidence or residual risk framing is incomplete.
- Requires approval: True
- Requires decision record: False

## Dependencies
- route-review-disposition / ready-for-merge

## Role assignments
- `merge-approver` / Merge approver => Approver; required=True; fallback-order=0; rebind=Merge approval belongs to the current branch steward, not to whoever presses the button.
- `review-lead` / Review lead => Reviewer; required=True; fallback-order=0; rebind=The review lead confirms the routed path that produced the merge-ready state.

## Artifact expectations
- `direct-merge-readiness-note` -> `merge-readiness-note` / Direct merge readiness note | kind=Decision | trust=ReviewRequired | sensitivity=Internal | validation=Must capture residual risks, merge conditions, and named approver.

## Artifact inputs
- From step `route-review-disposition` expectation `review-routing-decision-record`

## Branch outcomes
- No explicit branch outcomes.

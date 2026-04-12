# Approve merge after default normalization

**Process:** `branching-code-review` / Branching code review and merge governance  
**Step key:** `approve-merge-after-default`  
**Step kind:** Approval  
**Target lead hours:** 2

## Summary
Default-route merge gate

## Notes
Approve or reject merge after the default normalization lane has made the ambiguous route explicit.

## Contracts
- Input contract: Review routing decision, normalization note, residual risk framing, and release-note completeness.
- Output contract: Approved or rejected post-normalization merge decision with explicit rationale.
- Evidence contract: Default-route merge note, residual risks, and next action if merge is blocked after normalization.

## Governance
- Decision rights: Merge approver owns the post-normalization merge gate and cannot bypass normalization evidence.
- Exception policy: Reject merge when normalization evidence or route provenance is incomplete.
- Requires approval: True
- Requires decision record: False

## Dependencies
- route-review-disposition / __default__
- normalize-default-lane

## Role assignments
- `merge-approver` / Merge approver => Approver; required=True; fallback-order=0; rebind=Merge approval belongs to the current branch steward, not to whoever presses the button.
- `review-lead` / Review lead => Reviewer; required=True; fallback-order=0; rebind=The review lead confirms the routed path that produced the merge-ready state.

## Artifact expectations
- `default-merge-readiness-note` -> `merge-readiness-note` / Default-route merge readiness note | kind=Decision | trust=ReviewRequired | sensitivity=Internal | validation=Must capture residual risks, merge conditions, and named approver.

## Artifact inputs
- From step `route-review-disposition` expectation `review-routing-decision-record`
- From step `normalize-default-lane` expectation `review-normalization-note`

## Branch outcomes
- No explicit branch outcomes.

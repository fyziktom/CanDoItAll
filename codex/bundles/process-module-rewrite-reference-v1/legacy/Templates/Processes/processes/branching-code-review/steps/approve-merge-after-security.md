# Approve merge after security review

**Process:** `branching-code-review` / Branching code review and merge governance  
**Step key:** `approve-merge-after-security`  
**Step kind:** Approval  
**Target lead hours:** 2

## Summary
Security merge gate

## Notes
Approve or reject merge after the security lane has produced explicit trust-sensitive review evidence.

## Contracts
- Input contract: Review routing decision, security lane review note, residual risk framing, and release-note completeness.
- Output contract: Approved or rejected post-security merge decision with explicit rationale.
- Evidence contract: Security merge note, residual risks, and next action if merge is blocked after security review.

## Governance
- Decision rights: Merge approver owns the post-security merge gate and cannot bypass security evidence.
- Exception policy: Reject merge when security evidence or route provenance is incomplete.
- Requires approval: True
- Requires decision record: False

## Dependencies
- route-review-disposition / security-review
- perform-security-review

## Role assignments
- `merge-approver` / Merge approver => Approver; required=True; fallback-order=0; rebind=Merge approval belongs to the current branch steward, not to whoever presses the button.
- `review-lead` / Review lead => Reviewer; required=True; fallback-order=0; rebind=The review lead confirms the routed path that produced the merge-ready state.

## Artifact expectations
- `security-merge-readiness-note` -> `merge-readiness-note` / Security merge readiness note | kind=Decision | trust=ReviewRequired | sensitivity=Internal | validation=Must capture residual risks, merge conditions, and named approver.

## Artifact inputs
- From step `route-review-disposition` expectation `review-routing-decision-record`
- From step `perform-security-review` expectation `security-lane-note`

## Branch outcomes
- No explicit branch outcomes.

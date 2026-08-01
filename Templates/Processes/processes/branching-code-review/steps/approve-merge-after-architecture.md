# Approve merge after architecture escalation

**Process:** `branching-code-review` / Branching code review and merge governance  
**Step key:** `approve-merge-after-architecture`  
**Step kind:** Approval  
**Target lead hours:** 2

## Summary
Architecture merge gate

## Notes
Approve or reject merge after the architecture lane has resolved the non-local design concern explicitly.

## Contracts
- Input contract: Review routing decision, architecture escalation brief, residual risk framing, and release-note completeness.
- Output contract: Approved or rejected post-architecture merge decision with explicit rationale.
- Evidence contract: Architecture merge note, residual risks, and next action if merge is blocked after escalation.

## Governance
- Decision rights: Merge approver owns the post-architecture merge gate and cannot bypass architecture evidence.
- Exception policy: Reject merge when architecture evidence or route provenance is incomplete.
- Requires approval: True
- Requires decision record: False

## Dependencies
- route-review-disposition / architecture-review
- architecture-escalation

## Role assignments
- `merge-approver` / Merge approver => Approver; required=True; fallback-order=0; rebind=Merge approval belongs to the current branch steward, not to whoever presses the button.
- `review-lead` / Review lead => Reviewer; required=True; fallback-order=0; rebind=The review lead confirms the routed path that produced the merge-ready state.

## Artifact expectations
- `architecture-merge-readiness-note` -> `merge-readiness-note` / Architecture merge readiness note | kind=Decision | trust=ReviewRequired | sensitivity=Internal | validation=Must capture residual risks, merge conditions, and named approver.

## Artifact inputs
- From step `route-review-disposition` expectation `review-routing-decision-record`
- From step `architecture-escalation` expectation `architecture-escalation-brief`

## Branch outcomes
- No explicit branch outcomes.

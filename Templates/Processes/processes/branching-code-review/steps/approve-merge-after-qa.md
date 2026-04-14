# Approve merge after QA validation

**Process:** `branching-code-review` / Branching code review and merge governance  
**Step key:** `approve-merge-after-qa`  
**Step kind:** Approval  
**Target lead hours:** 2

## Summary
QA merge gate

## Notes
Approve or reject merge after the QA lane has produced explicit browser and regression evidence.

## Contracts
- Input contract: Review routing decision, QA lane validation note, residual risk framing, and release-note completeness.
- Output contract: Approved or rejected post-QA merge decision with explicit rationale.
- Evidence contract: QA merge note, residual risks, and next action if merge is blocked after QA.

## Governance
- Decision rights: Merge approver owns the post-QA merge gate and cannot bypass QA evidence.
- Exception policy: Reject merge when QA evidence or route provenance is incomplete.
- Requires approval: True
- Requires decision record: False

## Dependencies
- route-review-disposition / qa-validation
- validate-qa-lane

## Role assignments
- `merge-approver` / Merge approver => Approver; required=True; fallback-order=0; rebind=Merge approval belongs to the current branch steward, not to whoever presses the button.
- `review-lead` / Review lead => Reviewer; required=True; fallback-order=0; rebind=The review lead confirms the routed path that produced the merge-ready state.

## Artifact expectations
- `qa-merge-readiness-note` -> `merge-readiness-note` / QA merge readiness note | kind=Decision | trust=ReviewRequired | sensitivity=Internal | validation=Must capture residual risks, merge conditions, and named approver.

## Artifact inputs
- From step `route-review-disposition` expectation `review-routing-decision-record`
- From step `validate-qa-lane` expectation `qa-lane-validation-note`

## Branch outcomes
- No explicit branch outcomes.

# Perform security review before merge

**Process:** `branching-code-review` / Branching code review and merge governance  
**Step key:** `perform-security-review`  
**Step kind:** Approval  
**Target lead hours:** 6

## Summary
Trust-sensitive lane

## Notes
Review tenant-data, secrets, and policy-exception consequences for changes routed into the security lane.

## Contracts
- Input contract: Review routing decision, pull request packet, and trust-sensitive changed-surface notes.
- Output contract: Security lane outcome with explicit approval, block, or exception rationale.
- Evidence contract: Security notes, decision rationale, and residual risk owner.

## Governance
- Decision rights: Security reviewer owns the trust-sensitive merge gate for routed changes.
- Exception policy: Do not let merge urgency waive data-handling or secrets review.
- Requires approval: True
- Requires decision record: False

## Dependencies
- route-review-disposition / security-review

## Role assignments
- `security-reviewer` / Security reviewer => Approver; required=True; fallback-order=0; rebind=Security approval remains attached to the role even if the reviewer changes.

## Artifact expectations
- `security-lane-note` -> `security-review-note` / Security lane review note | kind=Decision | trust=HumanApproved | sensitivity=Confidential | validation=Must capture the trust-sensitive review outcome and residual risk owner.

## Artifact inputs
- From step `route-review-disposition` expectation `review-routing-decision-record`

## Branch outcomes
- No explicit branch outcomes.

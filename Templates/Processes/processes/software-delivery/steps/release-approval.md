# Approve release readiness

**Process:** `software-delivery` / Multi-team software delivery and release governance  
**Step key:** `release-approval`  
**Step kind:** Approval  
**Target lead hours:** 3

## Summary
Go / no-go board decision

## Notes
Approve or reject release using QA proof, security posture, rollback readiness, and support coverage.

## Contracts
- Input contract: QA evidence, security outcome, rollback plan, and support ownership.
- Output contract: Approved or rejected release readiness with accountable rationale.
- Evidence contract: Approval note, residual risk register, and rollback ownership record.

## Governance
- Decision rights: Delivery manager owns the decision and cannot waive missing proof or missing rollback readiness silently.
- Exception policy: Reject release when security review, rollback ownership, or support readiness remains incomplete.
- Requires approval: True
- Requires decision record: True

## Dependencies
- implementation
- qa-validation
- security-review

## Role assignments
- `delivery-manager` / Delivery manager => Approver; required=True; fallback-order=0; rebind=Release approval belongs to the accountable delivery manager role, not to whichever person attended a meeting.
- `release-manager` / Release manager => Reviewer; required=True; fallback-order=0; rebind=Operational release owner reviews rollback and support readiness before the board decision.

## Artifact expectations
- `release-approval-record` -> `release-approval-record` / Release approval record | kind=Decision | trust=HumanApproved | sensitivity=Internal | validation=Must name the approver, residual risk owner, rollback trigger, and release timing conditions.

## Artifact inputs
- From step `implementation` expectation `migration-rollout-preparation-checklist`
- From step `qa-validation` expectation `regression-evidence-pack`
- From step `security-review` expectation `security-exception-assessment`

## Branch outcomes
- No explicit branch outcomes.

## Checklists
- `release-go-live-checklist`

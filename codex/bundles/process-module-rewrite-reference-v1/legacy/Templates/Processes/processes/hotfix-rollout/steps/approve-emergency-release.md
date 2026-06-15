# Approve emergency release window

**Process:** `hotfix-rollout` / Emergency hotfix rollout with shard-risk governance  
**Step key:** `approve-emergency-release`  
**Step kind:** Approval  
**Target lead hours:** 1

## Summary
Explicit go / no-go and rollback trigger

## Notes
Review the emergency evidence, rollback trigger, and customer communication plan before production rollout.

## Contracts
- Input contract: Validation evidence, rollback trigger, customer-impact status, and operator readiness.
- Output contract: Go / no-go decision with explicit rollback trigger and accountable owners.
- Evidence contract: Approval note, release window, fallback trigger, and outward-communication owner.

## Governance
- Decision rights: Release approver owns the emergency release decision and cannot waive missing evidence or unclear rollback control.
- Exception policy: Reject the rollout when rollback conditions or customer-facing obligations are not explicit.
- Requires approval: True
- Requires decision record: True

## Dependencies
- assess-blast-radius
- validate-hotfix

## Role assignments
- `release-approver` / Release approver => Approver; required=True; fallback-order=0; rebind=Emergency release approval remains attached to the role, not a specific operator.
- `customer-liaison` / Customer liaison => Reviewer; required=True; fallback-order=0; rebind=Customer communication owner must review timing and outbound commitments before approval.

## Artifact expectations
- `emergency-window-approval-record` -> `emergency-window-approval-record` / Emergency release approval record | kind=Decision | trust=HumanApproved | sensitivity=Internal | validation=Must name the approver, rollback trigger, communication owner, and residual risk owner.

## Artifact inputs
- From step `assess-blast-radius` expectation `blast-radius-assessment`
- From step `validate-hotfix` expectation `emergency-validation-evidence-pack`

## Branch outcomes
- No explicit branch outcomes.

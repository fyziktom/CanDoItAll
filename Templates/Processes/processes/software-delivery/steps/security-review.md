# Perform security and data-handling review

**Process:** `software-delivery` / Multi-team software delivery and release governance  
**Step key:** `security-review`  
**Step kind:** Approval  
**Target lead hours:** 6

## Summary
Tenant data, secrets, and exception posture

## Notes
Review tenant-data handling, secrets, boundary changes, and policy exceptions before release approval.

## Contracts
- Input contract: Peer-reviewed package, changed-surface inventory, and data-handling notes.
- Output contract: Security outcome with explicit approval, block, or exception rationale.
- Evidence contract: Security review notes, exception rationale, and approved controls.

## Governance
- Decision rights: Security reviewer owns the sign-off for tenant-data and policy exceptions.
- Exception policy: Block release when data-handling review capacity is missing or exception rationale is incomplete.
- Requires approval: True
- Requires decision record: False

## Dependencies
- peer-review

## Role assignments
- `security-reviewer` / Security reviewer => Approver; required=True; fallback-order=0; rebind=Security approval remains attached to the role even if the reviewer changes.

## Artifact expectations
- `security-exception-assessment` -> `security-exception-assessment` / Security exception assessment | kind=Decision | trust=HumanApproved | sensitivity=Confidential | validation=Must capture controls, residual risk owner, and approval or block rationale.

## Artifact inputs
- No explicit artifact inputs.

## Branch outcomes
- No explicit branch outcomes.

## Checklists
- `security-review-checklist`

## Prompts
- `prompt-security-review`

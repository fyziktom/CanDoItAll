# Approve escalation path

**Process:** `incident-response` / Incident response and escalation  
**Step key:** `escalate`  
**Step kind:** Approval  
**Target lead hours:** 2

## Summary
Emergency decision gate

## Notes
Approve, block, or refuse the proposed escalation or emergency change path.

## Contracts
- Input contract: Diagnosis and proposed change path.
- Output contract: Approved, blocked, or refused escalation.
- Evidence contract: Approval record and rationale.

## Governance
- Decision rights: Approver owns the escalation gate.
- Exception policy: Do not execute high-risk mitigation without explicit approval and rollback framing.
- Requires approval: True
- Requires decision record: False

## Dependencies
- diagnose

## Role assignments
- `approver` / Approver => Approver; required=True; fallback-order=0; rebind=Escalation approval always belongs to the current approver role holder.

## Artifact expectations
- `mitigation-approval-record` -> `mitigation-approval-record` / Escalation approval record | kind=Decision | trust=HumanApproved | sensitivity=Internal | validation=Emergency change approvals must keep explicit rationale.

## Artifact inputs
- From step `diagnose` expectation `diagnosis-evidence-pack`

## Branch outcomes
- No explicit branch outcomes.

## Validations
- `validate-mitigation-approved`

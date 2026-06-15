# Make the board decision and record rationale

**Process:** `architecture-decision-governance` / Architecture decision governance and ADR stewardship  
**Step key:** `board-decision`  
**Step kind:** Approval  
**Target lead hours:** 2

## Summary
Approval is a commitment, not just agreement

## Notes
Approve, defer, or reject the recommended architecture direction with explicit rationale, dissent notes, and adoption conditions.

## Contracts
- Input contract: Reviewed ADR draft, option analysis, and security/compliance notes.
- Output contract: Approved or deferred architecture decision with adoption conditions.
- Evidence contract: Board decision record and named follow-up obligations.

## Governance
- Decision rights: Governance facilitator records the decision; service owner and domain owner must both acknowledge adoption obligations.
- Exception policy: Do not approve with placeholders for ownership or decision rationale.
- Requires approval: True
- Requires decision record: True

## Dependencies
- security-compliance-review

## Role assignments
- `service-owner` / Service owner => Approver; required=True; fallback-order=0; rebind=Operational decision approval remains with accountable owner.
- `domain-owner` / Domain owner => Reviewer; required=True; fallback-order=0; rebind=Domain owner reviews adoption burden.
- `governance-facilitator` / Governance facilitator => Responsible; required=True; fallback-order=0; rebind=Facilitator keeps the record durable.

## Artifact expectations
- `board-decision-architecture-decision-record` -> `architecture-decision-record` / Approved architecture decision record | kind=Decision | trust=HumanApproved | sensitivity= | validation=Must include decision owner, affected boundaries, irreversible consequences, and follow-up actions.

## Artifact inputs
- No explicit artifact inputs.

## Branch outcomes
- `approved` / Approved — The ADR is accepted with adoption conditions.
- `deferred` / Deferred — The ADR requires more evidence or narrower scope.

## Checklists
- `decision-readiness-checklist`

## Validations
- `validation-architecture-aligned`
- `validate-domain-owner-coverage`

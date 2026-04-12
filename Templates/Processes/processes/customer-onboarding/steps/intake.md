# Capture commercial intake

**Process:** `customer-onboarding` / Customer onboarding orchestration  
**Step key:** `intake`  
**Step kind:** Start  
**Target lead hours:** 4

## Summary
Demand handoff and initial delivery framing

## Notes
Turn signed demand into a typed onboarding brief with explicit stakeholders, target dates, constraints, and acceptance intent.

## Contracts
- Input contract: Signed scope, target dates, stakeholder summary, and customer expectations.
- Output contract: Typed intake packet ready for delivery and staffing review.
- Evidence contract: Scope summary, named stakeholders, and decision-ready onboarding notes.

## Governance
- Decision rights: Account owner can prepare intake but cannot commit delivery without review.
- Exception policy: Escalate immediately if customer commitments are implied rather than explicit.
- Requires approval: False
- Requires decision record: False

## Dependencies
- No explicit predecessor.

## Role assignments
- `account-owner` / Account owner => Responsible; required=True; fallback-order=0; rebind=Rebind to another commercial owner if the original owner changes.

## Artifact expectations
- `customer-onboarding-brief` -> `customer-onboarding-brief` / Customer onboarding brief | kind=Brief | trust=ReviewRequired | sensitivity=Internal | validation=Must identify success criteria, stakeholders, target dates, constraints, and unresolved dependencies.

## Artifact inputs
- No explicit artifact inputs.

## Branch outcomes
- No explicit branch outcomes.

## Checklists
- `customer-handoff-checklist`

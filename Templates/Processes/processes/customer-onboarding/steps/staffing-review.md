# Review staffing intent

**Process:** `customer-onboarding` / Customer onboarding orchestration  
**Step key:** `staffing-review`  
**Step kind:** Review  
**Target lead hours:** 8

## Summary
Capacity and fallback decision

## Notes
Validate the proposed onboarding staffing path, named specialists, and fallback plan before kickoff approval.

## Contracts
- Input contract: Intake packet and delivery constraints.
- Output contract: Recommended staffing path with explicit fallback and coverage gaps.
- Evidence contract: Candidate list, allocation picture, and fallback recommendation.

## Governance
- Decision rights: Staffing manager recommends; governance owner approves kickoff readiness.
- Exception policy: Do not treat partial staffing assumptions as committed staffing.
- Requires approval: False
- Requires decision record: True

## Dependencies
- intake

## Role assignments
- `staffing-manager` / Staffing manager => Responsible; required=True; fallback-order=0; rebind=Rebind to another staffing manager if allocation changes.
- `kickoff-lead` / Kickoff lead => Reviewer; required=True; fallback-order=0; rebind=Delivery lead review stays explicit even if the assigned person changes.

## Artifact expectations
- `staffing-readiness-note` -> `staffing-readiness-note` / Staffing recommendation | kind=Decision | trust=ReviewRequired | sensitivity=Internal | validation=Reviewer must confirm role-fit, availability, and fallback coverage.

## Artifact inputs
- From step `intake` expectation `customer-onboarding-brief`

## Branch outcomes
- No explicit branch outcomes.

## Checklists
- `staffing-feasibility-checklist`

## Validations
- `validate-staffing-feasible`

## Prompts
- `prompt-staffing-summary`

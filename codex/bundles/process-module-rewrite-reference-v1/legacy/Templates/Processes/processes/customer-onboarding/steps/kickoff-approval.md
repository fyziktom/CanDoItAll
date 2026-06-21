# Approve kickoff readiness

**Process:** `customer-onboarding` / Customer onboarding orchestration  
**Step key:** `kickoff-approval`  
**Step kind:** Approval  
**Target lead hours:** 2

## Summary
Formal onboarding launch gate

## Notes
Approve or reject kickoff readiness using staffing evidence, goals, milestones, and named owners.

## Contracts
- Input contract: Staffing recommendation and draft kickoff plan.
- Output contract: Approved or rejected kickoff readiness.
- Evidence contract: Approval record and managed kickoff artifacts.

## Governance
- Decision rights: Kickoff lead can approve, block, or refuse unsafe launch.
- Exception policy: Reject kickoff when ownership, milestones, or dependencies remain vague.
- Requires approval: True
- Requires decision record: True

## Dependencies
- staffing-review

## Role assignments
- `kickoff-lead` / Kickoff lead => Approver; required=True; fallback-order=0; rebind=Approval remains attached to the role, not the current person.

## Artifact expectations
- `kickoff-approval-record` -> `kickoff-packet` / Kickoff approval record | kind=Decision | trust=HumanApproved | sensitivity=Internal | validation=Approval rationale must be explicit and reviewable.

## Artifact inputs
- From step `staffing-review` expectation `staffing-readiness-note`

## Branch outcomes
- No explicit branch outcomes.

## Checklists
- `kickoff-alignment-checklist`

## Validations
- `validation-kickoff-ready`

## Prompts
- `prompt-kickoff-agenda`

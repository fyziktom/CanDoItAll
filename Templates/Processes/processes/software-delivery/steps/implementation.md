# Implement feature, tests, and migration notes

**Process:** `software-delivery` / Multi-team software delivery and release governance  
**Step key:** `implementation`  
**Step kind:** Work  
**Target lead hours:** 36

## Summary
Code, tests, and reviewable proof

## Notes
Produce the change set, migration guidance, and targeted validation without losing traceability to the approved design.

## Contracts
- Input contract: Approved architecture path, scope packet, and unresolved technical questions.
- Output contract: Review-ready implementation with tests, migration notes, and rollout checklist inputs.
- Evidence contract: Change set, test outputs, migration steps, and touched-surface inventory.

## Governance
- Decision rights: Lead engineer can implement but cannot silently alter the approved architecture or reduce proof depth.
- Exception policy: Pause when migration impact, performance risk, or dependency scope grows beyond the approved path.
- Requires approval: False
- Requires decision record: True

## Dependencies
- feature-intake
- architecture-review

## Role assignments
- `lead-engineer` / Lead engineer => Responsible; required=True; fallback-order=0; rebind=Engineering ownership moves between qualified engineers without changing the role contract.
- `product-owner` / Product owner => Reviewer; required=True; fallback-order=0; rebind=Value owner reviews only acceptance drift, not technical implementation details.

## Artifact expectations
- `implementation-change-set` -> `implementation-change-set` / Implementation change set | kind=Deliverable | trust=ReviewRequired | sensitivity=Internal | validation=Must be linked to tests, migration notes, and touched-surface inventory.
- `migration-rollout-preparation-checklist` -> `rollback-plan` / Migration and rollout preparation checklist | kind=Checklist | trust=ReviewRequired | sensitivity=Internal | validation=Must name data changes, operational preconditions, and rollback steps.

## Artifact inputs
- From step `feature-intake` expectation `scope-boundary-packet`
- From step `architecture-review` expectation `architecture-decision-record`

## Branch outcomes
- No explicit branch outcomes.

## Checklists
- `implementation-readiness-checklist`
- `delivery-scope-freeze-checklist`

## Validations
- `validate-migration-rehearsal`

## Prompts
- `prompt-release-scope-recap`

# Run atomic implementation slice

**Process:** `software-delivery` / Multi-team software delivery and release governance  
**Step key:** `implementation`  
**Step kind:** Work
**Target lead hours:** 36

## Summary
Observed implementation work

## Notes
Coordinate the implementation slice selected by the project context so setup, changes, validation, and blockers are observable from this parent delivery run.

## Contracts
- Input contract: Approved architecture path, scope packet, unresolved technical questions, and implementation-slice start criteria.
- Output contract: Completed implementation slice with validation notes, blockers, and rollout checklist inputs visible from the parent process.
- Evidence contract: Implementation journal, change set, validation outputs, migration steps, and touched-surface inventory.

## Governance
- Decision rights: Parent delivery manager owns sequencing and escalation, while the selected implementation role owns the concrete changes.
- Exception policy: Do not complete the implementation step until the selected implementation role records a completed terminal disposition with validation evidence or explicit blockers.
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

# Run atomic implementation slice

**Process:** `software-delivery` / Multi-team software delivery and release governance  
**Step key:** `implementation`  
**Step kind:** Subprocess  
**Target lead hours:** 36

## Summary
Observed implementation subprocess

## Notes
Launch the `.NET implementation slice with atomic validation` subprocess so implementation, setup, tests, and blockers are observable from this parent delivery run.

## Contracts
- Input contract: Approved architecture path, scope packet, unresolved technical questions, and implementation-slice start criteria.
- Output contract: Completed child implementation slice with tests, migration notes, blockers, and rollout checklist inputs visible from the parent process.
- Evidence contract: Child run journal, implementation change set, test outputs, migration steps, and touched-surface inventory.

## Governance
- Decision rights: Parent delivery manager can instruct the child manager, but implementation changes remain owned by the child slice roles.
- Exception policy: Do not complete the parent implementation step until the child implementation slice reaches a completed terminal state.
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

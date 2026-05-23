# Implement bounded delivery change

**Process:** `software-delivery` / Multi-team software delivery and release governance  
**Step key:** `implementation`  
**Step kind:** Work
**Target lead hours:** 36

## Summary
Concrete delivery work

## Notes
Implement the bounded change directly from approved project-structure context and upstream artifacts. Preserve explicit source-of-truth constraints such as platform, stack, runtime target, output location, exclusions, and acceptance criteria; block instead of narrowing, switching, or deferring a required behavior without a decision record.

## Contracts
- Input contract: Approved architecture path, scope packet, unresolved technical questions, and implementation-slice start criteria.
- Output contract: Reviewable implementation change set that satisfies the current project-structure source of truth, with blockers and rollout checklist inputs visible from the parent process.
- Evidence contract: Changed or created deliverables, validation outputs, output-placement notes, migration steps when applicable, and touched-surface inventory.

## Governance
- Decision rights: Parent delivery manager owns sequencing and escalation, while the selected implementation role owns the concrete changes.
- Exception policy: Do not complete the implementation step if any explicit project-structure requirement is omitted, deferred, stack-switched, or unverifiable without an accepted decision record.
- Requires approval: False
- Requires decision record: True

## Dependencies
- feature-intake
- architecture-review

## Role assignments
- `lead-engineer` / Lead engineer => Responsible; required=True; fallback-order=0; rebind=Engineering ownership moves between qualified engineers without changing the role contract.
- `product-owner` / Product owner => Reviewer; required=True; fallback-order=0; rebind=Value owner reviews only acceptance drift, not technical implementation details.

## Artifact expectations
- `implementation-change-set` -> `implementation-change-set` / Implementation change set | kind=Deliverable | trust=ReviewRequired | sensitivity=Internal | validation=Must list changed or created product files, output target, validation proof tied to acceptance criteria, and confirmation that explicit project-structure requirements were not dropped or deferred.
- `migration-rollout-preparation-checklist` -> `rollback-plan` / Migration and rollout preparation checklist | kind=Checklist | trust=ReviewRequired | sensitivity=Internal | validation=Must name data changes or none, operational preconditions, publish or output-placement steps, and rollback or removal steps.

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

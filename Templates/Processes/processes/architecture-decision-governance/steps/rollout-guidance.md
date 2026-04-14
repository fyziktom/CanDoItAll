# Publish rollout guidance and revisit triggers

**Process:** `architecture-decision-governance` / Architecture decision governance and ADR stewardship  
**Step key:** `rollout-guidance`  
**Step kind:** End  
**Target lead hours:** 4

## Summary
Translate decision into executable downstream expectations

## Notes
Turn the approved ADR into rollout guidance, architectural guardrails, and explicit revisit triggers so implementation teams know how to apply the decision.

## Contracts
- Input contract: Approved ADR and adoption conditions.
- Output contract: Published rollout guidance and revisit criteria.
- Evidence contract: Architecture guidance note and future revisit triggers.

## Governance
- Decision rights: Solution architect owns technical guidance; governance facilitator ensures revisit criteria are durable.
- Exception policy: If rollout expectations cannot be stated clearly, the decision is not ready for downstream implementation.
- Requires approval: False
- Requires decision record: False

## Dependencies
- board-decision

## Role assignments
- `solution-architect` / Solution architect => Responsible; required=True; fallback-order=0; rebind=Architecture guidance ownership remains with the architect.
- `governance-facilitator` / Governance facilitator => Reviewer; required=True; fallback-order=0; rebind=Facilitator checks record completeness.
- `product-owner` / Product owner => Reviewer; required=True; fallback-order=1; rebind=Product owner reviews value-delivery implications.

## Artifact expectations
- `rollout-guidance-implementation-plan` -> `implementation-plan` / Architecture rollout guidance | kind=Brief | trust= | sensitivity= | validation=Must identify dependency sequencing, fallback approach, and proof expectations step by step.
- `rollout-guidance-retrospective-improvement-log` -> `retrospective-improvement-log` / Architecture revisit trigger log | kind=Brief | trust= | sensitivity= | validation=Must identify observed problem, root cause or likely cause, owner, and follow-up expectation.

## Artifact inputs
- No explicit artifact inputs.

## Branch outcomes
- No explicit branch outcomes.

## Checklists
- `architecture-gate-checklist`

## Validations
- `validation-architecture-aligned`

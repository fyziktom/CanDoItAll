# Capture architecture decision demand

**Process:** `architecture-decision-governance` / Architecture decision governance and ADR stewardship  
**Step key:** `decision-intake`  
**Step kind:** Start  
**Target lead hours:** 4

## Summary
Define the decision boundary and why now

## Notes
Normalize the incoming design tension into a single decision problem statement with scope, urgency, and impacted domains.

## Contracts
- Input contract: Design concern, change proposal, incident learning, or product demand requiring architecture direction.
- Output contract: Typed decision intake with named context owner and impacted domains.
- Evidence contract: Decision intake brief and initial affected-system list.

## Governance
- Decision rights: Governance facilitator may reject vague requests that do not define a real decision.
- Exception policy: Do not proceed when scope and affected domains are not explicit.
- Requires approval: False
- Requires decision record: False

## Dependencies
- No explicit predecessor.

## Role assignments
- `governance-facilitator` / Governance facilitator => Responsible; required=True; fallback-order=0; rebind=Facilitation remains with trained board coordinator.
- `domain-owner` / Domain owner => Reviewer; required=True; fallback-order=0; rebind=A domain owner must review the intake boundary.

## Artifact expectations
- `decision-intake-decision-brief` -> `decision-brief` / Decision brief | kind= | trust= | sensitivity= | validation=Must make the decision question explicit, not only the desired answer.
- `decision-intake-intake-brief` -> `intake-brief` / Architecture demand intake | kind=Brief | trust= | sensitivity= | validation=Must identify request source, decision owner, scope boundary, and missing inputs explicitly.

## Artifact inputs
- No explicit artifact inputs.

## Branch outcomes
- No explicit branch outcomes.

## Checklists
- `decision-readiness-checklist`

## Validations
- `validate-domain-owner-coverage`

## Prompts
- `prompt-adr-question-framing`

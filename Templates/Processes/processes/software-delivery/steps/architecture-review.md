# Review architecture and canonical-model impact

**Process:** `software-delivery` / Multi-team software delivery and release governance  
**Step key:** `architecture-review`  
**Step kind:** Review  
**Target lead hours:** 12

## Summary
Cross-module and source-of-truth decision

## Notes
Validate process/workspace/billing seams, canonical model implications, migration ownership, and integration boundaries before implementation starts.

## Contracts
- Input contract: Scope packet, touched modules, data-flow map, and integration concerns.
- Output contract: Approved architecture path with explicit trade-offs and rejected alternatives.
- Evidence contract: Architecture notes, canonical-model decision, and source-of-truth rationale.

## Governance
- Decision rights: Architecture authority recommends the path; delivery manager remains accountable for choosing the approved option.
- Exception policy: Do not continue while source-of-truth ownership or migration responsibility remains ambiguous.
- Requires approval: False
- Requires decision record: True

## Dependencies
- feature-intake

## Role assignments
- `solution-architect` / Solution architect => Responsible; required=True; fallback-order=0; rebind=Architecture authority may be reassigned between vetted humans or approved architecture agents.
- `lead-engineer` / Lead engineer => Reviewer; required=True; fallback-order=0; rebind=Implementation owner must confirm the design is buildable before approval.

## Artifact expectations
- `architecture-decision-record` -> `architecture-decision-record` / Architecture decision record | kind=Decision | trust=HumanApproved | sensitivity=Internal | validation=Must capture selected option, rejected options, source-of-truth choice, and migration ownership.

## Artifact inputs
- From step `feature-intake` expectation `scope-boundary-packet`

## Branch outcomes
- No explicit branch outcomes.

## Checklists
- `architecture-gate-checklist`

## Validations
- `validate-architecture-boundaries`

## Prompts
- `prompt-architecture-review`

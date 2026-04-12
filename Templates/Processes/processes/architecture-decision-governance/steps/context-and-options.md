# Assemble context, constraints, and options

**Process:** `architecture-decision-governance` / Architecture decision governance and ADR stewardship  
**Step key:** `context-and-options`  
**Step kind:** Work  
**Target lead hours:** 10

## Summary
Evidence before preference

## Notes
Collect current-state constraints, target outcomes, option set, and measurable trade-offs before the board debates a preferred answer.

## Contracts
- Input contract: Decision intake, current system context, risk drivers, and domain constraints.
- Output contract: ADR draft with options, trade-offs, and recommendation.
- Evidence contract: Draft ADR and supporting context evidence.

## Governance
- Decision rights: Solution architect authors the option analysis; domain owner confirms local reality and adoption cost.
- Exception policy: If fewer than two meaningful options are considered, explain why explicitly.
- Requires approval: False
- Requires decision record: True

## Dependencies
- decision-intake

## Role assignments
- `solution-architect` / Solution architect => Responsible; required=True; fallback-order=0; rebind=Architecture authorship remains explicit.
- `domain-owner` / Domain owner => Reviewer; required=True; fallback-order=0; rebind=Domain-owner review is required.
- `product-owner` / Product owner => Reviewer; required=True; fallback-order=1; rebind=Product value trade-offs are reviewed before board discussion.

## Artifact expectations
- `context-and-options-architecture-decision-record` -> `architecture-decision-record` / Architecture decision record | kind= | trust= | sensitivity= | validation=Must include decision owner, affected boundaries, irreversible consequences, and follow-up actions.
- `context-and-options-decision-brief` -> `decision-brief` / Option comparison brief | kind=Decision | trust= | sensitivity= | validation=Must make the decision question explicit, not only the desired answer.

## Artifact inputs
- No explicit artifact inputs.

## Branch outcomes
- No explicit branch outcomes.

## Checklists
- `architecture-gate-checklist`
- `decision-readiness-checklist`

## Validations
- `validation-architecture-aligned`
- `validate-domain-owner-coverage`

## Prompts
- `prompt-architecture-review`

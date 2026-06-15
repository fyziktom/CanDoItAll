# Capture change demand and human acceptance boundary

**Process:** `ai-assisted-change-delivery` / AI-assisted change delivery with guarded delegation  
**Step key:** `task-intake`  
**Step kind:** Start  
**Target lead hours:** 4

## Summary
AI work starts from a human-owned contract

## Notes
Normalize the requested change into a bounded problem statement, value target, exclusions, and human acceptance criteria before any agent is engaged.

When the current project structure or mindmap already supplies a concrete deliverable, product root, feature boundary, exclusions, and acceptance criteria, treat those explicit facts as the current human acceptance boundary for this intake step. Mindmap facts may be carried by child node titles, labels, and hierarchy, not only by long notes fields; an empty note body does not make explicit child-node requirements missing. Do not require a separate approval artifact just to repeat the same facts; record missing or contradictory owner input only when the mindmap leaves a core deliverable, authority, cost/license constraint, or safe execution boundary unresolved.

## Contracts
- Input contract: Feature request, defect, or improvement demand with business context.
- Output contract: Bounded change brief with explicit human acceptance boundary.
- Evidence contract: Intake brief and acceptance criteria map.

## Governance
- Decision rights: Product owner owns value boundary; no agent may redefine acceptance criteria alone.
- Exception policy: Do not delegate ambiguous or ownerless tasks.
- Requires approval: False
- Requires decision record: False

## Dependencies
- No explicit predecessor.

## Role assignments
- `product-owner` / Product owner => Responsible; required=True; fallback-order=0; rebind=Product owner retains accountability for the change intent.
- `solution-architect` / Solution architect => Reviewer; required=True; fallback-order=0; rebind=Architecture review confirms technical boundary.

## Artifact expectations
- `task-intake-intake-brief` -> `intake-brief` / Intake brief | kind= | trust= | sensitivity= | validation=Must identify request source, decision owner, scope boundary, and missing inputs explicitly.
- `intake-acceptance-criteria-pack` -> `acceptance-criteria-pack` / Acceptance criteria pack | kind= | trust= | sensitivity= | validation=Must copy acceptance criteria from the current project structure or approval source; explicit project-structure criteria count as acknowledged for this intake step unless they are missing or contradictory.

## Artifact inputs
- No explicit artifact inputs.

## Branch outcomes
- No explicit branch outcomes.

## Checklists
- `implementation-readiness-checklist`

## Validations
- `validation-intake-complete`

## Prompts
- `prompt-implementation-brief`

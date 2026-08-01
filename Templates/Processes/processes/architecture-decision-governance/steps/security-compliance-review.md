# Review security, compliance, and policy impact

**Process:** `architecture-decision-governance` / Architecture decision governance and ADR stewardship  
**Step key:** `security-compliance-review`  
**Step kind:** Review  
**Target lead hours:** 6

## Summary
Decision quality includes control impact

## Notes
Evaluate whether the proposed options change data exposure, trust assumptions, regulatory boundaries, or operational policy obligations.

## Contracts
- Input contract: ADR draft, system context, data flows, control inventory, and third-party implications.
- Output contract: Review note covering security and policy implications.
- Evidence contract: Security and compliance findings tied to decision options.

## Governance
- Decision rights: Security reviewer and compliance steward may force rework when controls are under-specified.
- Exception policy: No approval when control obligations are ambiguous.
- Requires approval: False
- Requires decision record: False

## Dependencies
- context-and-options

## Role assignments
- `security-reviewer` / Security reviewer => Responsible; required=True; fallback-order=0; rebind=Security review remains explicit.
- `compliance-steward` / Compliance steward => Reviewer; required=False; fallback-order=1; rebind=Compliance review becomes required for regulated domains.
- `service-owner` / Service owner => Reviewer; required=True; fallback-order=1; rebind=Service owner reviews operational control impact.

## Artifact expectations
- `security-compliance-review-security-review-note` -> `security-review-note` / Security review note | kind= | trust= | sensitivity= | validation=Must state reviewed scope, identified risks, required controls, and explicit approval or exception status.
- `security-compliance-review-provenance-report` -> `provenance-report` / Dependency and provenance impact note | kind=Evidence | trust= | sensitivity= | validation=Must identify origin, producing system, trust assumptions, and gaps or manual overrides.

## Artifact inputs
- No explicit artifact inputs.

## Branch outcomes
- No explicit branch outcomes.

## Checklists
- `security-review-checklist`

## Validations
- `validation-security-clear`

## Prompts
- `prompt-architecture-review`

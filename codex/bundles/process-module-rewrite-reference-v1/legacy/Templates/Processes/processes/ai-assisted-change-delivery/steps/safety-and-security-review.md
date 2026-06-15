# Review safety, security, and residual risk

**Process:** `ai-assisted-change-delivery` / AI-assisted change delivery with guarded delegation  
**Step key:** `safety-and-security-review`  
**Step kind:** Approval  
**Target lead hours:** 4

## Summary
The output must be acceptable, not merely functional

## Notes
Assess whether the agent-assisted output changes trust boundaries, introduces insecure patterns, or requires residual-risk ownership before merge.

## Contracts
- Input contract: Evaluation report, execution trace, code diff, and sensitive-scope map.
- Output contract: Approved, held, or rejected merge recommendation.
- Evidence contract: Safety/security approval record with residual risk statement.

## Governance
- Decision rights: Security reviewer and model-risk approver may block merge; release approver owns final risk acceptance for guarded lanes.
- Exception policy: Do not merge when control impact is ambiguous.
- Requires approval: True
- Requires decision record: True

## Dependencies
- evaluation-and-benchmarking

## Role assignments
- `security-reviewer` / Security reviewer => Reviewer; required=True; fallback-order=0; rebind=Security review remains required.
- `model-risk-approver` / Model risk approver => Reviewer; required=True; fallback-order=1; rebind=Model-risk review remains explicit for guarded autonomy.
- `release-approver` / Release approver => Approver; required=True; fallback-order=0; rebind=Controlled merge approval stays human.

## Artifact expectations
- `safety-and-security-review-security-review-note` -> `security-review-note` / Security review note | kind= | trust= | sensitivity= | validation=Must state reviewed scope, identified risks, required controls, and explicit approval or exception status.
- `safety-and-security-review-release-readiness-report` -> `release-readiness-report` / AI-assisted merge approval note | kind=Decision | trust=HumanApproved | sensitivity= | validation=Must identify unresolved conditions, rollback posture, support coverage, and explicit decision recommendation.

## Artifact inputs
- No explicit artifact inputs.

## Branch outcomes
- `approved` / Approved — The output may proceed to controlled merge.
- `rework` / Rework — The output must be revised or re-evaluated before merge.

## Checklists
- `security-review-checklist`
- `ai-governance-checklist`

## Validations
- `validation-security-clear`
- `validation-release-authorized`

## Prompts
- `prompt-security-review`
- `prompt-ai-risk-brief`

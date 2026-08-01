# Validate emergency fix in shadow environment

**Process:** `hotfix-rollout` / Emergency hotfix rollout with shard-risk governance  
**Step key:** `validate-hotfix`  
**Step kind:** Review  
**Target lead hours:** 2

## Summary
Fast but explicit regression gate

## Notes
Run the emergency checklist against the hotfix bundle in a shadow or representative environment before release approval.

## Contracts
- Input contract: Emergency patch bundle, known blast radius, and incident reproduction notes.
- Output contract: Focused validation result with residual risks and unsupported cases.
- Evidence contract: Checklist output, shadow-environment notes, and residual-risk annotations.

## Governance
- Decision rights: QA responder may block the rollout if the emergency evidence is too thin for the risk profile.
- Exception policy: Do not convert the gate into a verbal approval; evidence still needs typed reviewable form.
- Requires approval: False
- Requires decision record: False

## Dependencies
- package-hotfix

## Role assignments
- `qa-lead` / QA lead => Responsible; required=True; fallback-order=0; rebind=Emergency QA ownership may move across the responder rota but the gate remains explicit.
- `platform-engineer` / Platform engineer => Reviewer; required=True; fallback-order=0; rebind=Package owner reviews failures and unsupported coverage before approval.

## Artifact expectations
- `emergency-validation-evidence-pack` -> `emergency-validation-evidence-pack` / Emergency validation evidence pack | kind=Evidence | trust=HumanApproved | sensitivity=Internal | validation=Must name validated flows, skipped checks, and residual risk that the approver must accept explicitly.

## Artifact inputs
- No explicit artifact inputs.

## Branch outcomes
- No explicit branch outcomes.

## Checklists
- `emergency-window-checklist`

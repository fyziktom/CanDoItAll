# Package emergency hotfix and rollback scripts

**Process:** `hotfix-rollout` / Emergency hotfix rollout with shard-risk governance  
**Step key:** `package-hotfix`  
**Step kind:** Work  
**Target lead hours:** 4

## Summary
Controlled change assembly

## Notes
Assemble the emergency patch, deployment instructions, and rollback scripts under one accountable engineer.

## Contracts
- Input contract: Blast-radius assessment, target change scope, and deployment constraints.
- Output contract: Hotfix package with rollout steps, rollback scripts, and changed-surface inventory.
- Evidence contract: Patch diff, deployment bundle, schema scripts, and operator checklist.

## Governance
- Decision rights: Platform engineer owns assembly but cannot expand scope beyond the approved emergency boundary.
- Exception policy: Pause immediately when the required fix grows into an unreviewable multi-area release.
- Requires approval: False
- Requires decision record: True

## Dependencies
- assess-blast-radius

## Role assignments
- `platform-engineer` / Platform engineer => Responsible; required=True; fallback-order=0; rebind=Emergency packaging remains attached to the platform-engineer role even if the individual changes.
- `database-engineer` / Database engineer => Reviewer; required=True; fallback-order=0; rebind=Database review is mandatory for scripts that touch shard state or tenant data.

## Artifact expectations
- `hotfix-package` -> `hotfix-package` / Emergency patch and rollback bundle | kind=Deliverable | trust=ReviewRequired | sensitivity=Confidential | validation=Must link the exact patch, database scripts, rollback path, and operator checklist.

## Artifact inputs
- No explicit artifact inputs.

## Branch outcomes
- No explicit branch outcomes.

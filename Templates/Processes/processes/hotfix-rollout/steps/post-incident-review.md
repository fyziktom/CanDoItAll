# Capture post-incident learning and corrective actions

**Process:** `hotfix-rollout` / Emergency hotfix rollout with shard-risk governance  
**Step key:** `post-incident-review`  
**Step kind:** End  
**Target lead hours:** 6

## Summary
Forensic replay and systemic follow-up

## Notes
Turn the failed or recovered emergency path into explicit learning about detection, coordination, rollback, and architecture weaknesses.

## Contracts
- Input contract: Rollout outcome, telemetry record, customer communications, and command timeline.
- Output contract: Post-incident review with corrective actions, owner assignments, and simulation updates.
- Evidence contract: Timeline, contributing factors, missing controls, and next corrective actions.

## Governance
- Decision rights: Incident commander owns follow-up assignment while engineering and communications roles retain accountability for their control gaps.
- Exception policy: Do not close the emergency process while corrective actions remain unnamed or unowned.
- Requires approval: False
- Requires decision record: True

## Dependencies
- execute-emergency-rollout

## Role assignments
- `incident-commander` / Incident commander => Responsible; required=True; fallback-order=0; rebind=The command role remains accountable until corrective actions have owners.
- `platform-engineer` / Platform engineer => Reviewer; required=True; fallback-order=0; rebind=Engineering role reviews technical corrective actions.
- `customer-liaison` / Customer liaison => Reviewer; required=True; fallback-order=0; rebind=Customer communication owner reviews trust and communication lessons.

## Artifact expectations
- `post-incident-corrective-actions` -> `retrospective-improvement-log` / Post-incident corrective action log | kind=Decision | trust=ReviewRequired | sensitivity=Internal | validation=Must capture missing controls, decision latency, and concrete corrective actions with owners.

## Artifact inputs
- No explicit artifact inputs.

## Branch outcomes
- No explicit branch outcomes.

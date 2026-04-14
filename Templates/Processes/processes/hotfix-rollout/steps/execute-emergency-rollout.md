# Execute emergency rollout and watch telemetry

**Process:** `hotfix-rollout` / Emergency hotfix rollout with shard-risk governance  
**Step key:** `execute-emergency-rollout`  
**Step kind:** Delivery  
**Target lead hours:** 2

## Summary
Controlled production action

## Notes
Roll out the patch inside the approved window while telemetry, shard locks, and customer communication remain actively managed.

## Contracts
- Input contract: Approved release record, deployment bundle, telemetry checkpoints, and customer message cadence.
- Output contract: Executed rollout with explicit telemetry outcome and rollback state.
- Evidence contract: Operator notes, telemetry checkpoints, rollback invocation if needed, and customer update timeline.

## Governance
- Decision rights: Platform engineer may execute only inside the approved window and rollback trigger boundaries.
- Exception policy: Trigger rollback immediately when shard lock duration, tenant impact, or telemetry drift breaches the approved threshold.
- Requires approval: False
- Requires decision record: False

## Dependencies
- approve-emergency-release

## Role assignments
- `platform-engineer` / Platform engineer => Responsible; required=True; fallback-order=0; rebind=Execution ownership remains with the platform-engineer role until rollout completes or fails.
- `incident-commander` / Incident commander => Reviewer; required=True; fallback-order=0; rebind=Incident commander reviews telemetry and escalation timing throughout the rollout.
- `database-engineer` / Database engineer => Backup; required=False; fallback-order=1; rebind=Database specialist remains an explicit fallback for rollback execution.

## Artifact expectations
- `hotfix-rollout-log` -> `rollback-plan` / Emergency rollout and telemetry log | kind=Transcript | trust=ReviewRequired | sensitivity=Internal | validation=Must capture rollout timing, telemetry checkpoints, and any rollback invocation.

## Artifact inputs
- From step `approve-emergency-release` expectation `emergency-window-approval-record`

## Branch outcomes
- No explicit branch outcomes.

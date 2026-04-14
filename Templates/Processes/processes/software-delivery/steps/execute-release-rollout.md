# Execute controlled release rollout

**Process:** `software-delivery` / Multi-team software delivery and release governance  
**Step key:** `execute-release-rollout`  
**Step kind:** Delivery  
**Target lead hours:** 4

## Summary
Live execution and first-hour telemetry watch

## Notes
Deploy the approved change inside the controlled release window while telemetry and rollback readiness remain actively managed.

## Contracts
- Input contract: Approved release record, deployment package, rollback plan, and telemetry watch points.
- Output contract: Executed rollout with explicit telemetry outcome, rollback status, and live-watch notes.
- Evidence contract: Operator notes, telemetry checkpoints, and any rollback invocation or release halt.

## Governance
- Decision rights: Release manager may execute only inside the approved window and rollback-trigger boundaries.
- Exception policy: Trigger halt or rollback immediately when telemetry, tenant impact, or operational constraints breach the approved threshold.
- Requires approval: False
- Requires decision record: False

## Dependencies
- release-approval

## Role assignments
- `release-manager` / Release manager => Responsible; required=True; fallback-order=0; rebind=Execution ownership remains with the release-manager role until rollout completes or fails.
- `lead-engineer` / Lead engineer => Backup; required=False; fallback-order=1; rebind=Implementation owner stays available for rapid corrective interpretation during rollout.
- `delivery-manager` / Delivery manager => Reviewer; required=True; fallback-order=0; rebind=Governance owner reviews telemetry and escalation timing during the release window.

## Artifact expectations
- `deployment-watch-log` -> `release-readiness-report` / Deployment and telemetry watch log | kind=Transcript | trust=ReviewRequired | sensitivity=Internal | validation=Must capture release timing, telemetry checkpoints, and any halt or rollback decision.

## Artifact inputs
- From step `release-approval` expectation `release-approval-record`

## Branch outcomes
- No explicit branch outcomes.

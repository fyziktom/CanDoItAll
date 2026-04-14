# Execute cutover and observe live telemetry

**Process:** `release-readiness-and-deployment` / Release readiness and deployment control  
**Step key:** `execute-cutover`  
**Step kind:** Delivery  
**Target lead hours:** 4

## Summary
Run only inside the approved envelope

## Notes
Execute the approved deployment sequence, monitor live indicators, and invoke rollback if the approved threshold is breached.

## Contracts
- Input contract: Go decision, cutover brief, watch roster, telemetry thresholds, and rollback triggers.
- Output contract: Executed release with live status and next action record.
- Evidence contract: Operator execution note, telemetry snapshots, and rollback note if used.

## Governance
- Decision rights: Platform engineer performs the cutover; change manager controls timing and handoffs.
- Exception policy: Rollback immediately when thresholds or instructions dictate it.
- Requires approval: False
- Requires decision record: False

## Dependencies
- security-and-go-no-go

## Role assignments
- `platform-engineer` / Platform engineer => Responsible; required=True; fallback-order=0; rebind=Execution remains with the platform engineer.
- `change-manager` / Change manager => Reviewer; required=True; fallback-order=0; rebind=Change manager reviews cutover timing.
- `service-owner` / Service owner => Backup; required=False; fallback-order=1; rebind=Service owner remains fallback during live watch.

## Artifact expectations
- `execute-cutover-rollback-plan` -> `rollback-plan` / Rollback plan | kind= | trust= | sensitivity= | validation=Must define trigger thresholds, owner actions, dependencies, and data integrity considerations.
- `execute-cutover-provenance-report` -> `provenance-report` / Release execution provenance note | kind=Evidence | trust= | sensitivity= | validation=Must identify origin, producing system, trust assumptions, and gaps or manual overrides.

## Artifact inputs
- No explicit artifact inputs.

## Branch outcomes
- No explicit branch outcomes.

## Checklists
- `release-go-live-checklist`

## Validations
- `validation-release-authorized`

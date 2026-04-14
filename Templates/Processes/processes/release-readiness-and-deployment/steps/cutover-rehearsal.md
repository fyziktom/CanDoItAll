# Rehearse cutover and confirm watch coverage

**Process:** `release-readiness-and-deployment` / Release readiness and deployment control  
**Step key:** `cutover-rehearsal`  
**Step kind:** Review  
**Target lead hours:** 6

## Summary
Operational readiness is rehearsed, not assumed

## Notes
Exercise the cutover sequence, support handoffs, and watch roster so operators know exactly who acts on which signal during the real release.

## Contracts
- Input contract: Release readiness package, deployment sequence, monitoring plan, and on-call availability.
- Output contract: Rehearsed cutover package and watch roster with gaps called out.
- Evidence contract: Watch roster, cutover rehearsal notes, and gap log.

## Governance
- Decision rights: Change manager owns choreography; platform engineer and service owner validate live-operability.
- Exception policy: If critical watch roles are missing or unreachable, hold the release.
- Requires approval: False
- Requires decision record: True

## Dependencies
- readiness-synthesis

## Role assignments
- `change-manager` / Change manager => Responsible; required=True; fallback-order=0; rebind=Cutover choreography remains with the change manager.
- `platform-engineer` / Platform engineer => Reviewer; required=True; fallback-order=0; rebind=Platform engineer reviews operational feasibility.
- `service-owner` / Service owner => Reviewer; required=True; fallback-order=1; rebind=Service owner validates coverage and escalation.
- `release-approver` / Release approver => Reviewer; required=False; fallback-order=1; rebind=Approver may observe rehearsal for high-risk releases.

## Artifact expectations
- `cutover-rehearsal-cutover-watch-roster` -> `cutover-watch-roster` / Cutover watch roster | kind= | trust= | sensitivity= | validation=Must include actual names, contact paths, and timing, not placeholder roles only.
- `cutover-rehearsal-rollback-plan` -> `rollback-plan` / Rollback plan | kind= | trust= | sensitivity= | validation=Must define trigger thresholds, owner actions, dependencies, and data integrity considerations.

## Artifact inputs
- No explicit artifact inputs.

## Branch outcomes
- No explicit branch outcomes.

## Checklists
- `cutover-rehearsal-checklist`
- `release-go-live-checklist`

## Validations
- `validate-watch-coverage`
- `validation-rollback-ready`

## Prompts
- `prompt-cutover-command-brief`

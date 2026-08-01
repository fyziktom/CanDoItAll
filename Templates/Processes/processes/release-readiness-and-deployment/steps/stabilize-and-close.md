# Confirm stability, close watch, and capture learning

**Process:** `release-readiness-and-deployment` / Release readiness and deployment control  
**Step key:** `stabilize-and-close`  
**Step kind:** End  
**Target lead hours:** 6

## Summary
A release ends only after stability is explicit

## Notes
Confirm whether the service is stable, close the live watch, and record improvements for the next release.

## Contracts
- Input contract: Cutover outcome, telemetry trend, incident or support notes, and watch coverage observations.
- Output contract: Closed release watch with stability declaration and improvements.
- Evidence contract: Stability declaration and improvement log.

## Governance
- Decision rights: Service owner declares stability; change manager closes the change record; delivery manager captures learning.
- Exception policy: Do not declare stability without observed evidence for the agreed horizon.
- Requires approval: False
- Requires decision record: True

## Dependencies
- execute-cutover

## Role assignments
- `service-owner` / Service owner => Responsible; required=True; fallback-order=0; rebind=Stability declaration remains with service owner.
- `change-manager` / Change manager => Reviewer; required=True; fallback-order=0; rebind=Change manager closes control records.
- `delivery-manager` / Delivery manager => Reviewer; required=True; fallback-order=1; rebind=Delivery manager captures follow-up work.

## Artifact expectations
- `stabilize-and-close-retrospective-improvement-log` -> `retrospective-improvement-log` / Retrospective improvement log | kind= | trust= | sensitivity= | validation=Must identify observed problem, root cause or likely cause, owner, and follow-up expectation.
- `stabilize-and-close-release-readiness-report` -> `release-readiness-report` / Release closure note | kind=Brief | trust= | sensitivity= | validation=Must identify unresolved conditions, rollback posture, support coverage, and explicit decision recommendation.

## Artifact inputs
- No explicit artifact inputs.

## Branch outcomes
- No explicit branch outcomes.

## Checklists
- `release-go-live-checklist`

## Validations
- `validation-proof-sufficient`

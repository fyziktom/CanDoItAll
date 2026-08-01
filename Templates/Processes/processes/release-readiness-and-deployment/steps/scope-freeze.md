# Freeze release scope and deployment boundary

**Process:** `release-readiness-and-deployment` / Release readiness and deployment control  
**Step key:** `scope-freeze`  
**Step kind:** Start  
**Target lead hours:** 4

## Summary
Make the release target explicit

## Notes
Capture the exact change set, environment boundary, rollout strategy, and excluded items before readiness claims start.

## Contracts
- Input contract: Candidate release contents, change logs, linked work items, and deployment boundary.
- Output contract: Frozen release scope with explicit inclusions and exclusions.
- Evidence contract: Scope freeze note and environment targeting summary.

## Governance
- Decision rights: Change manager controls the frozen boundary; delivery manager reviews business commitment impact.
- Exception policy: No readiness claim is valid if scope can still silently drift.
- Requires approval: False
- Requires decision record: False

## Dependencies
- No explicit predecessor.

## Role assignments
- `change-manager` / Change manager => Responsible; required=True; fallback-order=0; rebind=Change manager owns boundary discipline.
- `delivery-manager` / Delivery manager => Reviewer; required=True; fallback-order=0; rebind=Delivery manager reviews plan impact.
- `service-owner` / Service owner => Reviewer; required=True; fallback-order=1; rebind=Operational boundary review remains explicit.

## Artifact expectations
- `scope-freeze-intake-brief` -> `intake-brief` / Release scope freeze note | kind=Brief | trust= | sensitivity= | validation=Must identify request source, decision owner, scope boundary, and missing inputs explicitly.
- `scope-freeze-implementation-plan` -> `implementation-plan` / Deployment strategy outline | kind=Brief | trust= | sensitivity= | validation=Must identify dependency sequencing, fallback approach, and proof expectations step by step.

## Artifact inputs
- No explicit artifact inputs.

## Branch outcomes
- No explicit branch outcomes.

## Checklists
- `release-go-live-checklist`

## Validations
- `validation-proof-sufficient`

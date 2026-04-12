# Assess blast radius and rollback constraints

**Process:** `hotfix-rollout` / Emergency hotfix rollout with shard-risk governance  
**Step key:** `assess-blast-radius`  
**Step kind:** Review  
**Target lead hours:** 2

## Summary
Risk framing and bounded emergency scope

## Notes
Determine which shards, data paths, tenants, and rollback constraints bound the emergency change.

## Contracts
- Input contract: Emergency bridge log, production telemetry, and known change hypotheses.
- Output contract: Explicit blast-radius assessment with rollback constraints and bounded emergency scope.
- Evidence contract: Impact map, rollback notes, and disallowed expansion paths.

## Governance
- Decision rights: Incident commander frames the boundary; platform and database owners challenge unsupported assumptions.
- Exception policy: Pause immediately when the required fix grows into an unreviewable multi-area release.
- Requires approval: False
- Requires decision record: True

## Dependencies
- activate-emergency-bridge

## Role assignments
- `incident-commander` / Incident commander => Responsible; required=True; fallback-order=0; rebind=Emergency boundary ownership stays with command.
- `platform-engineer` / Platform engineer => Reviewer; required=True; fallback-order=0; rebind=Platform owner reviews operational fit of the proposed emergency boundary.
- `database-engineer` / Database engineer => Reviewer; required=True; fallback-order=0; rebind=Database review is mandatory where shard state or tenant data is affected.

## Artifact expectations
- `blast-radius-assessment` -> `blast-radius-assessment` / Blast-radius assessment | kind=Decision | trust=ReviewRequired | sensitivity=Confidential | validation=Must identify impacted shards or services, rollback constraints, and forbidden scope expansion paths.

## Artifact inputs
- No explicit artifact inputs.

## Branch outcomes
- No explicit branch outcomes.

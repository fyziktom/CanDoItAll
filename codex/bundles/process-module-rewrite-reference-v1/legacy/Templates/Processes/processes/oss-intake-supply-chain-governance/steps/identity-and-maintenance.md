# Verify component identity and maintenance posture

**Process:** `oss-intake-supply-chain-governance` / Open-source intake and supply-chain governance  
**Step key:** `identity-and-maintenance`  
**Step kind:** Review  
**Target lead hours:** 6

## Summary
Know exactly what you are evaluating

## Notes
Confirm upstream source, package lineage, maintainer activity, release freshness, and whether the requested artifact matches the claimed project.

## Contracts
- Input contract: Component request, repository links, package registry metadata, and maintainer signals.
- Output contract: Verified identity record and maintenance posture summary.
- Evidence contract: Identity note, upstream mapping, and maintenance-health summary.

## Governance
- Decision rights: SBOM curator verifies identity; software engineer helps interpret practical adoption implications.
- Exception policy: Do not continue if the requested package cannot be reliably tied to an upstream project and maintenance history.
- Requires approval: False
- Requires decision record: True

## Dependencies
- component-request

## Role assignments
- `sbom-curator` / SBOM curator => Responsible; required=True; fallback-order=0; rebind=Identity verification remains with the catalog steward.
- `software-engineer` / Software engineer => Reviewer; required=True; fallback-order=0; rebind=Engineering review confirms functional match.

## Artifact expectations
- `identity-and-maintenance-sbom-manifest` -> `sbom-manifest` / Component identity registration draft | kind=Deliverable | trust= | sensitivity= | validation=Must include component identity, version, and completeness caveats for decision-grade use.
- `identity-and-maintenance-provenance-report` -> `provenance-report` / Upstream identity and maintenance summary | kind=Evidence | trust= | sensitivity= | validation=Must identify origin, producing system, trust assumptions, and gaps or manual overrides.

## Artifact inputs
- No explicit artifact inputs.

## Branch outcomes
- No explicit branch outcomes.

## Checklists
- `component-identity-checklist`
- `oss-intake-checklist`

## Validations
- `validation-sbom-ready`

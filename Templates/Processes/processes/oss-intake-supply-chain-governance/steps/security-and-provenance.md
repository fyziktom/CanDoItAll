# Review security posture and provenance trust

**Process:** `oss-intake-supply-chain-governance` / Open-source intake and supply-chain governance  
**Step key:** `security-and-provenance`  
**Step kind:** Review  
**Target lead hours:** 8

## Summary
Supply-chain trust must match deployment risk

## Notes
Assess known vulnerabilities, update responsiveness, maintainer trust, signed provenance, and build-chain confidence relative to intended use.

## Contracts
- Input contract: Identity record, license position, vulnerability data, provenance statements, and deployment context.
- Output contract: Supply-chain risk position with compensating controls or rejection rationale.
- Evidence contract: Security/provenance review note and risk position.

## Governance
- Decision rights: Security reviewer may reject a component even if the license is acceptable.
- Exception policy: Do not approve runtime-critical components on weak provenance without explicit compensating controls.
- Requires approval: False
- Requires decision record: False

## Dependencies
- license-and-obligations

## Role assignments
- `security-reviewer` / Security reviewer => Responsible; required=True; fallback-order=0; rebind=Security and provenance review remain explicit.
- `service-owner` / Service owner => Reviewer; required=True; fallback-order=0; rebind=Operational owner reviews deployment risk.
- `sbom-curator` / SBOM curator => Reviewer; required=True; fallback-order=1; rebind=Catalog steward verifies metadata completeness.

## Artifact expectations
- `security-and-provenance-security-review-note` -> `security-review-note` / Security review note | kind= | trust= | sensitivity= | validation=Must state reviewed scope, identified risks, required controls, and explicit approval or exception status.
- `security-and-provenance-provenance-report` -> `provenance-report` / Provenance report | kind= | trust= | sensitivity= | validation=Must identify origin, producing system, trust assumptions, and gaps or manual overrides.

## Artifact inputs
- No explicit artifact inputs.

## Branch outcomes
- No explicit branch outcomes.

## Checklists
- `security-review-checklist`
- `oss-intake-checklist`

## Validations
- `validation-security-clear`

## Prompts
- `prompt-oss-evaluation`

# Analyze license obligations and redistribution constraints

**Process:** `oss-intake-supply-chain-governance` / Open-source intake and supply-chain governance  
**Step key:** `license-and-obligations`  
**Step kind:** Review  
**Target lead hours:** 6

## Summary
Adoption must preserve downstream compliance

## Notes
Translate the component license, notice requirements, source obligations, export constraints, and policy exceptions into explicit obligations.

## Contracts
- Input contract: Verified identity, license texts, notice files, policy guidance, and intended distribution model.
- Output contract: Obligation matrix and legal position for adoption.
- Evidence contract: License-obligation matrix and unresolved legal questions.

## Governance
- Decision rights: License counsel owns legal interpretation; compliance steward reviews policy fit.
- Exception policy: No approval if obligations cannot be explained to downstream teams.
- Requires approval: False
- Requires decision record: False

## Dependencies
- identity-and-maintenance

## Role assignments
- `license-counsel` / License counsel => Responsible; required=True; fallback-order=0; rebind=Legal interpretation remains with qualified counsel.
- `compliance-steward` / Compliance steward => Reviewer; required=True; fallback-order=0; rebind=Compliance stewardship remains explicit.

## Artifact expectations
- `license-and-obligations-license-obligation-matrix` -> `license-obligation-matrix` / License obligation matrix | kind= | trust= | sensitivity= | validation=Must identify which obligation applies to which component and usage mode.
- `license-and-obligations-sbom-manifest` -> `sbom-manifest` / Component license metadata entry | kind=Deliverable | trust= | sensitivity= | validation=Must include component identity, version, and completeness caveats for decision-grade use.

## Artifact inputs
- No explicit artifact inputs.

## Branch outcomes
- No explicit branch outcomes.

## Checklists
- `oss-intake-checklist`

## Validations
- `validate-license-obligation-coverage`

## Prompts
- `prompt-component-approval-note`

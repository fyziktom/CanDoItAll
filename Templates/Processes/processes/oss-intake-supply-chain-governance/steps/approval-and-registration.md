# Approve, reject, or constrain adoption

**Process:** `oss-intake-supply-chain-governance` / Open-source intake and supply-chain governance  
**Step key:** `approval-and-registration`  
**Step kind:** Approval  
**Target lead hours:** 3

## Summary
Approval carries obligations forward

## Notes
Make the adoption decision and publish the resulting obligations, owner, approved version boundary, and review cadence.

## Contracts
- Input contract: License position, security/provenance review, and intended use context.
- Output contract: Approval decision with obligations, owner, and review cadence.
- Evidence contract: Approval record and final SBOM registration.

## Governance
- Decision rights: Compliance steward approves policy fit; service owner accepts operational ownership.
- Exception policy: No approval without named ongoing owner and explicit version boundary.
- Requires approval: True
- Requires decision record: True

## Dependencies
- security-and-provenance

## Role assignments
- `compliance-steward` / Compliance steward => Approver; required=True; fallback-order=0; rebind=Approval stays with compliance steward.
- `service-owner` / Service owner => Reviewer; required=True; fallback-order=0; rebind=Owner acceptance remains explicit.
- `license-counsel` / License counsel => Reviewer; required=True; fallback-order=1; rebind=Legal review is retained in the record.

## Artifact expectations
- `approval-and-registration-sbom-manifest` -> `sbom-manifest` / SBOM manifest | kind= | trust= | sensitivity= | validation=Must include component identity, version, and completeness caveats for decision-grade use.
- `approval-and-registration-release-readiness-report` -> `release-readiness-report` / Component approval note | kind=Decision | trust=HumanApproved | sensitivity= | validation=Must identify unresolved conditions, rollback posture, support coverage, and explicit decision recommendation.

## Artifact inputs
- No explicit artifact inputs.

## Branch outcomes
- `approved` / Approved — Component may be adopted under recorded conditions.
- `rejected` / Rejected — Component is not approved for the intended use.

## Checklists
- `oss-intake-checklist`

## Validations
- `validation-sbom-ready`
- `validate-license-obligation-coverage`

## Prompts
- `prompt-component-approval-note`

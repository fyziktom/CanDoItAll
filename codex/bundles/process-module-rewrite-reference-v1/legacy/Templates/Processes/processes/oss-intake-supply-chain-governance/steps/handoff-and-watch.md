# Hand off obligations and establish re-review triggers

**Process:** `oss-intake-supply-chain-governance` / Open-source intake and supply-chain governance  
**Step key:** `handoff-and-watch`  
**Step kind:** End  
**Target lead hours:** 4

## Summary
Approval must become operational practice

## Notes
Publish obligation guidance, notify consuming teams, and define the triggers that force re-review when component posture changes.

## Contracts
- Input contract: Approval record, SBOM entry, obligations matrix, and owner contacts.
- Output contract: Downstream handoff package and re-review trigger list.
- Evidence contract: Obligation handoff note and re-review log.

## Governance
- Decision rights: SBOM curator owns metadata dissemination; service owner owns runtime review cadence.
- Exception policy: If obligations cannot be passed to consuming teams, the adoption is not operationally complete.
- Requires approval: False
- Requires decision record: False

## Dependencies
- approval-and-registration

## Role assignments
- `sbom-curator` / SBOM curator => Responsible; required=True; fallback-order=0; rebind=Catalog dissemination remains with the SBOM curator.
- `service-owner` / Service owner => Reviewer; required=True; fallback-order=0; rebind=Operational owner confirms ongoing watch.
- `software-engineer` / Software engineer => Reviewer; required=True; fallback-order=1; rebind=Consuming engineer confirms implementation fit.

## Artifact expectations
- `handoff-and-watch-license-obligation-matrix` -> `license-obligation-matrix` / Published obligation handoff | kind=Brief | trust= | sensitivity= | validation=Must identify which obligation applies to which component and usage mode.
- `handoff-and-watch-retrospective-improvement-log` -> `retrospective-improvement-log` / Component re-review trigger log | kind=Brief | trust= | sensitivity= | validation=Must identify observed problem, root cause or likely cause, owner, and follow-up expectation.

## Artifact inputs
- No explicit artifact inputs.

## Branch outcomes
- No explicit branch outcomes.

## Checklists
- `oss-intake-checklist`

## Validations
- `validation-sbom-ready`

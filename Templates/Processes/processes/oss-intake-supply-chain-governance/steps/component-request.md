# Register component intake request

**Process:** `oss-intake-supply-chain-governance` / Open-source intake and supply-chain governance  
**Step key:** `component-request`  
**Step kind:** Start  
**Target lead hours:** 3

## Summary
Make the request explicit before evaluation

## Notes
Capture why the component is wanted, where it will be used, what alternatives exist, and who will own it after approval.

## Contracts
- Input contract: Package name or component identifier, intended use, business context, and requesting team.
- Output contract: Typed component intake request with accountable owner.
- Evidence contract: Intake brief and ownership statement.

## Governance
- Decision rights: Software engineer requests; service owner confirms operational ownership exists.
- Exception policy: Reject anonymous or ownerless component requests.
- Requires approval: False
- Requires decision record: False

## Dependencies
- No explicit predecessor.

## Role assignments
- `software-engineer` / Software engineer => Responsible; required=True; fallback-order=0; rebind=Requester remains accountable for supplying context.
- `service-owner` / Service owner => Reviewer; required=True; fallback-order=0; rebind=Operational owner review is required.

## Artifact expectations
- `component-request-intake-brief` -> `intake-brief` / Component intake request | kind=Brief | trust= | sensitivity= | validation=Must identify request source, decision owner, scope boundary, and missing inputs explicitly.

## Artifact inputs
- No explicit artifact inputs.

## Branch outcomes
- No explicit branch outcomes.

## Checklists
- `oss-intake-checklist`
- `component-identity-checklist`

## Validations
- `validation-sbom-ready`

## Prompts
- `prompt-oss-evaluation`

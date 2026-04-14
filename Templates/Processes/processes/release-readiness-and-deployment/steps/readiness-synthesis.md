# Synthesize readiness evidence and open risks

**Process:** `release-readiness-and-deployment` / Release readiness and deployment control  
**Step key:** `readiness-synthesis`  
**Step kind:** Work  
**Target lead hours:** 8

## Summary
Gather all proof into one release lens

## Notes
Combine implementation state, QA proof, security findings, operational checks, and known open items into one decision-ready package.

## Contracts
- Input contract: Frozen release scope, QA runs, security notes, environment status, and known exceptions.
- Output contract: Release readiness package with explicit open-risk inventory.
- Evidence contract: Release readiness report and linked evidence references.

## Governance
- Decision rights: Delivery manager and QA lead own evidence synthesis; security reviewer and service owner contribute risk posture.
- Exception policy: Do not summarize evidence that you have not actually inspected or linked.
- Requires approval: False
- Requires decision record: False

## Dependencies
- scope-freeze

## Role assignments
- `delivery-manager` / Delivery manager => Responsible; required=True; fallback-order=0; rebind=Delivery manager remains accountable for the integrated view.
- `qa-lead` / QA lead => Reviewer; required=True; fallback-order=0; rebind=QA review confirms objective proof coverage.
- `security-reviewer` / Security reviewer => Reviewer; required=True; fallback-order=1; rebind=Security review remains explicit.
- `service-owner` / Service owner => Reviewer; required=True; fallback-order=1; rebind=Service owner reviews operational readiness.

## Artifact expectations
- `readiness-synthesis-release-readiness-report` -> `release-readiness-report` / Release readiness report | kind= | trust= | sensitivity= | validation=Must identify unresolved conditions, rollback posture, support coverage, and explicit decision recommendation.
- `readiness-synthesis-test-evidence-pack` -> `test-evidence-pack` / Test evidence pack | kind= | trust= | sensitivity= | validation=Must contain reproducible evidence sources, coverage statement, open defects, and residual risk summary.

## Artifact inputs
- No explicit artifact inputs.

## Branch outcomes
- No explicit branch outcomes.

## Checklists
- `qa-evidence-checklist`
- `release-go-live-checklist`

## Validations
- `validation-proof-sufficient`

## Prompts
- `prompt-release-decision`
- `prompt-qa-test-design`

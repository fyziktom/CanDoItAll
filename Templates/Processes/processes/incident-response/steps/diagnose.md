# Diagnose probable cause

**Process:** `incident-response` / Incident response and escalation  
**Step key:** `diagnose`  
**Step kind:** Work  
**Target lead hours:** 6

## Summary
Evidence-led technical assessment

## Notes
Develop a diagnosis hypothesis and proposed action from logs, traces, and responder findings.

## Contracts
- Input contract: Initial severity and evidence.
- Output contract: Diagnosis hypothesis and proposed action.
- Evidence contract: Logs, traces, or structured findings.

## Governance
- Decision rights: Resolver proposes action; approver decides emergency changes.
- Exception policy: Do not present a mitigation as safe until evidence and rollback framing exist.
- Requires approval: False
- Requires decision record: True

## Dependencies
- respond

## Role assignments
- `resolver` / Resolver => Responsible; required=True; fallback-order=0; rebind=Resolver role may be rebound to another capable executor.
- `triage-lead` / Triage lead => Reviewer; required=True; fallback-order=0; rebind=Triage lead validates communication impact before escalation.

## Artifact expectations
- `diagnosis-evidence-pack` -> `diagnosis-evidence-pack` / Diagnosis evidence pack | kind=Evidence | trust=ReviewRequired | sensitivity=Internal | validation=Evidence must capture source and review status.

## Artifact inputs
- From step `respond` expectation `incident-triage-note`

## Branch outcomes
- No explicit branch outcomes.

## Checklists
- `diagnosis-discipline-checklist`

## Prompts
- `prompt-mitigation-options`

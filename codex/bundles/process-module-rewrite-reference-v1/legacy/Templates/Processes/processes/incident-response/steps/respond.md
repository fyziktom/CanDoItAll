# Acknowledge and classify incident

**Process:** `incident-response` / Incident response and escalation  
**Step key:** `respond`  
**Step kind:** Start  
**Target lead hours:** 1

## Summary
First response and safe-refusal posture

## Notes
Capture the incoming alert, classify probable severity, and establish the initial response owner.

## Contracts
- Input contract: Inbound alert, customer impact, and available evidence.
- Output contract: Initial severity and response owner.
- Evidence contract: Timestamped acknowledgement and incident notes.

## Governance
- Decision rights: Triage lead may safely refuse malformed or irrelevant incidents.
- Exception policy: Do not let an ambiguous incoming signal bypass typed acknowledgement and classification.
- Requires approval: False
- Requires decision record: False

## Dependencies
- No explicit predecessor.

## Role assignments
- `triage-lead` / Triage lead => Responsible; required=True; fallback-order=0; rebind=Another triage lead may take over without invalidating the process.

## Artifact expectations
- `incident-triage-note` -> `incident-triage-note` / Incident triage note | kind=Decision | trust=ReviewRequired | sensitivity=Internal | validation=Must capture triggering signal, user impact, severity posture, responders, and first actions.

## Artifact inputs
- No explicit artifact inputs.

## Branch outcomes
- No explicit branch outcomes.

## Checklists
- `incident-triage-discipline-checklist`

## Validations
- `validation-incident-triage-explicit`

## Prompts
- `prompt-incident-triage-brief`

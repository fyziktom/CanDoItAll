# Incident response and escalation

**Key:** `incident-response`  
**Criticality:** High  
**Autonomy level:** Assisted  
**Operating mode:** Emergency  
**Customer name:** Managed services  
**Owner name:** Response leadership

## Summary
Coordinate first response, diagnosis, escalation, and customer communication with explicit safe-refusal paths, artifact inputs, and emergency-governance notes.

## Value statement
Shorten blocked time and preserve trust under ambiguity.

## Interface contract summary
Customer-facing response depends on reliable diagnosis and explicit decision rights.

## Governance notes
Critical escalations require approval notes and trust-aware evidence handling.

## Architecture and constitution rules
- Governance policy: Emergency paths stay bounded by approval, journaling, and evidence controls.
- Constitution rule: Policy decisions stay explicit and reviewable.

## Operating and simulation notes
- Operating mode summary: Emergency operating mode is explicitly bounded by governance rules.
- Simulation readiness: Seed pack models refusal, blocking, and artifact trust scenarios.

## Source frameworks
- nist-ssdf
- owasp-samm
- openchain
- spdx
- slsa

## Process metrics
- Median time from acknowledgement to diagnosis evidence pack.
- Approvals executed with explicit mitigation rationale and rollback framing.
- Percentage of incidents triaged with explicit severity and responders.
- Rate of false-positive incidents safely refused without follow-on churn.
- Post-incident findings caused by weak diagnosis evidence or approval discipline.

## Process risks
- Triage proceeding with vague severity or impact framing.
- Diagnosis notes losing source provenance.
- Emergency changes executed without explicit approval.
- Customer-impact communication drifting away from technical facts.

## Tailoring rules
- Low-signal incidents may stop after respond with an explicit safe refusal or downgrade.
- Major incidents may branch into a richer command-and-communications scenario after escalation.
- If mitigation changes production state, approval remains mandatory even when the same person also owns technical response.

## Role usages
- `triage-lead` / **Triage lead** — Turn an incoming operational signal into a structured incident response posture with explicit severity, owners, and first actions.
- `resolver` / **Resolver** — Investigate the technical fault path, propose mitigation, and execute or coordinate the change needed to restore service.
- `approver` / **Approver** — Make the accountable go / no-go decision when an incident response path requires explicit approval beyond responder discretion.

## Steps
### 1. Acknowledge and classify incident (`respond`)
- Step kind: Start
- Depends on: None
- Inputs: Inbound alert, customer impact, and available evidence.
- Outputs: Initial severity and response owner.
- Evidence: Timestamped acknowledgement and incident notes.
- Decision rights: Triage lead may safely refuse malformed or irrelevant incidents.
- Exception policy: Do not let an ambiguous incoming signal bypass typed acknowledgement and classification.
- Artifact expectations:
  - `incident-triage-note` => `incident-triage-note` / Incident triage note
- Checklists: incident-triage-discipline-checklist
- Validations: validation-incident-triage-explicit
- Prompts: prompt-incident-triage-brief

### 2. Diagnose probable cause (`diagnose`)
- Step kind: Work
- Depends on: respond
- Inputs: Initial severity and evidence.
- Outputs: Diagnosis hypothesis and proposed action.
- Evidence: Logs, traces, or structured findings.
- Decision rights: Resolver proposes action; approver decides emergency changes.
- Exception policy: Do not present a mitigation as safe until evidence and rollback framing exist.
- Artifact expectations:
  - `diagnosis-evidence-pack` => `diagnosis-evidence-pack` / Diagnosis evidence pack
- Artifact inputs:
  - from `respond` expectation `incident-triage-note`
- Checklists: diagnosis-discipline-checklist
- Prompts: prompt-mitigation-options

### 3. Approve escalation path (`escalate`)
- Step kind: Approval
- Depends on: diagnose
- Inputs: Diagnosis and proposed change path.
- Outputs: Approved, blocked, or refused escalation.
- Evidence: Approval record and rationale.
- Decision rights: Approver owns the escalation gate.
- Exception policy: Do not execute high-risk mitigation without explicit approval and rollback framing.
- Artifact expectations:
  - `mitigation-approval-record` => `mitigation-approval-record` / Escalation approval record
- Artifact inputs:
  - from `diagnose` expectation `diagnosis-evidence-pack`
- Validations: validate-mitigation-approved


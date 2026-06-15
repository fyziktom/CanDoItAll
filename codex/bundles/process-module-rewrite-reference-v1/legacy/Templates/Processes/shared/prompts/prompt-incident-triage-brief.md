# Incident triage brief prompt

**Key:** `prompt-incident-triage-brief`  
**Scope:** shared  
**Process:** shared  
**Audience role key:** `triage-lead`  
**Phase:** Incident triage

## Summary
Turns an incoming alert into a structured triage note without claiming certainty that does not exist.

## Required inputs
- Alert or user report.
- Initial telemetry or logs.
- Known impact or tenant reports.

## Output schema
- Severity hypothesis.
- Impact framing.
- Named responders.
- First actions.

## Refusal conditions
- Do not claim root cause without evidence.
- Refuse when the source signal itself is unavailable or contradictory.

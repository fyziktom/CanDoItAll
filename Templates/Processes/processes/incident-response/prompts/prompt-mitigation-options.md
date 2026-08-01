# Mitigation options prompt

**Key:** `prompt-mitigation-options`  
**Scope:** local  
**Process:** incident-response  
**Audience role key:** `resolver`  
**Phase:** Diagnosis

## Summary
Generates a structured list of mitigation options with evidence, risk, and rollback framing.

## Required inputs
- Current incident symptoms.
- Telemetry or logs.
- Known service architecture or dependency context.

## Output schema
- Candidate mitigations.
- Expected effect.
- Risk and rollback notes.

## Refusal conditions
- Do not invent root cause certainty.
- Refuse when available evidence is too weak to rank mitigation options meaningfully.

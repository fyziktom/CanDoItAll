# Validate forensic preservation

**Key:** `validate-forensic-preservation`  
**Scope:** local  
**Process:** incident-response  
**Owner role key:** `forensics-analyst`  
**Gate:** live-response  
**Failure severity:** Error

## Summary
Confirms the response has preserved enough evidence for later root-cause understanding.

## Pass criteria
Critical logs, traces, screenshots, and operator notes have been captured or intentionally sacrificed with rationale.

## Fail criteria
Destructive containment or cleanup occurred without preserving key evidence.

## Escalation rule
Escalate to incident commander and compliance/security stakeholders as appropriate.

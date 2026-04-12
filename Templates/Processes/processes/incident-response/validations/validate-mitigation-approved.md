# Mitigation approved

**Key:** `validate-mitigation-approved`  
**Scope:** local  
**Process:** incident-response  
**Owner role key:** `approver`  
**Gate:** Incident approval  
**Failure severity:** Error

## Summary
Prevents high-risk incident mitigation from proceeding without explicit approval when required.

## Pass criteria
Risky mitigation steps have an approval record naming owner, conditions, and rollback trigger.

## Fail criteria
High-risk mitigation would proceed without explicit approval or rollback framing.

## Escalation rule
Pause the mitigation and escalate to the approver.

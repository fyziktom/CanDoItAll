# Validate rollback readiness

**Key:** `validation-rollback-ready`  
**Scope:** shared  
**Process:** shared  
**Owner role key:** `platform-engineer`  
**Gate:** release  
**Failure severity:** Error

## Summary
Confirms the change can be contained, stopped, or reversed according to the declared risk profile.

## Pass criteria
Rollback triggers, owner actions, tooling path, and data implications are explicit and realistic.

## Fail criteria
Rollback is assumed but not described, depends on unavailable people, or ignores data integrity consequences.

## Escalation rule
Escalate to platform and release owners before go-live.

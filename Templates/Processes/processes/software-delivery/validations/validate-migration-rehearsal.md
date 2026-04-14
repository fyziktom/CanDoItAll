# Validate migration rehearsal quality

**Key:** `validate-migration-rehearsal`  
**Scope:** local  
**Process:** software-delivery  
**Owner role key:** `data-migration-owner`  
**Gate:** pre-release  
**Failure severity:** Error

## Summary
Confirms risky data or schema migration behavior was rehearsed credibly before production exposure.

## Pass criteria
Rehearsal covered realistic sequencing, timing, and rollback observations for the declared risk slice.

## Fail criteria
Migration assumptions remain theoretical or rehearsal omitted critical risk characteristics.

## Escalation rule
Escalate to release approver and service owner; block production rollout until addressed.

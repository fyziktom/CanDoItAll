# Validate domain-owner coverage

**Key:** `validate-domain-owner-coverage`  
**Scope:** local  
**Process:** architecture-decision-governance  
**Owner role key:** `domain-owner`  
**Gate:** decision-readiness  
**Failure severity:** Error

## Summary
Confirms the impacted domain owners have been consulted before a cross-domain decision is locked in.

## Pass criteria
Every materially affected domain has either participated or been explicitly waived with rationale.

## Fail criteria
Architecture decision is proceeding with hidden affected domains or ownership blind spots.

## Escalation rule
Escalate to architecture facilitator and chief architect before approval.

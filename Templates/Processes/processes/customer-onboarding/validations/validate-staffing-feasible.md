# Staffing feasible

**Key:** `validate-staffing-feasible`  
**Scope:** local  
**Process:** customer-onboarding  
**Owner role key:** `staffing-manager`  
**Gate:** Staffing review  
**Failure severity:** Error

## Summary
Blocks kickoff progression when staffing gaps or specialist constraints remain implicit.

## Pass criteria
Named staffing coverage exists for the kickoff and first execution phase, or any accepted gaps are explicitly owned.

## Fail criteria
Kickoff would proceed with unnamed specialists, unknown allocation, or ignored constraints.

## Escalation rule
Stop kickoff progression and escalate the staffing gap explicitly.

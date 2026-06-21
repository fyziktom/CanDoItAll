# Validate release authorization

**Key:** `validation-release-authorized`  
**Scope:** shared  
**Process:** shared  
**Owner role key:** `release-approver`  
**Gate:** release  
**Failure severity:** Error

## Summary
Confirms an authorized owner explicitly accepted the release evidence and residual risk.

## Pass criteria
Go/no-go owner is identified, evidence reviewed, rollback understood, and decision recorded.

## Fail criteria
No accountable approver exists, decision rationale is absent, or unresolved conditions remain unowned.

## Escalation rule
Escalate to the release governance authority and hold deployment.

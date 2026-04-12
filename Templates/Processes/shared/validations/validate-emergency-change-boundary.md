# Emergency change boundary explicit

**Key:** `validate-emergency-change-boundary`  
**Scope:** shared  
**Process:** shared  
**Owner role key:** `release-approver`  
**Gate:** Emergency approval  
**Failure severity:** Error

## Summary
Blocks emergency approval when the hotfix scope has expanded beyond the bounded urgent fix.

## Pass criteria
The emergency change remains tightly bounded, reversible, and justified by the incident context.

## Fail criteria
The hotfix includes opportunistic changes, unclear rollback conditions, or unbounded blast radius.

## Escalation rule
Reject the emergency window until the package is reduced or re-scoped.

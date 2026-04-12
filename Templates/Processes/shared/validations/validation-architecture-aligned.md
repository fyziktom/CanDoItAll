# Validate architecture alignment

**Key:** `validation-architecture-aligned`  
**Scope:** shared  
**Process:** shared  
**Owner role key:** `solution-architect`  
**Gate:** architecture  
**Failure severity:** Error

## Summary
Confirms the chosen implementation path stays within agreed design guardrails or has an approved ADR.

## Pass criteria
Affected boundaries, risks, and irreversible choices are documented and either accepted as low risk or approved via ADR.

## Fail criteria
Implementation relies on undocumented boundary changes, hidden coupling, or unresolved architectural conflict.

## Escalation rule
Escalate to architecture governance before implementation passes the irreversible-threshold step.

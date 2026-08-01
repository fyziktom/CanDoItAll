# Validate security clearance

**Key:** `validation-security-clear`  
**Scope:** shared  
**Process:** shared  
**Owner role key:** `security-reviewer`  
**Gate:** security  
**Failure severity:** Error

## Summary
Confirms security-sensitive change scope is either approved or explicitly bounded by a documented exception.

## Pass criteria
Required security review occurred, open risks are accepted with conditions, and no unresolved high-severity blocker remains hidden.

## Fail criteria
Security review is missing, incomplete, or reveals unresolved unacceptable risk.

## Escalation rule
Escalate to release approver and accountable governance owner; freeze release progression.

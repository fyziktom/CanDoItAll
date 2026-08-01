# Security review checklist

**Key:** `security-review-checklist`  
**Scope:** shared  
**Process:** shared  
**Owner role key:** `security-reviewer`  
**Phase:** security

## Summary
Security gate checklist for changes touching trust boundaries, dependencies, data sensitivity, or AI safety.

## Entry criteria
A change or component requires security-sensitive review.

## Exit criteria
Security posture is explicit: approved, conditionally approved, blocked, or accepted under a documented exception.

## Checks
- Sensitive data, trust boundaries, and privileged flows were identified.
- Dependency or supply-chain concerns were reviewed for impacted components.
- Required threat or misuse analysis was completed proportionally to risk.
- Compensating controls are defined when full remediation is not practical.
- Approval conditions, expiry, or follow-up actions are explicit.
- Escalation occurred if unresolved risk exceeded delegated tolerance.

## Evidence expectations
- Security review note or exception record.
- Supporting threat or misuse analysis artifacts.

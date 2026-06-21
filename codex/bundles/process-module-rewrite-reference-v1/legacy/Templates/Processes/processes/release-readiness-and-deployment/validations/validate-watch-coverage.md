# Validate cutover watch coverage

**Key:** `validate-watch-coverage`  
**Scope:** local  
**Process:** release-readiness-and-deployment  
**Owner role key:** `change-manager`  
**Gate:** deployment  
**Failure severity:** Error

## Summary
Confirms the first live risk window has real staffed monitoring and decision coverage.

## Pass criteria
Named watchers, approvers, and escalation paths cover the critical deployment horizon.

## Fail criteria
Deployment depends on unstated availability or placeholder coverage.

## Escalation rule
Escalate to release approver and service owner before go-live.

# Validate emergency change boundary

**Key:** `validate-emergency-change-boundary`  
**Scope:** local  
**Process:** hotfix-rollout  
**Owner role key:** `incident-commander`  
**Gate:** hotfix-window  
**Failure severity:** Error

## Summary
Confirms the hotfix remains narrowly scoped to the incident need and has not become an uncontrolled bundled release.

## Pass criteria
Hotfix scope is minimal, incident-linked, and clearly separable from unrelated backlog or convenience changes.

## Fail criteria
Emergency path includes unrelated opportunistic changes or unbounded side effects.

## Escalation rule
Escalate immediately and reset the hotfix scope before deployment.

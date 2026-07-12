# Rollback trigger sheet

**Key:** `rollback-trigger-sheet`  
**Scope:** local  
**Process:** hotfix-rollout  
**Artifact kind:** Decision  
**Owner role key:** `database-engineer`  
**Default trust requirement:** ReviewRequired  
**Default sensitivity level:** Restricted  
**Default retention days:** 730

## Summary
Explicit rollback trigger sheet for emergency rollout that defines telemetry thresholds, shard-risk triggers, and stop conditions.

## Allowed future usage
Reusable only during the hotfix window, post-incident review, and authorized audit of the event.

## Validation requirement
Must define concrete telemetry or operational signals, not vague discomfort language.

# Validate AI delegation boundary

**Key:** `validate-delegation-boundary`  
**Scope:** local  
**Process:** ai-assisted-change-delivery  
**Owner role key:** `model-risk-approver`  
**Gate:** ai-delegation  
**Failure severity:** Error

## Summary
Confirms the workflow does not delegate materially sensitive judgment beyond the approved boundary.

## Pass criteria
Delegated tasks match the approved use boundary and required human checkpoints are intact.

## Fail criteria
Workflow silently extends AI autonomy beyond the reviewed scope or removes required human oversight.

## Escalation rule
Escalate immediately and suspend AI-assisted execution for the affected step.

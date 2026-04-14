# Review router safe

**Key:** `validate-review-router-safe`  
**Scope:** local  
**Process:** branching-code-review  
**Owner role key:** `review-lead`  
**Gate:** Review router  
**Failure severity:** Error

## Summary
Prevents the router from proceeding when route selection is implicit, contradictory, or unsupported by evidence.

## Pass criteria
One modeled lane is selected explicitly and the choice is supported by the review packet.

## Fail criteria
No explicit lane, multiple contradictory lanes, or insufficient evidence for the chosen lane.

## Escalation rule
Route to repairs or workflow-failure escalation and require an explicit correction.

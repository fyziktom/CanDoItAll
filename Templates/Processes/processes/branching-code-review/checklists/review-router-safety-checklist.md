# Review router safety checklist

**Key:** `review-router-safety-checklist`  
**Scope:** local  
**Process:** branching-code-review  
**Owner role key:** `review-lead`  
**Phase:** Review router

## Summary
Confirms the router chooses an explicit governed lane and preserves failure routing.

## Entry criteria
A review packet is ready and the next lane must be chosen.

## Exit criteria
Exactly one explicit governed lane or error route is selected with rationale.

## Checks
- The route is chosen explicitly from the modeled branch outcomes.
- The selected lane matches the evidence and risk posture.
- Unclassified outcomes are normalized instead of left implicit.
- Malformed states are sent to the error lane rather than guessed.

## Evidence expectations
- Review routing decision record.
- Normalization note when needed.
- Workflow failure record for error routes.

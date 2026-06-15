# Review normalization prompt

**Key:** `prompt-review-normalization`  
**Scope:** local  
**Process:** branching-code-review  
**Audience role key:** `review-lead`  
**Phase:** Review normalization

## Summary
Produces a structured normalization note when a review state must fall back to the default governed lane.

## Required inputs
- Observed ambiguous review state.
- Available evidence and reviewer comments.
- Allowed default lane.

## Output schema
- Normalized lane.
- Reason the state was considered ambiguous.
- Follow-up action or owner.

## Refusal conditions
- Refuse if the ambiguous state actually represents an error that should escalate.
- Do not invent reviewer intent not present in the evidence.

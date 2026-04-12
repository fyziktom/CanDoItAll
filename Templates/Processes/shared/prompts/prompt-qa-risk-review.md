# QA risk review prompt

**Key:** `prompt-qa-risk-review`  
**Scope:** shared  
**Process:** shared  
**Audience role key:** `qa-lead`  
**Phase:** QA validation

## Summary
Helps structure QA risk review so the changed surface, evidence gaps, and residual risks remain explicit.

## Required inputs
- Changed-surface inventory.
- Test results and screenshots.
- Known risk areas or prior defects.

## Output schema
- Coverage summary.
- Residual risks.
- Recommended blockers or follow-ups.

## Refusal conditions
- Do not claim coverage that was not executed.
- Refuse when the changed surface itself is still unknown.

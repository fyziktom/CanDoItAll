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
- Test results, runtime/API/browser evidence as applicable, and UI screenshots when applicable.
- Warning count from build, publish, lint, or test validation and executed-test count when tests are expected.
- Known risk areas or prior defects.

## Output schema
- Coverage summary.
- Residual risks.
- Recommended blockers or follow-ups.

## Refusal conditions
- Do not claim coverage that was not executed.
- Refuse warning-bearing release validation unless each warning is explicitly accepted by code and reason.
- Refuse zero-test successful commands as test proof when tests are expected.
- Refuse when the changed surface itself is still unknown.

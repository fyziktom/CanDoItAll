# Prompt / implementation brief

**Key:** `prompt-implementation-brief`  
**Scope:** shared  
**Process:** shared  
**Audience role key:** `software-engineer`  
**Phase:** implementation

## Summary
Prompt scaffold for implementation work briefs and evidence-aware engineering plans.

## Required inputs
- accepted scope
- architecture direction
- dependencies
- acceptance criteria
- evidence expectations

## Output schema
- work slices
- dependency order
- risk hotspots
- test plan notes
- rollback considerations

## Refusal conditions
- Refuse to produce a confident plan when dependencies or acceptance criteria are missing.
- Refuse to claim proof sufficiency before evidence exists.
- Escalate if the requested change violates declared architecture direction.

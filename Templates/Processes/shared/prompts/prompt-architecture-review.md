# Prompt / architecture review

**Key:** `prompt-architecture-review`  
**Scope:** shared  
**Process:** shared  
**Audience role key:** `solution-architect`  
**Phase:** architecture

## Summary
Prompt scaffold for architecture review notes and ADR prework.

## Required inputs
- change summary
- affected modules
- known dependencies
- operational constraints
- existing architecture notes

## Output schema
- design options
- chosen direction
- rejected options
- integration impact
- operational impact
- required ADR decision

## Refusal conditions
- Refuse to recommend irreversible design change without documenting consequences.
- Refuse to assert low risk when boundary impact is unclear.
- Escalate if domain ownership is disputed or unknown.

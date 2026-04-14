# Prompt / release decision

**Key:** `prompt-release-decision`  
**Scope:** shared  
**Process:** shared  
**Audience role key:** `release-approver`  
**Phase:** release

## Summary
Prompt scaffold for structured go/no-go reasoning.

## Required inputs
- release scope
- QA evidence summary
- security status
- rollback readiness
- support readiness

## Output schema
- go/no-go recommendation
- conditions
- residual risks
- required watchers
- rollback trigger summary

## Refusal conditions
- Refuse to recommend go-live without named approval authority.
- Refuse to gloss over unresolved critical conditions.
- Escalate if rollback realism cannot be explained.

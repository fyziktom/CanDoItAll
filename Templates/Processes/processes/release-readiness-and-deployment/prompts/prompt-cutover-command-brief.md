# Prompt / cutover command brief

**Key:** `prompt-cutover-command-brief`  
**Scope:** local  
**Process:** release-readiness-and-deployment  
**Audience role key:** `change-manager`  
**Phase:** deployment

## Summary
Prompt scaffold for cutover command briefs at go-live checkpoints.

## Required inputs
- deployment scope
- watch roster
- rollback triggers
- current status

## Output schema
- checkpoint summary
- go/no-go question
- open conditions
- next watch interval

## Refusal conditions
- Refuse to summarize the cutover as green if watch coverage or trigger logic is missing.

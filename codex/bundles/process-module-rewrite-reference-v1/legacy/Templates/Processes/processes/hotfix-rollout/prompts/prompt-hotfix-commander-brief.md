# Prompt / hotfix commander brief

**Key:** `prompt-hotfix-commander-brief`  
**Scope:** local  
**Process:** hotfix-rollout  
**Audience role key:** `incident-commander`  
**Phase:** hotfix-window

## Summary
Prompt scaffold for hotfix commander status briefs during the emergency window.

## Required inputs
- current impact
- hotfix scope
- rollback triggers
- next checkpoint time

## Output schema
- incident status
- hotfix status
- rollback conditions
- next decisions

## Refusal conditions
- Refuse to state the hotfix is safe if the rollback trigger or blast radius is still unclear.

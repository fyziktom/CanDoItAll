# Hotfix commander brief prompt

**Key:** `prompt-hotfix-commander-brief`  
**Scope:** shared  
**Process:** shared  
**Audience role key:** `incident-commander`  
**Phase:** Emergency bridge

## Summary
Builds a concise emergency commander brief that frames the issue, blast radius, current mitigation, and approval asks.

## Required inputs
- Emergency bridge notes.
- Blast radius assessment.
- Proposed hotfix package.

## Output schema
- Current incident summary.
- Approval ask.
- Rollback and watch notes.

## Refusal conditions
- Do not present unverified diagnosis as confirmed fact.
- Refuse if blast radius or rollback notes are missing.

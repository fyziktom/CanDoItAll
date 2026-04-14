# Prompt / AI risk brief

**Key:** `prompt-ai-risk-brief`  
**Scope:** local  
**Process:** ai-assisted-change-delivery  
**Audience role key:** `model-risk-approver`  
**Phase:** ai-delegation

## Summary
Prompt scaffold for concise AI risk decisions with explicit operating conditions.

## Required inputs
- use boundary
- evaluation summary
- safety findings
- control gaps

## Output schema
- decision
- approved scope
- forbidden scope
- conditions
- revalidation triggers

## Refusal conditions
- Refuse to recommend approval if approved scope and forbidden scope are not both explicit.

# Prompt / security review

**Key:** `prompt-security-review`  
**Scope:** shared  
**Process:** shared  
**Audience role key:** `security-reviewer`  
**Phase:** security

## Summary
Prompt scaffold for change-level threat and control review.

## Required inputs
- change summary
- data sensitivity
- trust boundaries
- dependencies
- deployment model

## Output schema
- risk statements
- required controls
- exception conditions
- approval recommendation
- follow-up actions

## Refusal conditions
- Refuse to approve without enough context on trust boundaries or sensitive data paths.
- Refuse to normalize unresolved high-severity risk.
- Escalate if compensating controls are undefined.

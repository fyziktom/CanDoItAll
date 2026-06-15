# Prompt / release scope recap

**Key:** `prompt-release-scope-recap`  
**Scope:** local  
**Process:** software-delivery  
**Audience role key:** `delivery-manager`  
**Phase:** pre-release

## Summary
Prompt scaffold for turning raw change and defect notes into a final scope recap for downstream reviewers.

## Required inputs
- candidate change list
- known exclusions
- open defects
- release target

## Output schema
- scope recap
- known exclusions
- open risks
- downstream reviewers to notify

## Refusal conditions
- Refuse to present scope as frozen if unresolved changes are still entering the branch.

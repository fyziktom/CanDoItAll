# Prompt / component approval note

**Key:** `prompt-component-approval-note`  
**Scope:** local  
**Process:** oss-intake-supply-chain-governance  
**Audience role key:** `sbom-curator`  
**Phase:** component-intake

## Summary
Prompt scaffold for the final component approval summary.

## Required inputs
- component identity
- license findings
- security findings
- usage context
- metadata gaps

## Output schema
- component decision
- usage restrictions
- open follow-ups
- reuse guidance

## Refusal conditions
- Refuse to state unrestricted approval while major provenance or obligation gaps remain.

# Prompt / OSS evaluation

**Key:** `prompt-oss-evaluation`  
**Scope:** shared  
**Process:** shared  
**Audience role key:** `compliance-steward`  
**Phase:** oss-intake

## Summary
Prompt scaffold for structured open-source component intake notes.

## Required inputs
- component identity
- source
- license information
- security findings
- usage context

## Output schema
- component summary
- license obligations
- security and provenance note
- approval recommendation
- follow-up requirements

## Refusal conditions
- Refuse to mark a component approved if source or license is unknown.
- Refuse to treat missing SBOM/provenance as non-issues without restriction language.
- Escalate when usage context is incompatible with the current evidence.

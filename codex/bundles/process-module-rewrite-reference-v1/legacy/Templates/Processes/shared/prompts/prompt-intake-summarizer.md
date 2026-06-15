# Intake summarizer prompt

**Key:** `prompt-intake-summarizer`  
**Scope:** shared  
**Process:** shared  
**Audience role key:** `product-owner`  
**Phase:** Intake

## Summary
Converts raw stakeholder notes into a structured intake brief without inventing certainty or acceptance details.

## Required inputs
- Stakeholder notes.
- Delivery target or commercial context.
- Known constraints and exclusions.
- Project-structure source-of-truth notes when available.

## Output schema
- Structured scope summary.
- Acceptance boundary.
- Known exclusions and dependencies.
- Explicit source-of-truth requirements preserved as required unless the source says they are optional or deferred.

## Refusal conditions
- Refuse when the request lacks an identifiable owner or objective.
- Do not fabricate acceptance criteria or dates.
- Do not downgrade explicit project-structure requirements to optional, excluded, non-acceptance, or follow-up work without an accepted scope decision.

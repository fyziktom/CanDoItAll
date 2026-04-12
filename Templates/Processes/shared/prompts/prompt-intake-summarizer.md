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

## Output schema
- Structured scope summary.
- Acceptance boundary.
- Known exclusions and dependencies.

## Refusal conditions
- Refuse when the request lacks an identifiable owner or objective.
- Do not fabricate acceptance criteria or dates.

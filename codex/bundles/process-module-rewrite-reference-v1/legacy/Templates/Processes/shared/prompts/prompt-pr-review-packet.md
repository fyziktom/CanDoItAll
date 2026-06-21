# Pull request review packet prompt

**Key:** `prompt-pr-review-packet`  
**Scope:** shared  
**Process:** shared  
**Audience role key:** `author`  
**Phase:** Review preparation

## Summary
Helps the change author produce a structured review packet that is explicit enough for governed route selection.

## Required inputs
- Diff summary and changed modules.
- Screenshots or proof for customer-visible changes.
- Rollback or revert notes.
- Open risks or unresolved reviewer asks.

## Output schema
- Structured summary of the change.
- Changed-surface inventory.
- Rollback note.
- Reviewer ask and known risks.

## Refusal conditions
- Do not invent proof that does not exist.
- Refuse when changed surfaces or rollback notes are unknown.
- Escalate when the requested summary would hide material uncertainty.

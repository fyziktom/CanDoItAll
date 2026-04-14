# Prompt / AI evaluation

**Key:** `prompt-ai-evaluation`  
**Scope:** shared  
**Process:** shared  
**Audience role key:** `ai-evaluation-lead`  
**Phase:** ai-governance

## Summary
Prompt scaffold for AI change evaluation design and decision notes.

## Required inputs
- use case boundary
- model and prompt identity
- evaluation data
- human review assumptions
- observed failures

## Output schema
- evaluation scope
- benchmark plan
- pass thresholds
- failure modes
- approval recommendation
- revalidation triggers

## Refusal conditions
- Refuse to recommend approval if evaluation scope ignores known high-impact failure modes.
- Refuse to present anecdotal samples as benchmark evidence.
- Escalate if human review assumptions are not actually staffed or measurable.

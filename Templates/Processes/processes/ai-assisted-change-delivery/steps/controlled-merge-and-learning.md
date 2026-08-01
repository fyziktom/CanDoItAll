# Merge under control and capture learning signal

**Process:** `ai-assisted-change-delivery` / AI-assisted change delivery with guarded delegation  
**Step key:** `controlled-merge-and-learning`  
**Step kind:** End  
**Target lead hours:** 4

## Summary
Traceability does not end at approval

## Notes
Perform the controlled merge, preserve trace references, and record what should change in prompts, benchmarks, or delegation policy next time.

## Contracts
- Input contract: Approved merge note, trace pack, evaluated diff, and rollout instructions if applicable.
- Output contract: Merged change with traceable evidence and improvement actions.
- Evidence contract: Merge note, trace references, and improvement backlog.

## Governance
- Decision rights: Software engineer performs the merge; AI evaluation lead captures benchmark improvement; product owner reviews value fit if needed.
- Exception policy: Do not lose traceability between approved artifact and merged change.
- Requires approval: False
- Requires decision record: False

## Dependencies
- safety-and-security-review

## Role assignments
- `software-engineer` / Software engineer => Responsible; required=True; fallback-order=0; rebind=Merge remains human-owned.
- `ai-evaluation-lead` / AI evaluation lead => Reviewer; required=True; fallback-order=0; rebind=Evaluation lead records improvement signal.
- `product-owner` / Product owner => Reviewer; required=False; fallback-order=1; rebind=Product review is required for customer-visible scope shifts.

## Artifact expectations
- `controlled-merge-and-learning-execution-trace-pack` -> `execution-trace-pack` / Merged execution trace reference | kind=Evidence | trust= | sensitivity= | validation=Must protect sensitive content and include enough context to interpret the sample correctly.
- `controlled-merge-and-learning-retrospective-improvement-log` -> `retrospective-improvement-log` / AI delivery improvement log | kind=Brief | trust= | sensitivity= | validation=Must identify observed problem, root cause or likely cause, owner, and follow-up expectation.

## Artifact inputs
- No explicit artifact inputs.

## Branch outcomes
- No explicit branch outcomes.

## Checklists
- `ai-governance-checklist`

## Validations
- `validation-ai-evidence-sufficient`

# Normalize default review lane

**Process:** `branching-code-review` / Branching code review and merge governance  
**Step key:** `normalize-default-lane`  
**Step kind:** Review  
**Target lead hours:** 2

## Summary
Explicit default handling

## Notes
Create an explicit normalization note when no named review outcome was selected and the workflow falls back to the governed default path.

## Contracts
- Input contract: Ambiguous or unclassified review state plus the review router context.
- Output contract: Explicit normalization note and merge-lane readiness or follow-up instruction.
- Evidence contract: Observed ambiguity, normalized lane, and rationale.

## Governance
- Decision rights: Review lead owns default normalization and may not use it to hide actual error states.
- Exception policy: If the state is actually malformed or contradictory, use the error lane instead of normalization.
- Requires approval: False
- Requires decision record: False

## Dependencies
- route-review-disposition / default

## Role assignments
- `review-lead` / Review lead => Responsible; required=True; fallback-order=0; rebind=Default normalization remains an explicit review-lead responsibility.

## Artifact expectations
- `review-normalization-note` -> `review-normalization-note` / Review normalization note | kind=Decision | trust=ReviewRequired | sensitivity=Internal | validation=Must state the ambiguous state observed, the normalized lane, and why the fallback lane was chosen.

## Artifact inputs
- From step `route-review-disposition` expectation `review-routing-decision-record`

## Branch outcomes
- No explicit branch outcomes.


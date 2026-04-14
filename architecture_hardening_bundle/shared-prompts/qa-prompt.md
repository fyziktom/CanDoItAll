# QA prompt

Validate the selected subbundle against its acceptance checklist and proof contract.

## What to re-read first

- the subbundle `## Acceptance Checklist`
- the subbundle `## Proof Required`
- the subbundle `## Browser Validation Logging`
- the subbundle `## Progression Gate`
- `09-proof-contract.md`

## Mandatory QA rules

- Run the exact targeted proof commands for the selected phase.
- If the phase changes UI, do a large-screen browser pass first and a narrower-width pass second.
- Review screenshots explicitly for readability, clipping, collisions, spacing, alignment, hierarchy, and space use.
- Do not accept “it probably works” proof.
- If proof is missing or stale, fail the gate.
- If proof reveals an earlier weak foundation, reopen it or trigger corrective work.

## Reporting rules

Update:
- `reviews/01-execution-report.md`
- `reviews/02-architecture-gate-memo-log.md` if a gate is involved
- any relevant subbundle status field

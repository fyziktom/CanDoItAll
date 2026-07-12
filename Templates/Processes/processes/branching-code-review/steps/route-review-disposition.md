# Route code review disposition

**Process:** `branching-code-review` / Branching code review and merge governance  
**Step key:** `route-review-disposition`  
**Step kind:** Decision  
**Target lead hours:** 4

## Summary
Switch-style review router

## Notes
Use an explicit branch router node to decide whether the authored change goes to repairs, QA, security, architecture, merge approval, the default normalization lane, or an error lane.

## Contracts
- Input contract: Review-ready pull request packet, reviewer comments, and changed-surface risk notes.
- Output contract: Explicit next-lane selection for the reviewed change.
- Evidence contract: Review notes, chosen route, and reasons for the selected lane.

## Governance
- Decision rights: Review lead owns the switch outcome and must keep the route explicit on the canvas.
- Exception policy: Do not bury routing logic inside comments or verbal agreements; the chosen lane must stay modeled and replayable.
- Requires approval: False
- Requires decision record: True

## Dependencies
- prepare-pull-request

## Role assignments
- `review-lead` / Review lead => Responsible; required=True; fallback-order=0; rebind=The review lead owns the branch-selection decision and its recorded rationale.
- `author` / Author => Reviewer; required=True; fallback-order=0; rebind=The author reviews whether requested follow-up work is clear before the route is accepted.

## Artifact expectations
- `review-routing-decision-record` -> `review-routing-decision-record` / Review routing decision record | kind=Decision | trust=ReviewRequired | sensitivity=Internal | validation=Must name the chosen lane, rationale, and any blockers or residual risks.

## Artifact inputs
- From step `prepare-pull-request` expectation `pull-request-readiness-packet`

## Branch outcomes
- `repairs-required` / Repairs required — Route the change back to the author for a focused repair pass.
- `qa-validation` / QA validation — Route the change into targeted QA and browser proof.
- `security-review` / Security review — Route the change into security and data-handling review.
- `architecture-review` / Architecture review — Escalate architectural consequences that exceed local code review authority.
- `ready-for-merge` / Ready for merge — Send the change directly to the merge approval lane.
- `default` / Default — Continue when no explicit branch outcome is selected.
- `error` / Error — Escalate canvas or runtime authoring failures that prevent a safe routing decision.

## Checklists
- `review-router-safety-checklist`

## Validations
- `validation-review-packet-complete`
- `validate-review-router-safe`

## Prompts
- `prompt-review-normalization`


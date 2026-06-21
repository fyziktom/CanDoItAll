# Escalate architecture consequences

**Process:** `branching-code-review` / Branching code review and merge governance  
**Step key:** `architecture-escalation`  
**Step kind:** Review  
**Target lead hours:** 8

## Summary
Non-local design lane

## Notes
Review architectural consequences that exceed local code-review authority and decide whether the change can still progress.

## Contracts
- Input contract: Review routing decision, change packet, and impacted boundary notes.
- Output contract: Architecture escalation outcome with explicit next action.
- Evidence contract: Architecture concern, decision, and approved next action.

## Governance
- Decision rights: Solution architect owns the architecture lane and may return the change for redesign or authorize progression with conditions.
- Exception policy: Do not hide canonical-model or module-boundary concerns inside informal reviewer comments.
- Requires approval: False
- Requires decision record: False

## Dependencies
- route-review-disposition / architecture-review

## Role assignments
- `solution-architect` / Solution architect => Responsible; required=True; fallback-order=0; rebind=Architecture escalation remains attached to architecture authority rather than a specific person.

## Artifact expectations
- `architecture-escalation-brief` -> `architecture-escalation-brief` / Architecture escalation brief | kind=Decision | trust=ReviewRequired | sensitivity=Internal | validation=Must capture the architectural concern, impacted boundary, and decision needed.

## Artifact inputs
- From step `route-review-disposition` expectation `review-routing-decision-record`

## Branch outcomes
- No explicit branch outcomes.

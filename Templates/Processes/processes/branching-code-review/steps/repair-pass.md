# Complete focused repair pass

**Process:** `branching-code-review` / Branching code review and merge governance  
**Step key:** `repair-pass`  
**Step kind:** Work  
**Target lead hours:** 8

## Summary
Author repair loop

## Notes
Apply the requested repair scope and return a bounded fix package without hiding residual uncertainty.

## Contracts
- Input contract: Review routing decision, blocking review comments, and the original pull request packet.
- Output contract: Updated change set and repair brief ready for another routing decision or merge path.
- Evidence contract: Repair diff, note of addressed concerns, and any remaining risk.

## Governance
- Decision rights: Author may fix only the bounded review issues unless the route is re-opened explicitly.
- Exception policy: Escalate if repairs materially expand scope beyond the routed review request.
- Requires approval: False
- Requires decision record: False

## Dependencies
- route-review-disposition / repairs-required

## Role assignments
- `author` / Author => Responsible; required=True; fallback-order=0; rebind=Repair ownership stays with the author unless explicitly rebound.

## Artifact expectations
- `repair-brief` -> `repair-brief` / Repair brief | kind=Brief | trust=ReviewRequired | sensitivity=Internal | validation=Must state the repair scope, blocking findings, and what evidence is required on resubmission.

## Artifact inputs
- From step `route-review-disposition` expectation `review-routing-decision-record`

## Branch outcomes
- No explicit branch outcomes.

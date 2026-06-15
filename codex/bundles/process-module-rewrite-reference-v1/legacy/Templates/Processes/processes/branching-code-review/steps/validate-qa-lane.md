# Validate QA lane and browser proof

**Process:** `branching-code-review` / Branching code review and merge governance  
**Step key:** `validate-qa-lane`  
**Step kind:** Review  
**Target lead hours:** 6

## Summary
Targeted regression lane

## Notes
Run targeted regression and browser proof for changes explicitly routed into the QA lane.

## Contracts
- Input contract: Review routing decision, pull request packet, and changed-surface inventory.
- Output contract: QA lane result with proof depth and residual quality risk.
- Evidence contract: QA notes, screenshots, regression result, and unresolved concerns.

## Governance
- Decision rights: QA lead may keep the change out of merge until risk is explicit and acceptable.
- Exception policy: Do not convert QA lane results into chat-only confidence.
- Requires approval: False
- Requires decision record: False

## Dependencies
- route-review-disposition / qa-validation

## Role assignments
- `qa-lead` / QA lead => Responsible; required=True; fallback-order=0; rebind=QA lane ownership remains explicit even when rota coverage changes.
- `author` / Author => Reviewer; required=True; fallback-order=0; rebind=The author reviews QA failures before merge or reroute.

## Artifact expectations
- `qa-lane-validation-note` -> `qa-lane-validation-note` / QA lane validation note | kind=Evidence | trust=ReviewRequired | sensitivity=Internal | validation=Must name tested flows, proof depth, and unresolved quality risks.

## Artifact inputs
- From step `route-review-disposition` expectation `review-routing-decision-record`

## Branch outcomes
- No explicit branch outcomes.

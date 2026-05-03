# Run QA validation and browser proof

**Process:** `software-delivery` / Multi-team software delivery and release governance  
**Step key:** `qa-validation`  
**Step kind:** Review  
**Target lead hours:** 10

## Summary
Regression, UX, and quality evidence

## Notes
Execute targeted regression, browser proof, and defect triage against the reviewed implementation package.

## Contracts
- Input contract: Peer-reviewed change set, changed-surface inventory, release-scope assumptions, and direct inspection of inherited implementation artifact paths.
- Output contract: Targeted QA result with screenshots, regressions, and explicit residual quality risk.
- Evidence contract: Regression logs, browser proof, screenshots, defect notes, and stat/read evidence for the exact implementation artifacts under review.

## Governance
- Decision rights: QA lead may block release progression when evidence is too thin for the risk profile.
- Exception policy: Do not let schedule pressure replace proof with verbal confidence.
- Requires approval: False
- Requires decision record: False

## Dependencies
- implementation
- peer-review

## Role assignments
- `qa-lead` / QA lead => Responsible; required=True; fallback-order=0; rebind=QA ownership may move across qualified reviewers without collapsing the gate.
- `lead-engineer` / Lead engineer => Reviewer; required=True; fallback-order=0; rebind=Implementation owner reviews failures and fixes before release approval.

## Artifact expectations
- `regression-evidence-pack` -> `regression-evidence-pack` / Regression evidence pack | kind=Evidence | trust=ReviewRequired | sensitivity=Internal | validation=Must name changed flows, assertion depth, screenshots, and unresolved risks.

## Artifact inputs
- From step `implementation` expectation `implementation-change-set`
- From step `peer-review` expectation `peer-review-note`

## Branch outcomes
- No explicit branch outcomes.

## Checklists
- `qa-evidence-checklist`

## Prompts
- `prompt-qa-risk-review`

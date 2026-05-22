# Run QA validation and runtime or browser proof

**Process:** `software-delivery` / Multi-team software delivery and release governance  
**Step key:** `qa-validation`  
**Step kind:** Review  
**Target lead hours:** 10

## Summary
Regression, UX, runtime, and quality evidence

## Notes
Execute targeted regression, runtime/API/browser proof as applicable, and defect triage against the reviewed implementation package. Treat validation warnings, zero-test successful commands, entrypoint/runtime mismatches, and stale or unreferenced artifact evidence as release-blocking proof gaps unless this process explicitly accepts them.

## Contracts
- Input contract: Peer-reviewed change set, changed-surface inventory, release-scope assumptions, and direct inspection of inherited implementation artifact paths.
- Output contract: Targeted QA result with runtime/API/browser evidence as applicable, regressions, warning and executed-test counts, shipped entrypoint/runtime consistency, and explicit residual quality risk.
- Evidence contract: Regression logs, warning-free validation output unless explicitly accepted, nonzero executed-test proof when tests are expected, runtime/API/browser proof as applicable, screenshots for UI surfaces, defect notes, shipped entrypoint plus referenced-runtime inspection, stale or unreferenced artifact assessment, and stat/read evidence for the exact implementation artifacts under review.

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
- `regression-evidence-pack` -> `regression-evidence-pack` / Regression evidence pack | kind=Evidence | trust=ReviewRequired | sensitivity=Internal | validation=Must name changed flows, assertion depth, warning counts, executed-test counts when tests are expected, shipped entrypoint and referenced runtime files or commands, runtime/API/browser evidence as applicable, screenshots for UI surfaces, stale/unreferenced artifact findings, and unresolved risks.

## Artifact inputs
- From step `implementation` expectation `implementation-change-set`
- From step `peer-review` expectation `peer-review-note`

## Branch outcomes
- No explicit branch outcomes.

## Checklists
- `qa-evidence-checklist`

## Prompts
- `prompt-qa-risk-review`

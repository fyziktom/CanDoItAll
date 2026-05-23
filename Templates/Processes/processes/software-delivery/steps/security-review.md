# Perform security and data-handling review

**Process:** `software-delivery` / Multi-team software delivery and release governance  
**Step key:** `security-review`  
**Step kind:** Approval  
**Target lead hours:** 6

## Summary
Sensitive data, secrets, and exception posture

## Notes
Review sensitive-data handling, secrets, boundary changes, and policy exceptions before release approval. Scale findings to the declared release boundary instead of inventing production controls that are outside the approved handoff.

## Contracts
- Input contract: Peer-reviewed package, changed-surface inventory, and data-handling notes.
- Output contract: Security outcome with explicit approval, block, or exception rationale tied to the declared release boundary.
- Evidence contract: Security review notes, exception rationale, boundary-applicable controls, and future production controls when they are outside the current boundary.

## Governance
- Decision rights: Security reviewer owns the sign-off for sensitive-data and boundary-applicable policy exceptions.
- Exception policy: Block release when current-boundary data-handling review capacity is missing, exception rationale is incomplete, or a security risk affects the approved handoff. Record out-of-boundary production controls as recommendations unless explicitly required.
- Requires approval: True
- Requires decision record: False

## Dependencies
- implementation
- peer-review

## Role assignments
- `security-reviewer` / Security reviewer => Approver; required=True; fallback-order=0; rebind=Security approval remains attached to the role even if the reviewer changes.

## Artifact expectations
- `security-exception-assessment` -> `security-exception-assessment` / Security exception assessment | kind=Decision | trust=ReviewRequired | sensitivity=Confidential | validation=Must capture boundary-applicable controls, residual risk owner, approval or block rationale, and any future controls that are not release blockers for the current boundary.

## Artifact inputs
- From step `implementation` expectation `implementation-change-set`
- From step `peer-review` expectation `peer-review-note`

## Branch outcomes
- No explicit branch outcomes.

## Checklists
- `security-review-checklist`

## Prompts
- `prompt-security-review`

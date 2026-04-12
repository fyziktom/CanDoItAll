# Complete peer review and integration readiness

**Process:** `software-delivery` / Multi-team software delivery and release governance  
**Step key:** `peer-review`  
**Step kind:** Review  
**Target lead hours:** 8

## Summary
Design and implementation challenge

## Notes
Review the change set against the approved design, integration consequences, and release assumptions.

## Contracts
- Input contract: Implementation package, architecture decision record, and changed-surface inventory.
- Output contract: Peer-reviewed change set with explicit residual risk and follow-up items.
- Evidence contract: Review notes, unresolved issues list, and approved follow-up actions.

## Governance
- Decision rights: Reviewers may block unsafe merge or release progression until the change set satisfies design and evidence expectations.
- Exception policy: Do not downgrade architecture, data, or migration concerns to cosmetic comments.
- Requires approval: False
- Requires decision record: False

## Dependencies
- implementation

## Role assignments
- `lead-engineer` / Lead engineer => Responsible; required=True; fallback-order=0; rebind=Implementation owner remains attached until review comments are addressed.
- `solution-architect` / Solution architect => Reviewer; required=True; fallback-order=0; rebind=Architectural review remains explicit when cross-module consequences exist.
- `qa-lead` / QA lead => Reviewer; required=False; fallback-order=0; rebind=QA participates when changed surface suggests regression risk.

## Artifact expectations
- `peer-review-note` -> `test-evidence-pack` / Peer review note | kind=Evidence | trust=ReviewRequired | sensitivity=Internal | validation=Must capture accepted issues, rejected concerns, and explicit residual risk.

## Artifact inputs
- From step `implementation` expectation `implementation-change-set`

## Branch outcomes
- No explicit branch outcomes.

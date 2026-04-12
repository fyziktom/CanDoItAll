# Prepare pull request and reviewer brief

**Process:** `branching-code-review` / Branching code review and merge governance  
**Step key:** `prepare-pull-request`  
**Step kind:** Start  
**Target lead hours:** 6

## Summary
Implementation packet

## Notes
Package the code change, screenshots, impacted modules, test proof, and release-note draft before the review board decides what lane comes next.

## Contracts
- Input contract: Accepted work item, implementation diff, test evidence, and draft release notes.
- Output contract: Review-ready pull request packet with typed reviewer context and proof references.
- Evidence contract: Diff summary, screenshots, changed-surface list, and rollback notes.

## Governance
- Decision rights: The author may prepare the review packet but cannot declare the change ready for merge without explicit routing.
- Exception policy: Pause immediately if proof, rollback notes, or touched-surface inventory is incomplete.
- Requires approval: False
- Requires decision record: False

## Dependencies
- No explicit predecessor.

## Role assignments
- `author` / Author => Responsible; required=True; fallback-order=0; rebind=Authorship stays attached to the implementation owner even if the engineer changes before review.

## Artifact expectations
- `pull-request-readiness-packet` -> `pull-request-readiness-packet` / Pull request readiness packet | kind=Deliverable | trust=ReviewRequired | sensitivity=Internal | validation=Must name touched modules, screenshots, rollback notes, and explicit reviewer asks.

## Artifact inputs
- No explicit artifact inputs.

## Branch outcomes
- No explicit branch outcomes.

## Checklists
- `pull-request-packet-checklist`

## Prompts
- `prompt-pr-review-packet`

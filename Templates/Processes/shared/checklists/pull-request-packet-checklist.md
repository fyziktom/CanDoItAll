# Pull request packet checklist

**Key:** `pull-request-packet-checklist`  
**Scope:** shared  
**Process:** shared  
**Owner role key:** `author`  
**Phase:** Review preparation

## Summary
Ensures the review packet contains the minimum context and proof needed for explicit route selection.

## Entry criteria
Code changes and initial proof exist and the author is preparing the review packet.

## Exit criteria
The review packet is complete enough that the review lead can choose a governed lane without guessing.

## Checks
- Changed modules, services, or bounded contexts are listed explicitly.
- Screenshots or other proof exist for every customer-visible change.
- Rollback or revert notes are attached for the change.
- Open risks or unresolved questions are named clearly.
- Reviewer ask is explicit instead of implied by raw code only.

## Evidence expectations
- A typed pull request readiness packet exists.
- References to screenshots, tests, or evidence are included.
- Any missing proof is labeled as blocking, not implicit.

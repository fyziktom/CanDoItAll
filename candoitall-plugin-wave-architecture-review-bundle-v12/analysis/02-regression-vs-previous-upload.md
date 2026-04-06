# Regression versus the previous upload

## Why this matters
The current review is stronger than a simple static snapshot review because there is a directly comparable earlier user-uploaded repository in this conversation history.

The earlier upload:
- passed the phase10 gate,
- contained the explicit projection maintenance service,
- contained the zero-write proof tests,
- contained the unknown-manifest round-trip proof.

The current upload removes or regresses those items.

## Regression evidence
See:
- `inventories/05-phase10-gate-previous-upload-run.txt`
- `inventories/02-phase10-gate-current-run.txt`
- `inventories/06-regression-diff-vs-previous-upload.txt`

## Important interpretation
This looks like one of two things:
1. an older or wrong ZIP was uploaded, or
2. bundle11 work was attempted on top of a branch / snapshot that did not include the previously validated phase10 closure.

Either way, the only safe interpretation for this review is the code that is actually inside the uploaded ZIP.
Bundle12 therefore treats the current upload as the source of truth and requires phase10 recovery first.

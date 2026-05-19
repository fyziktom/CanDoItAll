# 05-dream-aggregate-quality

## Status

- `Ready`

## Objective

Make dream aggregates specific enough for human approval and safe enough to reject early when they are only structural summaries.

## Required Edits

- Build aggregate titles and bodies from primary keys plus source-backed snippets.
- Add a quality gate for structural-only or redacted-only aggregate candidates.
- Audit aggregate review decisions and application outcomes.

## Closure Proof

- At least one aggregate candidate contains concrete source-backed facts and is approved.
- Structural-only candidates are rejected or blocked with a clear reason.

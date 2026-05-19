# 07-qdrant-projection-operability

## Status

- `Ready`

## Objective

Make Qdrant projection health and vector recall behavior clear enough for operators to trust validation results.

## Required Edits

- Add default projection profile diagnostics.
- Add per-project/per-collection projection summaries.
- Add explicit recall warnings for missing projection options and provider failures.

## Closure Proof

- Projection rebuild proof shows projected, failed, and skipped counts.
- Recall proof distinguishes Qdrant hits from lexical-only recall.

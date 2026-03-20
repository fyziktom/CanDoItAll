# Prompt 07 — Tests, Fixtures, And Copied-DB Benchmarking

## Objective

Build the test safety net and copied-DB benchmark workflow.

## Tasks

1. Expand unit tests around normalization, scoring, and manual overrides.
2. Add integration tests for:
   - profile generation
   - dry run
   - apply run
   - rerun idempotency
3. Expand Playwright coverage for grouping workflows.
4. Add a benchmark fixture recipe or helper for copied real DB evaluation.
5. Add docs or scripts for copy-first DB benchmarking.

## Boundaries

- do not require access to the original real DB during CI
- do not make Playwright the only confidence source
- keep generated test data deterministic

## Required tests

Add coverage for:
- same title / different composers
- arrangement vs original
- movement vs full work
- multilingual variant
- false-positive prevention
- large-block guardrails
- manual lock behavior
- canonical display selection
- derived tag sync

## Review checklist

- [ ] benchmark path uses DB copy/snapshot only
- [ ] tests cover both precision and regression safety
- [ ] existing sample workflow still passes

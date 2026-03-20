# Prompt 09 — Final Self Review

Before handing the implementation back, review it critically.

## Self-review tasks

1. Re-read the bundle sections on:
   - data model
   - normalization
   - embeddings
   - scoring
   - UI
   - validation
2. Audit for accidental regressions in:
   - indexing
   - catalog
   - score detail
   - existing grouping routes
   - background task startup state
3. Verify no code path still performs destructive full regroup rebuild.
4. Verify manual locks and manual memberships cannot be silently overwritten.
5. Verify copied-DB workflow exists for real-data benchmarking.
6. Run all relevant tests.
7. Summarize:
   - completed work
   - remaining TODOs
   - known limitations
   - threshold tuning still needed
   - recommended next validator focus

## Final review checklist

- [ ] canonical truth is not tag-only
- [ ] multi-membership exists
- [ ] dry run exists
- [ ] apply exists
- [ ] evidence exists
- [ ] ambiguous cases have a review path
- [ ] embeddings are cached and incremental
- [ ] real DB is not mutated directly during validation

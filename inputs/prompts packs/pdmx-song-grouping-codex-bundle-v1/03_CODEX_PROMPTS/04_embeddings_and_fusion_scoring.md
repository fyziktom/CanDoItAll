# Prompt 04 — Embeddings And Fusion Scoring

## Objective

Add Ollama-based embedding generation and composite pair scoring.

## Tasks

1. Implement embedding-model availability check and pull-if-missing workflow.
2. Implement batched embedding generation from `EmbeddingInputText`.
3. Persist vectors with content hash and model metadata.
4. Add cosine similarity helpers.
5. Implement composite scoring:
   - deterministic structure
   - token overlap
   - composer similarity
   - catalog match/conflict
   - movement match/conflict
   - arrangement conflict
   - embedding similarity
6. Classify results into confidence bands.
7. Persist evidence summaries and evidence JSON in run-preview rows.

## Boundaries

- Do not include full AI descriptions in the primary work embedding text.
- Do not compute all-pairs similarity across the whole dataset.
- Do not auto-apply review-band edges blindly.

## Required tests

- skip unchanged vectors
- regenerate on input-hash change
- composite score sanity tests
- hard-conflict rejection tests
- confidence-band threshold tests
- evidence payload content tests

## Review checklist

- [ ] embeddings are cached
- [ ] model name and input hash persisted
- [ ] descriptions are auxiliary only
- [ ] scoring explains itself
- [ ] strong structure dominates generic semantic similarity

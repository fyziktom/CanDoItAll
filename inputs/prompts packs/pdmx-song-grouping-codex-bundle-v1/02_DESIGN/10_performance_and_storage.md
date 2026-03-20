# Performance And Storage Plan

## Scale reality

200k+ rows is large enough that:
- all-vs-all comparison is unacceptable,
- giant JSON blobs are a problem,
- destructive full rebuilds become expensive and risky,
- incremental recomputation matters.

## Vector storage size estimates

Raw float32 storage:
- 384 dimensions:
  - `200,000 * 384 * 4` bytes
  - about `307,200,000` bytes raw
- 768 dimensions:
  - about `614,400,000` bytes raw
- 1024 dimensions:
  - about `819,200,000` bytes raw

Practical consequence:
- vector JSON storage is wasteful,
- BLOB storage is preferable,
- phase 1 should store only the embeddings actually used.

## Recommended DB strategy

### SQLite-friendly phase 1
- persist vectors as BLOBs
- no SQL vector search dependency
- compare vectors in app code for candidate sets only

### PostgreSQL-compatible phase 1
- same data model should still work
- vector-specific acceleration can be optional later

## Index strategy

Add indexes for:
- `ScoreGroupingProfile.NormalizedTitleStrict`
- `ScoreGroupingProfile.NormalizedComposerStrict`
- `ScoreGroupingProfile.ComposerSurnameKey`
- `ScoreGroupingProfile.PrimaryCatalogSystem + PrimaryCatalogValue`
- `ScoreGroupingProfile.WorkSignatureStrict`
- `SongGroup.GroupKey`
- `SongGroupMembership.IndexedScoreId`
- `SongGroupMembership.SongGroupId`
- `SongGroupingRun.Status`
- `SongGroupingRun.RequestedUtc`

## Candidate-generation performance guidance

Efficient order:
1. select score IDs in scope
2. load only grouping profiles first
3. build blocks from compact structured fields
4. compute cheap heuristic features
5. only then load embeddings for surviving candidates

Avoid:
- eager-loading every score with every navigation property,
- loading vector blobs too early,
- reading full `IndexedScore` text columns when profile fields are enough.

## Batching guidance

Suggested starting values:
- profile refresh DB write batch: 200–1000 rows
- embedding request batch: 32–64 rows
- dry-run preview insert batch: 100–500 rows
- apply batch: per cluster or 25–100 memberships

Codex should expose these as named constants or options, not hidden literals.

## Parallelism guidance

Current workstation already runs:
- indexing,
- harmony,
- Ollama suggestions.

For grouping embeddings:
- do not assume the same parallelism as harmony is safe,
- keep embedding generation conservative at first,
- benchmark actual Ollama throughput in the target environment.

Suggested phase 1 posture:
- single grouping embedding worker lane within existing grouping task execution,
- limited in-task batch concurrency,
- preserve UI responsiveness.

## Hot-block mitigation

Some tokens will create huge blocks:
- `beethoven`,
- `mozart`,
- `sonata`,
- generic hymn titles,
- missing-composer rows.

Mitigation:
- require secondary keys:
  - catalog,
  - work number,
  - distinctive title tokens,
  - movement markers
- cap block expansion
- downgrade oversize blocks to review or staged sub-blocks

## Avoid repeated normalization

Once `ScoreGroupingProfile` exists:
- do not recompute normalization inside every catalog query or pair-scoring loop,
- recompute only when source text or normalization version changes.

## Memory guidance

For large dry runs:
- stream profiles or page them,
- do not hold all vector blobs in memory if not needed,
- prefer score ID lists and compact profile records in the first pass.

## Success metrics to track

- rows profiled per minute
- embeddings per minute
- candidate pairs generated
- average block size
- max block size
- clusters produced
- suspicious clusters
- DB size growth
- rerun delta size

## First performance benchmark checklist

Codex should benchmark on a copied DB:
- profile refresh throughput
- missing embedding throughput
- dry-run speed on:
  - 1k rows
  - 10k rows
  - selected large-composer slices
- memory footprint
- distribution of block sizes
- false-positive rate on a hand-reviewed benchmark subset

## When to consider ANN/vector-index acceleration later

Only consider extra vector infrastructure if copied-DB validation shows:
- acceptable precision but unacceptable runtime,
- or candidate blocks remain too large even after structured blocking.

That is a phase 2 optimization, not a phase 1 requirement.

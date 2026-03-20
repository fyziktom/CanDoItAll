# Candidate Generation And Embeddings

## Main principle

Candidate generation must do most of the scale work.
Embeddings must support it, not replace it.

## Recommended grouping stages

### Stage 0: input selection

The run must declare scope:
- all indexed scores,
- only ungrouped scores,
- only scores with changed grouping profile,
- specific score IDs,
- specific filtered subset.

### Stage 1: profile refresh

Refresh `ScoreGroupingProfile` for any selected score whose:
- raw metadata changed,
- normalization version changed,
- grouping fields missing.

### Stage 2: deterministic auto-group candidates

Immediately identify obvious same-work cases using:
- same manual override group key,
- same strict work signature,
- same strict composer + same catalog system/value + same movement number,
- same strict composer + same loose title + no conflicting structure.

These should generate high-confidence candidate edges before embeddings.

### Stage 3: blocking

Build candidate blocks, for example:

#### Block family A: composer-first
- same `NormalizedComposerStrict`
- or same `ComposerSurnameKey` plus high initial overlap

#### Block family B: catalog-first
- same `PrimaryCatalogSystem` + `PrimaryCatalogValue`

#### Block family C: title fingerprint
- same first 2–3 distinctive title tokens
- same work type + number

#### Block family D: fallback for missing composer
- same catalog data
- or same highly distinctive title fingerprint
- or same existing manual group override

Each score may appear in multiple blocks.

## Block-size guardrails

Large blocks are dangerous.
Suggested behavior:

- if block size <= 200:
  - pairwise scoring inside block is fine
- if block size 201–1000:
  - require secondary sub-blocks before full pairwise comparison
- if block size > 1000:
  - do not fully compare pairwise
  - use:
    - stricter sub-blocks,
    - top-K token candidates,
    - or embedding nearest-neighbor inside the block

## Cheap pre-score before embeddings

For each candidate pair, compute cheap features first:
- exact strict title match
- exact strict composer match
- loose token overlap
- catalog match
- movement match/conflict
- key match/conflict
- arrangement marker match/conflict

Then:
- reject obviously bad pairs early,
- embed only plausible pairs or plausible neighborhoods.

## Recommended embedding input

Phase 1 recommendation:
- use **one primary combined work embedding text**,
- optionally add a lighter title-only fallback for missing-composer cases.

### Primary embedding text template

```text
composer: <normalized composer loose or strict>
title: <normalized title loose>
work_type: <work type if known>
catalog: <catalog tokens if known>
number: <work number if known>
movement: <movement number or label if known>
key: <normalized key if known>
modifiers: <arrangement/excerpt/editorial markers if known>
```

Example:
```text
composer: frederic chopin
title: nocturne in d flat major
work_type: nocturne
catalog: opus 27 number 2
number: 2
key: d_flat_major
modifiers:
```

Why this is better than raw title only:
- more stable than source punctuation,
- keeps strong identity features close together,
- gives embeddings more semantic structure.

## Description usage policy

We already have descriptions for all songs.
They are valuable, but they are **not** ideal as primary grouping input.

Recommended use:
- do **not** include full AI descriptions in the primary work embedding text for phase 1,
- use descriptions only as an optional auxiliary signal in tie-break or review UI.

Why:
- descriptions can contain instrumentation and mood details that differ by edition,
- they can over-amplify arrangement differences,
- they may introduce model-specific drift unrelated to work identity.

Safe auxiliary uses:
- show in review UI,
- optional `DescriptionAux` embedding for later experiments,
- explain mismatches when title/composer are weak.

## Ollama model recommendation

Start with a small, local, multilingual-friendly embedding model that supports batch embed calls.
Practical first benchmark candidates:

- `embeddinggemma`
- optional comparison candidate:
  - `mxbai-embed-large`

Recommended first default:
- `embeddinggemma`

Why:
- good local footprint,
- multilingual training,
- suitable for search / clustering style tasks,
- compatible with Ollama batch embeddings.

## Embedding generation strategy

- generate only for scores in scope or missing/outdated vectors
- batch inputs
- store vectors with `InputHash`
- skip unchanged rows

Suggested batch policy for phase 1:
- start at 64 inputs per batch
- measure throughput and memory pressure
- then adjust

## Similarity strategy

Since Ollama returns unit-length vectors, cosine similarity is straightforward.
Use embeddings as:
- a numeric feature in the composite score,
- or for top-K neighbor retrieval inside a candidate block.

## No all-vs-all rule

Never do this on the full dataset:
- embed all 200k rows,
- compute all pairwise similarities.

Use one of these instead:
- block first, then pairwise within block
- block first, then top-K by token score, then embedding rerank
- block first, then approximate nearest-neighbor inside selected candidate pools

## Why not make FAISS a hard requirement in phase 1

FAISS is powerful, but the current codebase is C# + EF Core + Ollama-centric.
The first implementation should stay operationally simple.

Recommended phase 1:
- no hard dependency on FAISS or a vector DB
- persist vectors
- compare vectors only for plausible candidate sets

Recommended phase 2+ optional upgrade:
- add ANN index support if copied-DB dry runs show clear need

## Candidate pair pipeline detail

Suggested per pair:

1. compute heuristic gates
2. if hard conflict -> reject
3. if deterministic exact match -> accept with high confidence
4. otherwise, if plausible -> use embeddings
5. fuse final score
6. place into band:
   - auto-accept
   - review
   - reject

## Suggested hard conflicts

Examples:
- strong distinct composer mismatch and no alias evidence
- exact conflicting catalog identifiers
- exact conflicting movement number when policy is exact-work grouping
- arrangement marker mismatch if policy says arrangement is separate

## Missing-composer handling

Missing composer is common enough to plan for.

Recommended fallback behavior:
- tighten title/catalog criteria,
- require stronger token/catalog evidence,
- lower auto-accept willingness,
- route more pairs to review.

## Stored metadata for reproducibility

Each vector row should remember:
- model name
- dimensions
- input hash
- embedding kind
- updated timestamp

Each grouping run should remember:
- model name
- normalization version
- threshold profile
- whether vectors were reused or regenerated

## Phase 1 success criterion

Phase 1 is successful if:
- obvious duplicates group automatically,
- false positives are low,
- ambiguous cases become reviewable,
- runtime stays practical on copied real data,
- reruns are incremental.

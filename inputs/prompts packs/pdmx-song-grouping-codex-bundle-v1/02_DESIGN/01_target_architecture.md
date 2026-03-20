# Target Architecture

## Core design decision

The grouping feature should become a **first-class enrichment and curation subsystem**, not a single service that emits `SongGroup` rows.

## Target subsystem layers

```text
+--------------------------------------------------------------+
| UI                                                           |
|  - Home maintenance actions                                  |
|  - Catalog filters and badges                                |
|  - Score detail grouping panel                               |
|  - Groups browser / review queue / group detail              |
+---------------------------|----------------------------------+
                            v
+--------------------------------------------------------------+
| Application services                                          |
|  - SongGroupingRunService                                     |
|  - SongGroupingProfileService                                 |
|  - SongGroupingCandidateService                               |
|  - SongGroupingScoringService                                 |
|  - SongGroupingClusteringService                              |
|  - SongGroupAdminService                                      |
+---------------------------|----------------------------------+
                            v
+--------------------------------------------------------------+
| Persistence                                                   |
|  - IndexedScore                                               |
|  - ScoreGroupingProfile                                       |
|  - ScoreEmbeddingVector                                       |
|  - SongGroup                                                  |
|  - SongGroupMembership                                        |
|  - SongGroupingRun / run preview tables                       |
+---------------------------|----------------------------------+
                            v
+--------------------------------------------------------------+
| External/local model services                                 |
|  - Ollama embed API                                           |
+--------------------------------------------------------------+
```

## Architectural principles

### 1. Separate grouping inputs from grouping outcomes

Inputs:
- raw title/composer/source metadata
- normalized fields
- extracted structured signals
- optional embeddings

Outcomes:
- proposed groups in a run
- applied groups and memberships
- evidence/rationale

This separation is critical for:
- safe reruns,
- debugging,
- benchmark evaluation,
- manual curation.

### 2. Keep `IndexedScore` summary compatibility, but stop treating it as the full truth

Recommended compatibility strategy:
- keep `IndexedScore.SongGroupId` for primary-group summaries in existing catalog/detail UI,
- add canonical many-to-many memberships,
- derive the cached primary pointer from memberships.

Why:
- lower migration risk,
- faster initial UI adaptation,
- existing tests/pages keep working while richer UI is added.

### 3. Use run-based grouping

Grouping should happen inside explicit runs:
- dry run
- review
- apply

A run should capture:
- normalization version
- embedding model
- score scope
- thresholds
- counts and timings
- status
- whether it was applied

This makes results auditable and repeatable.

### 4. Manual curation must survive reruns

Required behavior:
- manual group assignment outranks automatic assignment,
- manual removal / split is sticky,
- pipeline reruns may propose changes but must not silently overwrite protected decisions.

### 5. Grouping logic must be layered

The architecture should explicitly separate:

- normalization and extraction
- candidate generation
- pairwise scoring
- clustering
- application of accepted results
- manual group admin

Avoid one giant service method that does everything.

## Recommended service responsibilities

### `ScoreGroupingProfileService`

Responsibilities:
- build/update normalized grouping profiles,
- parse and extract:
  - composer aliases,
  - catalog tokens,
  - movement markers,
  - key signatures,
  - work-type tokens,
- produce embedding input text,
- mark profile freshness/versioning.

### `ScoreGroupingEmbeddingService`

Responsibilities:
- check Ollama model availability,
- batch embedding generation,
- store vectors,
- skip unchanged vectors via content hash,
- expose cosine similarity helpers.

### `SongGroupingCandidateService`

Responsibilities:
- build candidate blocks,
- enforce max block size guardrails,
- emit candidate pairs or candidate neighborhoods,
- produce cheap heuristic pre-scores.

### `SongGroupingScoringService`

Responsibilities:
- combine structured signals + token similarity + embeddings,
- classify:
  - auto-accept,
  - review,
  - reject,
- build explanation/evidence objects.

### `SongGroupingClusteringService`

Responsibilities:
- cluster accepted edges,
- prevent giant-junk clusters,
- perform cluster-level consistency checks,
- produce proposed groups for a run.

### `SongGroupingRunService`

Responsibilities:
- create and progress grouping runs,
- track scope and statistics,
- expose run summaries to UI,
- apply reviewed results to canonical group tables.

### `SongGroupAdminService`

Responsibilities:
- create group manually,
- add/remove membership,
- merge groups,
- split group,
- set primary member / representative,
- set canonical display title/composer,
- sync derived tags.

## Recommended task model evolution

### Option A: add several task kinds
Pros:
- explicit stages in the UI and telemetry
Cons:
- more queue plumbing

### Option B: keep `GenerateSongGroups` but expand request modes
Pros:
- smaller queue-surface change
Cons:
- one task kind starts doing many distinct things

### Recommendation

Use **Option B for phase 1**:
- keep `GenerateSongGroups`
- expand request with explicit mode/options

Example modes:
- `BuildProfilesOnly`
- `BuildMissingEmbeddings`
- `DryRunGenerate`
- `ApplyRun`
- `RefreshSelectedScores`

This keeps the existing lane topology intact and reduces the chance that Codex breaks task routing.

## Primary data flow

```text
Indexing / Refresh
    -> ScoreGroupingProfile refresh
    -> optional missing embedding generation
    -> candidate generation
    -> pair scoring
    -> run proposals
    -> review / apply
    -> SongGroup + SongGroupMembership update
    -> IndexedScore primary group cache update
    -> derived group tags refresh
```

## Canonical truth vs compatibility projections

Canonical truth:
- `SongGroup`
- `SongGroupMembership`

Compatibility projections:
- `IndexedScore.SongGroupId`
- `group:XYZ` tags or dedicated serialized tag field

Rule:
- projections may be recomputed,
- canonical tables are the source of truth.

## Anti-patterns to avoid

- destructive delete-and-recreate grouping
- grouping hidden inside catalog queries
- raw tags as only source of truth
- embedding vectors stored as giant JSON blobs in the main score row
- manual decisions stored only in UI state
- rebuilding all embeddings on every run

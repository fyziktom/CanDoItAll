# Real DB Evaluation Strategy (Without Modifying The Original DB)

## Hard rule

The original indexed DB must not be mutated by Codex or validator experiments.

## Safe evaluation options

### Preferred for SQLite
1. locate the active SQLite DB file
2. create a full copy to a temp path
3. point the workstation/test environment at the copied DB
4. run migrations and grouping work against the copy only

### Preferred for PostgreSQL
1. create a scratch database or scratch schema from a snapshot
2. point tests and local app config there
3. destroy scratch after validation

## Evaluation passes

### Pass 1: schema and profile smoke test
Scope:
- small copied subset or temp DB copy

Validate:
- migrations apply
- profiles generate
- existing catalog/detail pages still load

### Pass 2: normalization benchmark subset
Build a benchmark set of:
- obvious duplicates
- known non-duplicates with similar titles
- arrangement vs original
- movement vs complete work
- same title / different composers

Validate:
- profile extraction quality
- catalog parsing quality
- alias generation sanity

### Pass 3: embedding throughput benchmark
Validate:
- model availability
- pull-if-missing behavior
- batch embedding throughput
- skip-if-unchanged behavior

### Pass 4: copied full-DB dry run
Validate:
- candidate block size distribution
- suspicious large groups
- review volume
- auto-accept precision on sampled clusters

### Pass 5: sampled manual audit
Randomly audit:
- 50 high-confidence groups
- 50 review-band groups
- 25 rejected but near-threshold pairs
- 25 suspicious large groups

## Suggested benchmark slices from the copied DB

Codex should create read-only reports for slices such as:
- top composers by row count
- top normalized title stems by row count
- missing-composer rows
- rows with catalog identifiers
- rows with arrangement markers
- rows with movement markers

## Minimal report outputs to save

Save these artifacts from copied-DB evaluation:
- normalization metrics summary
- block-size histogram
- top suspicious clusters
- threshold outcome counts
- sampled false-positive and false-negative notes

## Safety rule for Codex

Any SQL or app command that can write must target:
- temp DB copy,
- not the original DB path.

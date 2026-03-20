# Pipeline Integration And Rollout Plan

## Keep the durable-task architecture

The grouping subsystem should plug into the existing `ProcessingTask` infrastructure.
This is already one of the strongest parts of the workstation.

## Recommended request expansion

Expand `GenerateSongGroupsTaskRequest` with explicit run options.

Suggested fields:
- `Mode`
  - `BuildProfilesOnly`
  - `BuildMissingEmbeddings`
  - `DryRunGenerate`
  - `ApplyRun`
  - `RefreshScoreIds`
- `ScoreIds`
- `OnlyUngrouped`
- `OnlyChangedProfiles`
- `OnlyMissingEmbeddings`
- `DryRun`
- `RunIdToApply`
- `ThresholdProfile`
- `EmbeddingModelName`
- `MaxScores`
- `ForceRebuild`
- `SubsetDescription`

## Recommended workflow modes

### 1. Profile refresh only

Use when:
- normalization rules changed
- grouping profile schema changed
- embeddings are not required yet

Output:
- refreshed `ScoreGroupingProfile`

### 2. Missing embeddings only

Use when:
- profiles exist
- embeddings missing or stale

Output:
- refreshed `ScoreEmbeddingVector`

### 3. Dry-run generate

Use when:
- you want candidate groups
- you do not want canonical groups changed yet

Output:
- `SongGroupingRun`
- run preview tables
- statistics and diagnostics

### 4. Apply run

Use when:
- dry run was reviewed
- selected proposals are approved

Output:
- `SongGroup`
- `SongGroupMembership`
- `IndexedScore.SongGroupId` sync
- derived group tags sync

### 5. Refresh selected scope

Use when:
- manual corrections made
- specific scores changed
- targeted reevaluation needed

Output:
- minimal recomputation

## Idempotency rules

Required:
- profile refresh rerun should not duplicate rows
- embedding refresh rerun should skip unchanged rows
- dry-run rerun should produce a new run, not overwrite prior run silently
- apply should be repeat-safe for the same run
- manual locks must persist across reruns

## Checkpointing

Reuse the existing task cursor pattern.
Grouping tasks can checkpoint by:
- selected score cursor
- current block index
- current run group index
- apply progress index

## Failure handling

If grouping fails mid-run:
- the run should be marked failed,
- partial preview rows may remain for debugging,
- canonical `SongGroup` tables should only be changed during explicit apply mode.

If apply fails mid-run:
- use transactional batching,
- record which groups were applied,
- allow resume or safe retry.

## Recommended transactional boundaries

### Profile refresh
- batch transaction every N scores

### Embedding refresh
- batch transaction every N embeddings

### Dry run
- batch-insert preview rows

### Apply
- transaction per cluster or small cluster batch
- update cached `SongGroupId` after membership writes

## Derived tag sync policy

If group tags are retained for compatibility, sync should happen:
- after successful apply,
- not during dry run,
- from canonical membership tables only.

Recommended tag projection:
- primary exact group:
  - `group:<GroupKey>`
- optional future typed tags:
  - `group_exact:<GroupKey>`
  - `group_family:<GroupKey>`

Phase 1 recommendation:
- keep one compatibility tag shape:
  - `group:<GroupKey>`
- expose richer type in canonical tables and UI.

## Rollout phases

### Phase A: schema + profiles
- add tables
- generate profiles
- no user-visible behavior change yet

### Phase B: dry-run engine
- generate run proposals
- add review UI
- no canonical group apply yet on real DB

### Phase C: apply flow on copied DB
- validate on copied real data
- tune thresholds
- fix obvious issues

### Phase D: controlled production use
- use safe scope
- apply reviewed runs
- monitor suspicious groups

### Phase E: broader automation
- only after the validator path is stable

## Manual edit overwrite rule

Pipeline apply must never:
- remove locked manual memberships,
- replace curated canonical display values without review,
- merge locked groups automatically.

## Operational shortcuts worth adding to UI

- `Build missing profiles`
- `Build missing embeddings`
- `Run dry-run for ungrouped only`
- `Run dry-run for changed scores only`
- `Apply reviewed run`
- `Re-evaluate this group`
- `Re-evaluate selected scores`

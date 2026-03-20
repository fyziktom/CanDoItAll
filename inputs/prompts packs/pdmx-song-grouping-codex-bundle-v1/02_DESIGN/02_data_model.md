# Data Model Proposal

## Recommended design summary

Use:

- existing `SongGroup` as the canonical group entity,
- **new** `SongGroupMembership` as canonical many-to-many membership,
- **new** `ScoreGroupingProfile` as one-to-one normalization/extraction state,
- **new** `ScoreEmbeddingVector` as one-to-one or one-to-many embedding storage,
- **new** `SongGroupingRun` and run-preview tables for dry-run review.

Keep for compatibility:
- `IndexedScore.SongGroupId` as cached primary-group pointer for catalog/detail summaries.

## Why not tags-only

Tags-only grouping would be weak for:
- referential integrity,
- multi-group semantics,
- confidence storage,
- manual merge/split workflows,
- evidence storage,
- rollback and rerun audit.

Tags are useful for:
- compatibility,
- quick search/filter projection,
- downstream exposure.

Therefore tags should be **derived**, not canonical.

## Entity proposal

## 1. `ScoreGroupingProfile`

Purpose:
- store grouping-oriented normalized and structured fields separately from raw source data.

Proposed fields:

- `IndexedScoreId`
- `PipelineVersion`
- `NormalizationVersion`
- `NormalizedTitleLoose`
- `NormalizedTitleStrict`
- `NormalizedComposerLoose`
- `NormalizedComposerStrict`
- `ComposerSurnameKey`
- `ComposerForenameKey`
- `CatalogTokensCsv`
- `PrimaryCatalogSystem`
- `PrimaryCatalogValue`
- `OpusNumber`
- `WorkNumber`
- `MovementNumber`
- `MovementLabel`
- `KeySignatureKey`
- `WorkTypeKey`
- `WorkSignatureLoose`
- `WorkSignatureStrict`
- `AliasTitlesJson`
- `AliasComposersJson`
- `EmbeddingInputText`
- `UpdatedUtc`

Notes:
- do not over-normalize raw source values,
- keep display/raw values on `IndexedScore`,
- use the profile for search and grouping logic.

## 2. `ScoreEmbeddingVector`

Purpose:
- persist embeddings so they are not recomputed unless input/model changes.

Proposed fields:

- `IndexedScoreId`
- `EmbeddingKind`
  - suggested initial values:
    - `Work`
    - `WorkNoComposer`
    - optional later:
      - `DescriptionAux`
- `ModelName`
- `VectorDimensions`
- `InputHash`
- `VectorBlob`
- `QuantizationKind`
- `UpdatedUtc`

Notes:
- prefer `byte[]`/BLOB over JSON for vectors,
- JSON vector storage will bloat the DB quickly,
- only regenerate when `InputHash` or `ModelName` changes.

## 3. `SongGroup`

Keep existing entity but extend it.

Proposed fields:

- `Id`
- `GroupKey`
- `GroupType`
  - recommended initial values:
    - `ExactWork`
    - `WorkFamily`
  - future:
    - `Arrangement`
    - `Excerpt`
- `DisplayTitle`
- `DisplayComposer`
- `NormalizedDisplayTitle`
- `NormalizedDisplayComposer`
- `CanonicalIndexedScoreId`
- `MemberCount`
- `IsReviewed`
- `ReviewState`
- `Source`
  - `Auto`
  - `Manual`
  - `Hybrid`
- `ConfidenceSummary`
- `SearchAliasesJson`
- `Notes`
- `CreatedUtc`
- `UpdatedUtc`
- `RowVersion`

Notes:
- `DisplayTitle`/`DisplayComposer` are canonical display choices,
- they should not always be “first member inserted”.

## 4. `SongGroupMembership`

This is the most important structural addition.

Proposed fields:

- `Id`
- `IndexedScoreId`
- `SongGroupId`
- `MembershipRole`
  - `Primary`
  - `Secondary`
  - `Related`
- `MembershipSource`
  - `Manual`
  - `Auto`
  - `RunApply`
  - `Imported`
- `ConfidenceScore`
- `ConfidenceBand`
- `ReasonSummary`
- `ReasonJson`
- `IsLocked`
- `IsHidden`
- `CreatedUtc`
- `UpdatedUtc`
- `RowVersion`

Constraints:
- unique `(IndexedScoreId, SongGroupId, MembershipRole)` or unique `(IndexedScoreId, SongGroupId)` depending on final model,
- index on `SongGroupId`,
- index on `IndexedScoreId`,
- index on `ConfidenceBand`.

## 5. `SongGroupingRun`

Purpose:
- represent one explicit grouping job or evaluation run.

Proposed fields:

- `Id`
- `RunKind`
  - `DryRun`
  - `Apply`
  - `ProfileRefresh`
  - `EmbeddingRefresh`
- `ScopeDescription`
- `RequestedBy`
- `RequestedUtc`
- `CompletedUtc`
- `Status`
- `NormalizationVersion`
- `EmbeddingModel`
- `ThresholdProfile`
- `ScoreCount`
- `CandidatePairCount`
- `ProposedGroupCount`
- `AppliedGroupCount`
- `AutoAcceptedCount`
- `ReviewRequiredCount`
- `RejectedCount`
- `StatsJson`
- `Error`
- `CursorJson`

## 6. Run preview tables

Recommended because dry-run review is a hard requirement.

### `SongGroupingRunGroup`

- `Id`
- `RunId`
- `ProposedGroupKey`
- `GroupType`
- `DisplayTitle`
- `DisplayComposer`
- `MemberCount`
- `ConfidenceSummary`
- `ReviewState`
- `SummaryJson`

### `SongGroupingRunMember`

- `Id`
- `RunGroupId`
- `IndexedScoreId`
- `IsPrimaryCandidate`
- `ConfidenceScore`
- `ReasonSummary`
- `ReasonJson`
- `Disposition`
  - `AutoAccepted`
  - `NeedsReview`
  - `Rejected`

This mirrors the eventual applied state and makes UI review simpler.

## 7. `ScoreReview` additions

Recommended additions:

- keep `ManualGroupKey`
  - reinterpret as manual exact-work override / explicit requested primary group
- add `GroupingLockMode`
  - `None`
  - `ProtectManual`
  - `DoNotAutoAssign`
- add `GroupingCuratorNote`

Why:
- this preserves the existing review-centered curation model,
- gives reviewers a way to stop future automation from undoing corrections.

## Compatibility strategy for `IndexedScore`

Keep:
- `SongGroupId`
- `SongGroup`

New meaning:
- cached primary exact-work group
- derived from `SongGroupMembership`

Migration rule:
- existing `SongGroupId` should be lifted into memberships during migration.

## Canonical display-title selection strategy

Recommended order:
1. manually curated canonical group values
2. member explicitly marked representative
3. highest metadata-quality member
4. most common structured normalized title+composer form
5. fallback lexical choice

Metadata-quality ranking suggestion:
- has title,
- has composer,
- has catalog tokens,
- has MXL,
- reviewed/selected/export-ready,
- richer raw text length without looking noisy.

## Indexing strategy

Add indexes for:
- profile strict/loose signatures,
- composer surname key,
- primary catalog system/value,
- movement number,
- key signature,
- group memberships,
- run status.

## Minimal viable schema path

If Codex must keep scope tight, the MVP schema should still include:
- `ScoreGroupingProfile`
- `SongGroupMembership`
- `SongGroupingRun`
- `SongGroupingRunGroup`
- `SongGroupingRunMember`

`ScoreEmbeddingVector` may be deferred slightly if implemented in the same milestone but should still land in phase 1 if embeddings are part of the deliverable.

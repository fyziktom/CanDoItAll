# Memory Taxonomy And Data Model

## Memory Kinds

| Kind | Meaning | Examples |
|---|---|---|
| `Source` | Raw source-derived item | mindmap node, file chunk, email, repo file, workflow event |
| `Working` | Active short-lived context | current process goal, active task constraints |
| `Episodic` | What happened | agent run failed, workflow completed, user approved plan |
| `Semantic` | Stable conceptual knowledge | Docker deployment options, CanDoItAll plugin model |
| `Procedural` | How to do something | run test Docker simulation, prepare Codex bundle |
| `Decision` | Why a choice was made | test Docker separate from production Docker |
| `Reflection` | Lessons learned | agent skipped proof; need stricter validation |
| `Metacognitive` | What the system knows about its knowledge | source coverage, unknowns, stale areas |
| `Policy` | Governance and constraints | approval required for destructive tool use |
| `Capability` | Who/tool can do what | agent role skill, plugin capability, MAF tool availability |

## Projection Types

| Projection type | Purpose |
|---|---|
| `AtomicSource` | Exact source item projection. |
| `CanonicalItem` | Cleaned source-grounded meaning. |
| `LocalCluster` | Spatial/graph cluster inside one mindmap/project. |
| `SemanticTopic` | Topic-level semantic grouping. |
| `ProjectSummary` | Project-level canonical summary. |
| `CrossProjectTopic` | Reusable topic across projects. |
| `Procedure` | Actionable procedure/runbook. |
| `Decision` | Architectural/process decision. |
| `Episode` | Historical event. |
| `Reflection` | Lesson and improvement candidate. |

## Core Entities

### `MemorySourceRecord`

Represents a source container: project structure, file tree, repo, email account, workflow run store, process run store, plugin stream.

Key fields:

- `Id`
- `ProjectId`
- `SourceType`
- `SourceKey`
- `DisplayName`
- `SourceUri`
- `ConnectorId`
- `IsEnabled`
- `LastScanStartedAtUtc`
- `LastScanCompletedAtUtc`
- `LastSourceVersion`
- `LastContentHash`

### `MemorySourceItemRecord`

Represents one source item.

Key fields:

- `Id`
- `SourceId`
- `ProjectId`
- `SourceType`
- `SourceItemKey`
- `ParentSourceItemKey`
- `Title`
- `ContentSummary`
- `ContentHash`
- `SourceVersion`
- `StorageReferenceJson`
- `CoordinatesJson`
- `MetadataJson`
- `DetectedAtUtc`
- `UpdatedAtUtc`

### `CanonicalMemoryItemRecord`

Represents canonical meaning extracted from sources.

Key fields:

- `Id`
- `ProjectId`
- `MemoryKind`
- `ProjectionType`
- `Title`
- `Summary`
- `CanonicalText`
- `Scope`
- `TopicKey`
- `TagsJson`
- `EntitiesJson`
- `ConfidenceScore`
- `HumanValidationStatus`
- `SourceRefsJson`
- `CurrentState`
- `SupersededById`
- `CreatedAtUtc`
- `UpdatedAtUtc`

### `MemoryRelationRecord`

Represents typed associations.

Relation kinds:

- `Contains`
- `DerivedFrom`
- `Uses`
- `DependsOn`
- `Supports`
- `Contradicts`
- `Supersedes`
- `SimilarTo`
- `SemanticallyRelated`
- `SpatiallyNear`
- `GraphNear`
- `SemanticallyRelatedContextSeparated`
- `ProcedureFor`
- `DecisionFor`
- `EpisodeFor`
- `MentionedBy`
- `ValidatedBy`
- `FailedBecauseOf`

Key fields:

- `Id`
- `SourceMemoryItemId`
- `TargetMemoryItemId`
- `RelationKind`
- `Weight`
- `ConfidenceScore`
- `EvidenceJson`
- `Reason`
- `CreatedBy`
- `CreatedAtUtc`

### `MemoryActivationRecord`

Represents salience and retrieval dynamics.

Key fields:

- `MemoryItemId`
- `ActivationScore`
- `ImportanceScore`
- `RecencyScore`
- `UsageCount`
- `LastUsedAtUtc`
- `HumanValidationBoost`
- `RiskBoost`
- `FailureImpactBoost`
- `StalenessPenalty`
- `ContradictionPenalty`
- `DormantUntilUtc`

### `MemoryProjectionRecord`

Represents a Qdrant or search projection.

Key fields:

- `Id`
- `MemoryItemId`
- `ProjectionStoreKind`
- `CollectionName`
- `PointId`
- `VectorName`
- `ProjectionType`
- `EmbeddingProvider`
- `EmbeddingModel`
- `EmbeddingDimensions`
- `EmbeddingProfile`
- `ProjectionVersion`
- `SourceHash`
- `PayloadHash`
- `State`
- `CreatedAtUtc`
- `UpdatedAtUtc`

### `MemoryRecallTraceRecord`

Represents how a recall decision happened.

Key fields:

- `Id`
- `ProjectId`
- `RunId`
- `AgentId`
- `RequestJson`
- `Intent`
- `CandidateSummaryJson`
- `SelectedItemIdsJson`
- `ExcludedItemIdsJson`
- `ContextPackStorageReferenceJson`
- `OutcomeFeedback`
- `CreatedAtUtc`

### `MemoryConsolidationRunRecord`

Represents a sleep-cycle run.

Key fields:

- `Id`
- `ProjectId`
- `TriggerKind`
- `Mode`
- `StartedAtUtc`
- `CompletedAtUtc`
- `Status`
- `InputScopeJson`
- `ChangedSourceCount`
- `CreatedItemCount`
- `UpdatedItemCount`
- `CreatedRelationCount`
- `ProjectionUpdateCount`
- `HumanReviewTaskCount`
- `ReportStorageReferenceJson`

## Human Validation States

```text
Unreviewed
MachineHighConfidence
HumanValidated
HumanRejected
NeedsReview
Stale
Superseded
Contradicted
Dormant
```

## Memory State Machine

```text
DraftCandidate
  -> Active
  -> NeedsReview
  -> HumanValidated
  -> Superseded
  -> Dormant
  -> RetiredProjectionOnly
```

Raw source items remain available even if canonical/projection records become dormant or superseded.

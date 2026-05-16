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
| `KnowledgeRegion` | Topic/subtopic area used for coverage and gap modeling. |
| `KnowledgeCoverage` | Rebuildable projection of durable coverage map state. |
| `LearningOpportunity` | Searchable projection of approved/draft learning proposals, never source truth. |

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
- `HasKnowledgeGap`
- `NeedsEvidence`
- `SupportsLearningProposal`
- `ProbedBy`
- `ImprovedByLearning`
- `AlignedWithProjectDirection`
- `RaisesQuestion`

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

## Epistemic Drive Entities

Epistemic Drive records are metacognitive memory. They describe what the system knows about its own knowledge state. They must preserve vector components and evidence refs; a scalar priority may exist only as a secondary display/sorting field.

### `KnowledgeRegionRecord`

Represents a topic region or subregion such as `Docker`, `Docker.Networking`, or `Docker.Compose.NonHappyPaths`.

Key fields:

- `Id`
- `ProjectId`
- `ParentRegionId`
- `TopicKey`
- `DisplayName`
- `Scope`
- `TagsJson`
- `MetadataJson`
- `CreatedAtUtc`
- `UpdatedAtUtc`

### `ProjectDirectionVectorRecord`

Represents an active project direction derived from project graph, mindmap, process/workflow needs, roadmap sources, or explicit user priorities.

Key fields:

- `Id`
- `ProjectId`
- `DirectionKey`
- `DisplayName`
- `StrategicWeight`
- `RiskWeight`
- `TimeHorizonWeight`
- `SourceMemoryItemIdsJson`
- `MetadataJson`
- `CreatedAtUtc`
- `UpdatedAtUtc`

### `KnowledgeCoverageMapRecord`

Represents coverage/confidence/staleness/risk state for a region and its subregions.

Key fields:

- `Id`
- `ProjectId`
- `RootKnowledgeRegionId`
- `CoverageVersion`
- `SubregionsJson`
- `EvidenceRefsJson`
- `AlgorithmVersion`
- `InputHash`
- `CalculatedAtUtc`

### `KnowledgeGapRecord`

Represents a specific weak region, not just a generic topic.

Key fields:

- `Id`
- `ProjectId`
- `KnowledgeRegionId`
- `GapKey`
- `Title`
- `Description`
- `Severity`
- `ConfidenceWeakness`
- `CoverageWeakness`
- `EvidenceRefsJson`
- `CreatedAtUtc`
- `UpdatedAtUtc`

### `KnowledgeNeedVectorRecord`

Represents the multi-dimensional state used by Epistemic Drive. Store each core dimension as a typed numeric field so the model can be queried, explained, and tested.

Key fields:

- `Id`
- `ProjectId`
- `KnowledgeRegionId`
- `UsageFrequency`
- `ConfidenceWeakness`
- `RiskImpact`
- `Staleness`
- `FailureRecurrence`
- `StrategicAlignment`
- `QuestionDensity`
- `BusinessValue`
- `EstimatedLearningEffort`
- `SourceAvailability`
- `SourceQuality`
- `ContradictionPressure`
- `UserInterestSignal`
- `Volatility`
- `ExpectedReuse`
- `AlgorithmVersion`
- `CalculatedAtUtc`

### `EpistemicTensionRecord`

Represents the evaluated tension and candidate classification for one region.

Key fields:

- `Id`
- `ProjectId`
- `KnowledgeRegionId`
- `KnowledgeNeedVectorId`
- `Category`
- `ParetoRank`
- `DisplayPriorityScore`
- `LearningRoiEstimateJson`
- `IntersectingProjectDirectionIdsJson`
- `EvidenceRefsJson`
- `Explanation`
- `AlgorithmVersion`
- `CalculatedAtUtc`

`DisplayPriorityScore` is optional and secondary. It must not be the only stored decision basis.

### `LearningProposalRecord`

Represents a human-reviewable proposal to improve knowledge.

Key fields:

- `Id`
- `ProjectId`
- `KnowledgeRegionId`
- `Topic`
- `Summary`
- `KnowledgeNeedVectorId`
- `Category`
- `CoverageMapId`
- `EvidenceRefsJson`
- `RelatedProjectDirectionIdsJson`
- `SuggestedSourcesJson`
- `SourceTrustSummary`
- `EstimatedEffort`
- `SuggestedOutputsJson`
- `SuggestedAcceptanceCriteriaJson`
- `SuggestedProbingQuestionSetId`
- `ProposedDepth`
- `RiskSummary`
- `RequiresHumanApproval`
- `State`
- `SnoozedUntilUtc`
- `DecisionAuditJson`
- `CreatedAtUtc`
- `UpdatedAtUtc`

### `LearningTaskRecord`

Represents approved learning work assigned to a human or agent.

Key fields:

- `Id`
- `ProjectId`
- `ProposalId`
- `Title`
- `Scope`
- `ApprovedSourcesJson`
- `ExpectedOutputsJson`
- `AcceptanceCriteriaJson`
- `AssignedTo`
- `State`
- `InputHash`
- `StartedAtUtc`
- `CompletedAtUtc`
- `ReportStorageReferenceJson`

### `LearningOutcomeRecord`

Represents the auditable output from a learning task.

Key fields:

- `Id`
- `ProjectId`
- `LearningTaskId`
- `Summary`
- `DraftMemoryItemIdsJson`
- `DraftProcedureItemIdsJson`
- `ProbingQuestionSetIdsJson`
- `SourceEvidenceRefsJson`
- `QaFindingsJson`
- `ValidationState`
- `CreatedAtUtc`

### `OpenQuestionSetRecord`

Represents unresolved questions for a knowledge region.

Key fields:

- `Id`
- `ProjectId`
- `KnowledgeRegionId`
- `QuestionsJson`
- `EvidenceRefsJson`
- `UpdatedAtUtc`

### `ProbingQuestionSetRecord`

Represents generated questions used before or after learning.

Key fields:

- `Id`
- `ProjectId`
- `KnowledgeRegionId`
- `QuestionsJson`
- `Purpose`
- `EvidenceRefsJson`
- `CreatedAtUtc`

## Evidence Reference Model

Epistemic records should store typed evidence refs rather than embedding source-specific payloads directly.

Required evidence fields:

- `EvidenceKind`
- `EvidenceId`
- `Summary`
- `Weight`
- `ObservedAtUtc`
- `MetadataJson`

Allowed evidence kinds include:

- `RecallTrace`
- `WorkflowRun`
- `ProcessRun`
- `SourceItem`
- `CanonicalMemoryItem`
- `Contradiction`
- `ProbingSession`
- `UserCorrection`
- `ProjectDirection`
- `HumanReviewItem`

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

## Probing And Calibration Entities

Add these durable records to the Cognitive Memory model:

| Entity | Purpose |
|---|---|
| `MemoryProbeSessionRecord` | One interactive probing conversation scoped by project, user/agent, mode, and purpose. |
| `MemoryProbeTurnRecord` | One question/answer/correction/challenge turn with recall trace and answer metadata. |
| `MemoryProbeFeedbackRecord` | User or evaluator feedback on a turn. |
| `MemoryProbeFindingRecord` | Classified outcome such as missing knowledge, wrong scope, contradiction, or overconfidence. |
| `MemoryUserCorrectionRecord` | User-provided correction evidence with risk classification and review state. |
| `MemoryConfidenceCalibrationRecord` | Confidence-vs-outcome evidence used to tune recall scoring and answer confidence. |
| `MemoryRegressionTestCaseRecord` | Durable replayable test created from a probe failure or important question. |
| `MemoryRegressionTestRunRecord` | Result of replaying a memory regression test. |

These entities are metacognitive/audit artifacts. They can influence activation, confidence, coverage, and Epistemic Drive through explicit evidence. They cannot directly become source truth.

## Neuro-Cognitive Data Model Additions

The neuro-cognitive patch adds lower-level belief/control records that must exist before source ingestion and recall phases promote durable memory semantics.

| Entity | Purpose |
|---|---|
| `MemoryEvidenceAnchorRecord` | Fine-grained source anchor with source item, storage locator, structured path, text span, quote hash, trust level, redaction state, and source hash/version. |
| `MemoryClaimRecord` | Atomic claim with context frames, temporal validity, confidence, validation state, source evidence, attack/support evidence, and revision lineage. |
| `MemoryBeliefStateRecord` | Derived belief state for a claim: supported, contested, contradicted, stale, scope-limited, validated, rejected, or superseded. |
| `MemoryMutationCommandRecord` | Idempotent command submitted to mutation authority for authoritative changes. |
| `MemoryMutationAuditRecord` | Audit event recording actor, policy, evidence, version token, review decision, and projection invalidation. |
| `EntityRegistryRecord` | Entity and alias registry with source evidence and context-frame links. |
| `ContextFrameRecord` | Typed project/environment/runtime/process/role/time/source-trust/risk/access frame that controls merge and recall substitution. |
| `CognitiveWorkspaceFrameRecord` | Active working-memory frame for user, agent, workflow, process, probe, review, or learning task. |
| `WorkingMemorySlotRecord` | Focus slot referencing memory item, claim, procedure skill, source item, probe turn, workflow artifact, or open question. |
| `AttentionDecisionRecord` | Explainable route decision: recall, answer, clarify, source audit, probe, review, learning proposal, replay, or abstain. |
| `CognitiveSignalRecord` | Durable signal vector event for novelty, surprise, risk, usefulness, reward, rework, contradiction pressure, user interest, strategic alignment, staleness, source weakness, or calibration risk. |
| `PredictionExpectationRecord` | Expected claim, source sufficiency, procedure outcome, validation result, context boundary, or confidence range. |
| `PredictionErrorRecord` | Observed mismatch with kind, magnitude, cause hypothesis, evidence anchors, related claims/procedures, and suggested action. |
| `TemporalEpisodeRecord` | Ordered bounded experience with actors, goal, context frames, steps, decisions, artifacts, outcomes, prediction errors, claims, and procedures. |
| `EpisodeStepRecord` | Ordered step within an episode with actor, action kind, inputs, outputs, evidence, success/failure, and related claims/procedures. |
| `MemoryReplayJobRecord` | Scheduled replay/rehearsal job for claim rehearsal, probe regression, procedure validation, source anchor refresh, contradiction resolution, spaced recall, or context-boundary drills. |
| `ProcedureSkillRecord` | Validated skill graph with preconditions, steps, postconditions, failure modes, validation evidence, maturity, risk, and automation binding. |
| `ProcedureFailureModeRecord` | First-class failure condition with detection signal, cause, mitigation, rollback/compensation, prediction-error links, and confidence. |
| `ProcedureSimulationRecord` | Speculative simulation/analogy output that remains hypothesis until source-backed and reviewed. |
| `MetamemoryGateDecisionRecord` | Answer-time decision to answer, warn, clarify, source-audit, probe, review, request learning, or abstain. |

`MemoryItem` remains a chunk/container and projection surface. It may summarize one or more claims, but unresolved contradiction, weak support, stale scope, or context boundaries must remain visible at claim level.

Authoritative writes to claims, memory items, relations, procedure skills, belief state transitions, and projection invalidation must flow through mutation authority. Repository-style upserts may exist internally but are not the public architecture boundary.

## Relational Modeling Corrections

The field lists above intentionally name several `*Json` payloads as compact storage references. That must not be interpreted as permission to hide query-critical relationships or state only inside JSON.

Use relational rows and indexes for data that must be filtered, joined, reviewed, replayed, audited, or updated independently:

- source references for memory items,
- evidence references for relations, gaps, proposals, probes, learning outcomes, and review items,
- selected and excluded recall candidates,
- relation endpoints and relation kinds,
- projection profile/state/source hash/payload hash,
- review item state, risk, target item, and decision,
- proposal/task/outcome state and approval decisions,
- probe session/turn/feedback/finding/correction/regression/calibration links,
- evidence anchors, claim support/attack links, claim revision lineage, belief state, mutation command/audit state,
- entity aliases, context frame dimensions, workspace slots, inhibited candidates, attention decisions,
- prediction expectation/error links, cognitive signal vectors, episode steps, replay job targets, procedure maturity/failure modes, answer-gate decisions,
- required and forbidden regression memory refs.

JSON remains acceptable for:

- supplemental connector metadata,
- small versioned request snapshots,
- immutable report summaries,
- display-only score breakdowns,
- optional provider-specific payload fragments,
- storage references to large artifacts.

Every JSON payload that can affect behavior must have:

- a schema/profile version,
- source-generated or cached `System.Text.Json` serialization options,
- size budget and storage/IPFS offload threshold,
- redaction classification before persistence or external export,
- tests that reject missing required fields for the active schema version.

## Required EF Query And Index Shape

The implementation must define indexes before large data arrives. At minimum:

- `MemorySourceRecord`: `(ProjectId, SourceType, SourceKey)` unique, `(ProjectId, IsEnabled)`, `(LastContentHash)`.
- `MemorySourceItemRecord`: `(ProjectId, SourceId, SourceItemKey)` unique, `(ProjectId, SourceType, ContentHash)`, `(ParentSourceItemKey)`, `(UpdatedAtUtc)`.
- `CanonicalMemoryItemRecord`: `(ProjectId, MemoryKind, CurrentState)`, `(ProjectId, TopicKey)`, `(HumanValidationStatus)`, `(SupersededById)`, `(UpdatedAtUtc)`.
- `MemorySourceRefRecord`: `(MemoryItemId)`, `(SourceItemId)`, `(ProjectId, SourceType, SourceItemKey)`.
- `MemoryRelationRecord`: `(ProjectId, SourceMemoryItemId, RelationKind)`, `(ProjectId, TargetMemoryItemId, RelationKind)`, `(ProjectId, RelationKind)`.
- `MemoryActivationRecord`: `(ProjectId, ActivationScore)`, `(ProjectId, LastUsedAtUtc)`, `(DormantUntilUtc)`.
- `MemoryProjectionRecord`: `(MemoryItemId, ProjectionStoreKind, ProjectionVersion, EmbeddingProfile)` unique, `(ProjectId, CollectionName, State)`, `(SourceHash)`, `(PayloadHash)`.
- `MemoryRecallTraceRecord`: `(ProjectId, CreatedAtUtc)`, `(RunId)`, `(AgentId)`, `(Intent)`.
- `MemoryConsolidationRunRecord`: `(ProjectId, Status, StartedAtUtc)`, `(TriggerKind, Mode)`.
- Epistemic records: `(ProjectId, KnowledgeRegionId)`, `(ProjectId, State)`, `(ProjectId, CalculatedAtUtc)`, and unique idempotency keys for scan/proposal inputs.
- Probing records: `(ProjectId, SessionId, CreatedAtUtc)`, `(TurnId)`, `(RecallTraceId)`, `(FeedbackAction)`, `(RegressionTestState)`.
- Neuro-cognitive foundation records: evidence anchors `(ProjectId, SourceItemId, AnchorKind)`, claims `(ProjectId, ClaimKind, ValidationState)`, claim/context links `(ClaimId, ContextFrameId)`, support/attack evidence `(ClaimId, EvidenceAnchorId, Direction)`, mutation commands `(ProjectId, IdempotencyKey)` unique, mutation audits `(ProjectId, CreatedAtUtc, ActorKind)`.
- Entity/context records: entities `(ProjectId, EntityKind, CanonicalName)`, aliases `(ProjectId, AliasNormalized)`, context frames `(ProjectId, FrameKind)`, context dimensions `(ContextFrameId, DimensionKind, Value)`.
- Workspace/attention records: workspace frames `(ProjectId, FrameKind, ExpiresAtUtc)`, focus slots `(WorkspaceFrameId, Kind)`, inhibited candidates `(WorkspaceFrameId, CandidateId)`, attention decisions `(ProjectId, WorkspaceFrameId, CreatedAtUtc, DecisionKind)`.
- Prediction/signal/replay/procedure records: prediction errors `(ProjectId, ErrorKind, ObservedAtUtc)`, cognitive signals `(ProjectId, SignalKind, ObservedAtUtc)`, episodes `(ProjectId, EpisodeKind, StartedAtUtc)`, episode steps `(EpisodeId, SequenceIndex)` unique, replay jobs `(ProjectId, JobKind, State, ScheduledAtUtc)`, procedure skills `(ProjectId, Maturity, RiskLevel)`, answer-gate decisions `(ProjectId, WorkspaceFrameId, CreatedAtUtc, DecisionKind)`.

Read paths for dashboards, review queues, recall traces, source items, projections, proposals, and probes must be paged and must project to DTOs. Do not load full entity graphs with broad `Include` chains for operator screens or recall hot paths.

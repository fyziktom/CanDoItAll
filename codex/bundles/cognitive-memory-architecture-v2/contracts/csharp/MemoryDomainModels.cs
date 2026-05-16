using System;
using System.Collections.Generic;

namespace CanDoItAll.CognitiveMemory.Abstractions;

public enum MemoryType
{
    Source = 0,
    Working = 1,
    Episodic = 2,
    Semantic = 3,
    Procedural = 4,
    Decision = 5,
    Reflection = 6,
    Metacognitive = 7
}

public enum MemoryValidationState
{
    Draft = 0,
    MachineGenerated = 1,
    NeedsHumanReview = 2,
    HumanReviewed = 3,
    Approved = 4,
    Superseded = 5,
    Retired = 6,
    Rejected = 7
}

public enum MemoryStabilityState
{
    Unknown = 0,
    Experimental = 1,
    Active = 2,
    Stable = 3,
    Dormant = 4,
    Stale = 5,
    Deprecated = 6
}

public enum MemoryRelationType
{
    SameAs = 0,
    Refines = 1,
    Supersedes = 2,
    Contradicts = 3,
    Supports = 4,
    DependsOn = 5,
    Causes = 6,
    SimilarTo = 7,
    ContextuallyContains = 8,
    SemanticallyRelatedButContextSeparated = 9,
    ProcedureUses = 10,
    DecisionJustifies = 11,
    EpisodeProduced = 12
}

public sealed record MemoryItem(
    Guid Id,
    Guid ProjectId,
    MemoryType Type,
    string Title,
    string CanonicalText,
    string? SummaryText,
    ScoreVectorSnapshot ConfidenceVector,
    ScoreVectorSnapshot ActivationVector,
    ScoreScalarProjection? DisplayConfidence,
    ScoreScalarProjection? DisplayActivation,
    MemoryValidationState ValidationState,
    MemoryStabilityState StabilityState,
    IReadOnlyList<MemorySourceRef> SourceRefs,
    MemoryItemMetadata Metadata,
    string ContentHash,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record MemoryItemMetadata(
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Entities,
    IReadOnlyList<string> Scopes,
    string? OwnerAgentId,
    string? AlgorithmVersion,
    string? EmbeddingProfile,
    string? AccessScope,
    IReadOnlyDictionary<string, string> Properties);

public sealed record MemorySourceRef(
    Guid SourceManifestId,
    Guid SourceItemId,
    string SourceSystem,
    string SourceItemKey,
    string ContentHash,
    string? Locator,
    DateTimeOffset ObservedAtUtc);

public sealed record MemoryRelation(
    Guid Id,
    Guid ProjectId,
    Guid SourceMemoryItemId,
    Guid TargetMemoryItemId,
    MemoryRelationType RelationType,
    ScoreVectorSnapshot RelationVector,
    ScoreScalarProjection? DisplayStrength,
    ScoreScalarProjection? DisplayConfidence,
    IReadOnlyList<RelationEvidence> Evidence,
    string AlgorithmVersion,
    DateTimeOffset CreatedAtUtc);

public sealed record RelationEvidence(
    string EvidenceType,
    string Summary,
    ScoreComponent WeightComponent,
    IReadOnlyDictionary<string, string> Properties);

public sealed record MemoryItemQuery(
    Guid? ProjectId,
    IReadOnlyList<MemoryType> Types,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Scopes,
    IReadOnlyList<MemoryValidationState> AllowedValidationStates,
    IReadOnlyList<MemoryValidationState> ExcludedValidationStates,
    bool RequireHumanApprovedForHighRisk,
    bool AllowDraftOnlyForReviewMode,
    int Limit);

public sealed record MemoryRelationQuery(
    Guid? ProjectId,
    Guid? MemoryItemId,
    IReadOnlyList<MemoryRelationType> RelationTypes,
    int Limit);

using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemoryEvidenceAnchorRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ProjectId { get; set; }

    public CognitiveMemoryEvidenceAnchorKind AnchorKind { get; set; } = CognitiveMemoryEvidenceAnchorKind.TextSpan;

    public Guid? SourceManifestId { get; set; }

    public Guid? SourceItemId { get; set; }

    public string SourceSystem { get; set; } = string.Empty;

    public string Locator { get; set; } = string.Empty;

    public string StructuredPath { get; set; } = string.Empty;

    public int? TextStart { get; set; }

    public int? TextEnd { get; set; }

    public string QuoteHash { get; set; } = string.Empty;

    public CognitiveMemorySourceTrustLevel TrustLevel { get; set; } = CognitiveMemorySourceTrustLevel.Unknown;

    public CognitiveMemoryRedactionState RedactionState { get; set; } = CognitiveMemoryRedactionState.Unclassified;

    public CognitiveMemoryHashAlgorithm SourceHashAlgorithm { get; set; } = CognitiveMemoryHashAlgorithm.Sha256;

    public string SourceHash { get; set; } = string.Empty;

    public DateTimeOffset ObservedAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryClaimRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ProjectId { get; set; }

    public Guid? MemoryRecordId { get; set; }

    public CognitiveMemoryClaimKind ClaimKind { get; set; } = CognitiveMemoryClaimKind.Fact;

    public string ClaimText { get; set; } = string.Empty;

    public string SubjectKey { get; set; } = string.Empty;

    public string PredicateKey { get; set; } = string.Empty;

    public string ObjectKey { get; set; } = string.Empty;

    public Guid? PrimaryContextFrameId { get; set; }

    public DateTimeOffset? ValidFromUtc { get; set; }

    public DateTimeOffset? ValidToUtc { get; set; }

    public CognitiveMemoryBeliefStateKind CurrentBeliefState { get; set; } = CognitiveMemoryBeliefStateKind.Unexamined;

    public Guid? CurrentBeliefScoreEvaluationTraceId { get; set; }

    public CognitiveMemoryScoreProjectionBucket CurrentBeliefBucket { get; set; } = CognitiveMemoryScoreProjectionBucket.Unknown;

    public double? DisplayBeliefScore { get; set; }

    public CognitiveMemoryValidationState ValidationState { get; set; } = CognitiveMemoryValidationState.Draft;

    public CognitiveMemoryStabilityState StabilityState { get; set; } = CognitiveMemoryStabilityState.Unknown;

    public Guid? SupersedesClaimId { get; set; }

    public string AlgorithmVersion { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryClaimEvidenceLinkRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ClaimId { get; set; }

    public Guid EvidenceAnchorId { get; set; }

    public CognitiveMemoryEvidenceDirection Direction { get; set; } = CognitiveMemoryEvidenceDirection.Supports;

    public string Explanation { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CognitiveMemoryBeliefStateRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ClaimId { get; set; }

    public CognitiveMemoryBeliefStateKind StateKind { get; set; } = CognitiveMemoryBeliefStateKind.Unexamined;

    public Guid ScoreEvaluationTraceId { get; set; }

    public CognitiveMemoryScoreProjectionBucket ProjectionBucket { get; set; } = CognitiveMemoryScoreProjectionBucket.Unknown;

    public double? DisplayBeliefScore { get; set; }

    public string Explanation { get; set; } = string.Empty;

    public string AlgorithmVersion { get; set; } = string.Empty;

    public DateTimeOffset CalculatedAtUtc { get; set; }
}

public sealed class CognitiveMemoryEntityRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ProjectId { get; set; }

    public CognitiveMemoryEntityKind EntityKind { get; set; } = CognitiveMemoryEntityKind.TechnologyTopic;

    public string CanonicalName { get; set; } = string.Empty;

    public string CanonicalNameKey { get; set; } = string.Empty;

    public Guid? PrimaryContextFrameId { get; set; }

    public Guid? ConfidenceScoreEvaluationTraceId { get; set; }

    public CognitiveMemoryScoreProjectionBucket ConfidenceBucket { get; set; } = CognitiveMemoryScoreProjectionBucket.Unknown;

    public double? DisplayConfidenceScore { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryEntityAliasRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EntityId { get; set; }

    public Guid? ProjectId { get; set; }

    public CognitiveMemoryEntityKind EntityKind { get; set; } = CognitiveMemoryEntityKind.TechnologyTopic;

    public string Alias { get; set; } = string.Empty;

    public string AliasKey { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CognitiveMemoryContextFrameRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ProjectId { get; set; }

    public CognitiveMemoryContextFrameKind FrameKind { get; set; } = CognitiveMemoryContextFrameKind.Composite;

    public string DisplayName { get; set; } = string.Empty;

    public Guid? ConfidenceScoreEvaluationTraceId { get; set; }

    public CognitiveMemoryScoreProjectionBucket ConfidenceBucket { get; set; } = CognitiveMemoryScoreProjectionBucket.Unknown;

    public double? DisplayConfidenceScore { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryContextFrameDimensionRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ContextFrameId { get; set; }

    public Guid? ProjectId { get; set; }

    public CognitiveMemoryContextDimensionKind DimensionKind { get; set; } = CognitiveMemoryContextDimensionKind.Project;

    public string Value { get; set; } = string.Empty;

    public string ValueKey { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CognitiveMemoryContextBoundaryRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ProjectId { get; set; }

    public Guid SourceContextFrameId { get; set; }

    public Guid TargetContextFrameId { get; set; }

    public CognitiveMemoryContextBoundaryKind BoundaryKind { get; set; } = CognitiveMemoryContextBoundaryKind.EnvironmentBoundary;

    public CognitiveMemoryContextBoundaryPolicy BoundaryPolicy { get; set; } = CognitiveMemoryContextBoundaryPolicy.RelatedNotSubstitutable;

    public Guid? ScoreEvaluationTraceId { get; set; }

    public string Explanation { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CognitiveMemoryMutationCommandRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ProjectId { get; set; }

    public CognitiveMemoryMutationCommandKind CommandKind { get; set; } = CognitiveMemoryMutationCommandKind.ProposeClaim;

    public CognitiveMemoryMutationCommandStatus Status { get; set; } = CognitiveMemoryMutationCommandStatus.Accepted;

    public CognitiveMemoryActorKind ActorKind { get; set; } = CognitiveMemoryActorKind.System;

    public string ActorId { get; set; } = string.Empty;

    public string IdempotencyKey { get; set; } = string.Empty;

    public string AffectedMemoryRecordIdsJson { get; set; } = "[]";

    public string AffectedClaimIdsJson { get; set; } = "[]";

    public string EvidenceAnchorIdsJson { get; set; } = "[]";

    public string PayloadJson { get; set; } = "{}";

    public string ExpectedVersionToken { get; set; } = string.Empty;

    public bool RequiresHumanReview { get; set; }

    public string ReviewReason { get; set; } = string.Empty;

    public string ResultVersionToken { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryMutationAuditEventRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MutationCommandId { get; set; }

    public Guid? ProjectId { get; set; }

    public int Sequence { get; set; }

    public CognitiveMemoryMutationAuditEventKind EventKind { get; set; } = CognitiveMemoryMutationAuditEventKind.Submitted;

    public string Message { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}

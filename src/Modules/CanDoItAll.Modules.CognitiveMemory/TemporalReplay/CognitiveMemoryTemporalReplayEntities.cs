using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemoryTemporalEpisodeRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }

    public CognitiveMemoryTemporalEpisodeKind EpisodeKind { get; set; } = CognitiveMemoryTemporalEpisodeKind.Unknown;

    public string Goal { get; set; } = string.Empty;

    public string ExpectedOutcome { get; set; } = string.Empty;

    public string ActualOutcome { get; set; } = string.Empty;

    public string OutcomeSummary { get; set; } = string.Empty;

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? EndedAtUtc { get; set; }

    public DateTimeOffset? FirstStepAtUtc { get; set; }

    public DateTimeOffset? LastStepAtUtc { get; set; }

    public int StepCount { get; set; }

    public int LinkCount { get; set; }

    public string AlgorithmVersion { get; set; } = string.Empty;

    public string MetadataJson { get; set; } = "{}";

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryEpisodeStepRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EpisodeId { get; set; }

    public Guid ProjectId { get; set; }

    public int SequenceIndex { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    public CognitiveMemoryActorKind ActorKind { get; set; } = CognitiveMemoryActorKind.System;

    public string ActorId { get; set; } = string.Empty;

    public CognitiveMemoryEpisodeStepActionKind ActionKind { get; set; } = CognitiveMemoryEpisodeStepActionKind.Unknown;

    public string Summary { get; set; } = string.Empty;

    public string ToolOrPluginKey { get; set; } = string.Empty;

    public bool Succeeded { get; set; } = true;

    public string ErrorCode { get; set; } = string.Empty;

    public string ErrorSummary { get; set; } = string.Empty;

    public string MetadataJson { get; set; } = "{}";

    public DateTimeOffset CreatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryTemporalEpisodeLinkRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EpisodeId { get; set; }

    public Guid ProjectId { get; set; }

    public CognitiveMemoryTemporalEpisodeLinkKind LinkKind { get; set; } = CognitiveMemoryTemporalEpisodeLinkKind.Unknown;

    public Guid? TargetId { get; set; }

    public string TargetKey { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CognitiveMemoryEpisodeStepEvidenceRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid StepId { get; set; }

    public Guid EpisodeId { get; set; }

    public Guid ProjectId { get; set; }

    public CognitiveMemoryEpisodeStepEvidenceRole EvidenceRole { get; set; } = CognitiveMemoryEpisodeStepEvidenceRole.Unknown;

    public Guid EvidenceAnchorId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CognitiveMemoryEpisodeCausalLinkRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EpisodeId { get; set; }

    public Guid ProjectId { get; set; }

    public CognitiveMemoryEpisodeCausalLinkKind LinkKind { get; set; } = CognitiveMemoryEpisodeCausalLinkKind.Unknown;

    public Guid? FromStepId { get; set; }

    public Guid? ToStepId { get; set; }

    public Guid? EvidenceAnchorId { get; set; }

    public Guid? ClaimId { get; set; }

    public Guid? PredictionErrorId { get; set; }

    public Guid? ProcedureSkillId { get; set; }

    public string Summary { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CognitiveMemoryReplayJobRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }

    public CognitiveMemoryReplayJobKind JobKind { get; set; } = CognitiveMemoryReplayJobKind.Unknown;

    public CognitiveMemoryReplayJobState State { get; set; } = CognitiveMemoryReplayJobState.Draft;

    public string Reason { get; set; } = string.Empty;

    public Guid PriorityScoreEvaluationTraceId { get; set; }

    public CognitiveMemoryScoreProjectionBucket PriorityBucket { get; set; } = CognitiveMemoryScoreProjectionBucket.Unknown;

    public double? DisplayPriorityProjection { get; set; }

    public int QueuePriority { get; set; }

    public CognitiveMemoryHashAlgorithm InputHashAlgorithm { get; set; } = CognitiveMemoryHashAlgorithm.Sha256;

    public string InputHash { get; set; } = string.Empty;

    public string ExpectedOutputSchema { get; set; } = string.Empty;

    public string AlgorithmVersion { get; set; } = string.Empty;

    public string PolicyProfileId { get; set; } = string.Empty;

    public string SourceScopeKey { get; set; } = string.Empty;

    public string LeaseToken { get; set; } = string.Empty;

    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }

    public DateTimeOffset? ScheduledAtUtc { get; set; }

    public DateTimeOffset? StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public string FailureCode { get; set; } = string.Empty;

    public string FailureMessage { get; set; } = string.Empty;

    public string MetadataJson { get; set; } = "{}";

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryReplayJobTargetRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ReplayJobId { get; set; }

    public Guid ProjectId { get; set; }

    public CognitiveMemoryReplayJobTargetKind TargetKind { get; set; } = CognitiveMemoryReplayJobTargetKind.Unknown;

    public Guid? TargetId { get; set; }

    public string TargetKey { get; set; } = string.Empty;

    public string RequiredInputHash { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CognitiveMemoryReplayJobSignalRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ReplayJobId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid CognitiveSignalId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CognitiveMemoryReplayJobPredictionErrorRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ReplayJobId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid PredictionErrorId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CognitiveMemoryReplayOutputRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ReplayJobId { get; set; }

    public Guid ProjectId { get; set; }

    public CognitiveMemoryReplayOutputKind OutputKind { get; set; } = CognitiveMemoryReplayOutputKind.Unknown;

    public CognitiveMemoryReplayOutputStatus Status { get; set; } = CognitiveMemoryReplayOutputStatus.Draft;

    public string Summary { get; set; } = string.Empty;

    public CognitiveMemoryHashAlgorithm PayloadHashAlgorithm { get; set; } = CognitiveMemoryHashAlgorithm.Sha256;

    public string PayloadHash { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = "{}";

    public Guid? ReviewItemId { get; set; }

    public Guid? MutationCommandId { get; set; }

    public Guid? ProjectionId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CognitiveMemoryReplayWorkerResultRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ReplayJobId { get; set; }

    public Guid ProjectId { get; set; }

    public CognitiveMemoryReplayWorkerResultStatus Status { get; set; } = CognitiveMemoryReplayWorkerResultStatus.Submitted;

    public string WorkerId { get; set; } = string.Empty;

    public string InputHash { get; set; } = string.Empty;

    public string OutputHash { get; set; } = string.Empty;

    public string AlgorithmVersion { get; set; } = string.Empty;

    public string SourceScopeKey { get; set; } = string.Empty;

    public string PolicyProfileId { get; set; } = string.Empty;

    public string OutputSchema { get; set; } = string.Empty;

    public string ResultStorageReference { get; set; } = string.Empty;

    public string RejectionReason { get; set; } = string.Empty;

    public string WarningsJson { get; set; } = "[]";

    public DateTimeOffset SubmittedAtUtc { get; set; }

    public DateTimeOffset? AcceptedAtUtc { get; set; }
}

using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemoryConsolidationRunRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ProjectId { get; set; }

    public CognitiveMemoryConsolidationMode Mode { get; set; } = CognitiveMemoryConsolidationMode.IncrementalRecent;

    public CognitiveMemoryConsolidationTriggerKind TriggerKind { get; set; } = CognitiveMemoryConsolidationTriggerKind.Manual;

    public CognitiveMemoryRunStatus Status { get; set; } = CognitiveMemoryRunStatus.Pending;

    public string ProfileName { get; set; } = string.Empty;

    public string IdempotencyKey { get; set; } = string.Empty;

    public CognitiveMemoryHashAlgorithm InputHashAlgorithm { get; set; } = CognitiveMemoryHashAlgorithm.Sha256;

    public string InputHash { get; set; } = string.Empty;

    public CognitiveMemoryHashAlgorithm OutputHashAlgorithm { get; set; } = CognitiveMemoryHashAlgorithm.Sha256;

    public string OutputHash { get; set; } = string.Empty;

    public string AlgorithmVersion { get; set; } = string.Empty;

    public string Cursor { get; set; } = string.Empty;

    public string NextCursor { get; set; } = string.Empty;

    public string LeaseOwnerId { get; set; } = string.Empty;

    public DateTimeOffset LeaseExpiresAtUtc { get; set; }

    public int SourceItemsScanned { get; set; }

    public int CandidatesCreated { get; set; }

    public int MutationCommandsSubmitted { get; set; }

    public int ReviewItemsCreated { get; set; }

    public int ProjectionInvalidations { get; set; }

    public string FailureCode { get; set; } = string.Empty;

    public string FailureMessage { get; set; } = string.Empty;

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();
}

public sealed class CognitiveMemoryConsolidationCandidateRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RunId { get; set; }

    public Guid? ProjectId { get; set; }

    public CognitiveMemoryConsolidationCandidateKind CandidateKind { get; set; } = CognitiveMemoryConsolidationCandidateKind.Reflection;

    public CognitiveMemoryConsolidationCandidateStatus Status { get; set; } = CognitiveMemoryConsolidationCandidateStatus.Draft;

    public Guid? SourceItemId { get; set; }

    public Guid? EvidenceAnchorId { get; set; }

    public Guid? MemoryRecordId { get; set; }

    public Guid? MutationCommandId { get; set; }

    public Guid? ReviewItemId { get; set; }

    public Guid? ScoreEvaluationTraceId { get; set; }

    public CognitiveMemoryScoreProjectionBucket ScoreBucket { get; set; } = CognitiveMemoryScoreProjectionBucket.Unknown;

    public double? DisplayPriorityProjection { get; set; }

    public CognitiveMemoryHashAlgorithm SourceContentHashAlgorithm { get; set; } = CognitiveMemoryHashAlgorithm.Sha256;

    public string SourceContentHash { get; set; } = string.Empty;

    public CognitiveMemoryHashAlgorithm OutputHashAlgorithm { get; set; } = CognitiveMemoryHashAlgorithm.Sha256;

    public string OutputHash { get; set; } = string.Empty;

    public string AlgorithmVersion { get; set; } = string.Empty;

    public string ReasonCode { get; set; } = string.Empty;

    public string ReasonText { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = "{}";

    public DateTimeOffset CreatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();
}

public sealed class CognitiveMemoryConsolidationCursorRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ProjectId { get; set; }

    public CognitiveMemoryConsolidationMode Mode { get; set; } = CognitiveMemoryConsolidationMode.IncrementalRecent;

    public string SourceSystem { get; set; } = string.Empty;

    public string Cursor { get; set; } = string.Empty;

    public CognitiveMemoryHashAlgorithm LastSourceHashAlgorithm { get; set; } = CognitiveMemoryHashAlgorithm.Sha256;

    public string LastSourceHash { get; set; } = string.Empty;

    public Guid? LastRunId { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();
}

public sealed class CognitiveMemoryConsolidationReportRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RunId { get; set; }

    public Guid? ProjectId { get; set; }

    public CognitiveMemoryHashAlgorithm ReportHashAlgorithm { get; set; } = CognitiveMemoryHashAlgorithm.Sha256;

    public string ReportHash { get; set; } = string.Empty;

    public string ReportJson { get; set; } = "{}";

    public DateTimeOffset CreatedAtUtc { get; set; }
}

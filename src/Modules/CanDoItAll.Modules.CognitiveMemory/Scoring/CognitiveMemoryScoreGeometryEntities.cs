using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemoryScoreEvaluationTraceRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ProjectId { get; set; }

    public CognitiveMemoryScoreOwnerKind OwnerKind { get; set; } = CognitiveMemoryScoreOwnerKind.Unknown;

    public Guid? OwnerId { get; set; }

    public CognitiveMemoryScoreSpaceKind SpaceKind { get; set; } = CognitiveMemoryScoreSpaceKind.Unknown;

    public string SchemaVersion { get; set; } = string.Empty;

    public string NormalizationProfile { get; set; } = string.Empty;

    public string AlgorithmVersion { get; set; } = string.Empty;

    public CognitiveMemoryHashAlgorithm InputHashAlgorithm { get; set; } = CognitiveMemoryHashAlgorithm.Sha256;

    public string InputHash { get; set; } = string.Empty;

    public CognitiveMemoryScoreScalarProjectionKind ScalarProjectionKind { get; set; } = CognitiveMemoryScoreScalarProjectionKind.None;

    public CognitiveMemoryScoreProjectionBucket ProjectionBucket { get; set; } = CognitiveMemoryScoreProjectionBucket.Unknown;

    public double? DisplayScore { get; set; }

    public int MissingRequiredDimensionCount { get; set; }

    public int MatchedShapeCount { get; set; }

    public string TracePayloadJson { get; set; } = "{}";

    public DateTimeOffset CalculatedAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryScoreComponentRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ScoreEvaluationTraceId { get; set; }

    public Guid? ProjectId { get; set; }

    public CognitiveMemoryScoreOwnerKind OwnerKind { get; set; } = CognitiveMemoryScoreOwnerKind.Unknown;

    public Guid? OwnerId { get; set; }

    public CognitiveMemoryScoreSpaceKind SpaceKind { get; set; } = CognitiveMemoryScoreSpaceKind.Unknown;

    public string SchemaVersion { get; set; } = string.Empty;

    public CognitiveMemoryScoreDimensionKind DimensionKind { get; set; } = CognitiveMemoryScoreDimensionKind.Unknown;

    public double NormalizedValue { get; set; }

    public double Confidence { get; set; }

    public CognitiveMemoryScoreEvidenceKind EvidenceKind { get; set; } = CognitiveMemoryScoreEvidenceKind.Unknown;

    public Guid? EvidenceId { get; set; }

    public double? EvidenceConfidence { get; set; }

    public DateTimeOffset CalculatedAtUtc { get; set; }

    public string AlgorithmVersion { get; set; } = string.Empty;

    public string ComponentPayloadJson { get; set; } = "{}";
}

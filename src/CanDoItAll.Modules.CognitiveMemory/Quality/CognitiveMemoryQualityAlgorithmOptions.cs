namespace CanDoItAll.Modules.CognitiveMemory;

public sealed record CognitiveMemoryQualityAlgorithmOptions
{
    public static CognitiveMemoryQualityAlgorithmOptions Current { get; } = new();

    public CognitiveMemoryQualityClusterAlgorithmOptions Cluster { get; init; } = new();

    public CognitiveMemoryQualityDreamAlgorithmOptions Dream { get; init; } = new();

    public CognitiveMemoryQualityAggregateApplyAlgorithmOptions AggregateApply { get; init; } = new();

    public CognitiveMemoryQualityProfessorLifecycleAlgorithmOptions ProfessorLifecycle { get; init; } = new();

    public CognitiveMemoryQualityRecallAlgorithmOptions Recall { get; init; } = new();
}

public sealed record CognitiveMemoryQualityClusterAlgorithmOptions
{
    public CognitiveMemoryAlgorithmVersion AlgorithmVersion { get; init; } = new("quality-clustering-v3");

    public int MaxAggregateReadyMemoryRecords { get; init; } = 20;

    public int MaxCandidateKeyFanout { get; init; } = 80;

    public int MaxCandidatePairs { get; init; } = 5000;

    public int MaxFallbackSignalFanout { get; init; } = 24;

    public double MinimumRepresentativeKeyCoverageRatio { get; init; } = 0.5;

    public double CompositeEdgeThreshold { get; init; } = 0.58;

    public double SemanticFallbackThreshold { get; init; } = 0.62;
}

public sealed record CognitiveMemoryQualityDreamAlgorithmOptions
{
    public CognitiveMemoryAlgorithmVersion AlgorithmVersion { get; init; } = new("quality-dream-v3-claim-synthesis");

    public int MaxAggregateClaims { get; init; } = 8;

    public string AggregateClaimPredicateKey { get; init; } = "supported-by-source-memory";
}

public sealed record CognitiveMemoryQualityAggregateApplyAlgorithmOptions
{
    public CognitiveMemoryAlgorithmVersion AlgorithmVersion { get; init; } = new("quality-aggregate-apply-v3-semantic-calibrated");
}

public sealed record CognitiveMemoryQualityProfessorLifecycleAlgorithmOptions
{
    public int RequiredRepeatedUseCount { get; init; } = 2;

    public int DescendantTraversalDepth { get; init; } = 4;
}

public sealed record CognitiveMemoryQualityRecallAlgorithmOptions
{
    public int MaxFragmentsPerStatement { get; init; } = 4;

    public int MaxStatementCharacters { get; init; } = 900;
}

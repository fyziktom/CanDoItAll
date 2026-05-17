using System.Security.Cryptography;
using System.Text;

namespace CanDoItAll.Modules.CognitiveMemory;

public enum CognitiveMemoryOperationMode
{
    Observe = 0,
    Recall = 1,
    Consolidate = 2,
    Review = 3,
    Probe = 4,
    Learn = 5,
    Project = 6
}

public enum CognitiveMemoryRecordKind
{
    Source = 0,
    Semantic = 1,
    Episodic = 2,
    Procedural = 3,
    Decision = 4,
    Reflection = 5,
    Metacognitive = 6
}

public enum CognitiveMemoryRecordOrigin
{
    SourceDerived = 0,
    HumanEntered = 1,
    MachineGenerated = 2,
    Imported = 3
}

public enum CognitiveMemoryValidationState
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

public enum CognitiveMemoryStabilityState
{
    Unknown = 0,
    Experimental = 1,
    Active = 2,
    Stable = 3,
    Dormant = 4,
    Stale = 5,
    Deprecated = 6
}

public enum CognitiveMemoryRelationKind
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

public enum CognitiveMemoryProjectionKind
{
    RelationalSearch = 0,
    VectorCollection = 1,
    ContextPack = 2,
    OperatorView = 3
}

public enum CognitiveMemoryProjectionStatus
{
    Pending = 0,
    Projected = 1,
    RebuildRequired = 2,
    Failed = 3,
    Disabled = 4
}

public enum CognitiveMemoryRunKind
{
    SourceScan = 0,
    Canonicalization = 1,
    Projection = 2,
    Recall = 3,
    Consolidation = 4,
    Review = 5
}

public enum CognitiveMemoryRunStatus
{
    Pending = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3,
    Cancelled = 4,
    Blocked = 5
}

public enum CognitiveMemoryReviewKind
{
    SourceEvidence = 0,
    GeneratedMemory = 1,
    Contradiction = 2,
    AccessPolicy = 3,
    ProjectionHealth = 4,
    ProcedureSkill = 5,
    ProcedureSimulation = 6
}

public enum CognitiveMemoryReviewStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    NeedsChanges = 3,
    Deferred = 4
}

public enum CognitiveMemoryReviewSubjectKind
{
    SourceItem = 0,
    MemoryRecord = 1,
    MemoryRelation = 2,
    ProjectionState = 3,
    RecallTrace = 4,
    Run = 5,
    ProcedureSkill = 6,
    ProcedureSimulation = 7
}

public enum CognitiveMemoryEvidenceRole
{
    PrimarySource = 0,
    SupportingSource = 1,
    ContradictingSource = 2,
    GeneratedRationale = 3,
    HumanCorrection = 4
}

public enum CognitiveMemoryRedactionState
{
    Unclassified = 0,
    Safe = 1,
    Redacted = 2,
    Restricted = 3
}

public enum CognitiveMemoryAccessLevel
{
    Public = 0,
    Project = 1,
    Restricted = 2
}

public enum CognitiveMemoryRiskLevel
{
    Low = 0,
    Medium = 1,
    High = 2
}

public enum CognitiveMemoryHashAlgorithm
{
    Sha256 = 0
}

public readonly record struct CognitiveMemorySourceManifestId
{
    public CognitiveMemorySourceManifestId(Guid value)
    {
        Value = CognitiveMemoryGuard.EnsureNonEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static CognitiveMemorySourceManifestId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct CognitiveMemorySourceItemId
{
    public CognitiveMemorySourceItemId(Guid value)
    {
        Value = CognitiveMemoryGuard.EnsureNonEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static CognitiveMemorySourceItemId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct CognitiveMemoryRecordId
{
    public CognitiveMemoryRecordId(Guid value)
    {
        Value = CognitiveMemoryGuard.EnsureNonEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static CognitiveMemoryRecordId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct CognitiveMemoryRelationId
{
    public CognitiveMemoryRelationId(Guid value)
    {
        Value = CognitiveMemoryGuard.EnsureNonEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static CognitiveMemoryRelationId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct CognitiveMemoryProjectionId
{
    public CognitiveMemoryProjectionId(Guid value)
    {
        Value = CognitiveMemoryGuard.EnsureNonEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static CognitiveMemoryProjectionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct CognitiveMemoryRunId
{
    public CognitiveMemoryRunId(Guid value)
    {
        Value = CognitiveMemoryGuard.EnsureNonEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static CognitiveMemoryRunId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct CognitiveMemoryReviewItemId
{
    public CognitiveMemoryReviewItemId(Guid value)
    {
        Value = CognitiveMemoryGuard.EnsureNonEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static CognitiveMemoryReviewItemId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct CognitiveMemoryAlgorithmVersion
{
    public CognitiveMemoryAlgorithmVersion(string value)
    {
        Value = CognitiveMemoryGuard.EnsureText(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct CognitiveMemoryPolicyProfileId
{
    public CognitiveMemoryPolicyProfileId(string value)
    {
        Value = CognitiveMemoryGuard.EnsureText(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct CognitiveMemoryIdempotencyKey
{
    public CognitiveMemoryIdempotencyKey(string value)
    {
        Value = CognitiveMemoryGuard.EnsureText(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public sealed record CognitiveMemoryHash
{
    public CognitiveMemoryHash(CognitiveMemoryHashAlgorithm algorithm, string value)
    {
        Algorithm = algorithm;
        Value = NormalizeHashValue(algorithm, value);
    }

    public CognitiveMemoryHashAlgorithm Algorithm { get; }

    public string Value { get; }

    public static CognitiveMemoryHash FromUtf8(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return new CognitiveMemoryHash(CognitiveMemoryHashAlgorithm.Sha256, Convert.ToHexString(bytes));
    }

    public override string ToString() => $"{Algorithm}:{Value}";

    private static string NormalizeHashValue(CognitiveMemoryHashAlgorithm algorithm, string value)
    {
        var normalized = CognitiveMemoryGuard.EnsureText(value, nameof(value)).ToLowerInvariant();
        if (algorithm == CognitiveMemoryHashAlgorithm.Sha256 && !IsSha256Hex(normalized))
        {
            throw new ArgumentException("SHA-256 hashes must be 64 hexadecimal characters.", nameof(value));
        }

        return normalized;
    }

    private static bool IsSha256Hex(string value)
        => value.Length == 64 && value.All(Uri.IsHexDigit);
}

public sealed record CognitiveMemoryPolicyContext(
    Guid? ProjectId,
    string ActorId,
    CognitiveMemoryAccessLevel AccessLevel,
    CognitiveMemoryPolicyProfileId PolicyProfileId,
    CognitiveMemoryRiskLevel RiskLevel,
    bool AllowRestrictedContent);

public sealed record CognitiveMemoryAccessRequest(
    CognitiveMemoryOperationMode OperationMode,
    CognitiveMemoryPolicyContext PolicyContext,
    IReadOnlyList<CognitiveMemoryRecord> CandidateRecords);

public sealed record CognitiveMemoryAccessDenial(
    CognitiveMemoryRecordId RecordId,
    string ReasonCode,
    string Reason);

public sealed record CognitiveMemoryAccessDecision(
    IReadOnlyList<CognitiveMemoryRecordId> AllowedRecordIds,
    IReadOnlyList<CognitiveMemoryAccessDenial> Denials);

public interface ICognitiveMemoryAccessPolicy
{
    ValueTask<CognitiveMemoryAccessDecision> EvaluateAsync(
        CognitiveMemoryAccessRequest request,
        CancellationToken cancellationToken = default);
}

internal static class CognitiveMemoryGuard
{
    public static Guid EnsureNonEmpty(Guid value, string parameterName)
        => value == Guid.Empty
            ? throw new ArgumentException("Identifier values must not be empty.", parameterName)
            : value;

    public static string EnsureText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be empty.", parameterName);
        }

        return value.Trim();
    }
}

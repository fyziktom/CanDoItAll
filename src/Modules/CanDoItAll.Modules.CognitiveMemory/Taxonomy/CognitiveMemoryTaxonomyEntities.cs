using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemoryRecordEvidenceAnchorRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MemoryRecordId { get; set; }

    public Guid EvidenceAnchorId { get; set; }

    public CognitiveMemoryEvidenceRole EvidenceRole { get; set; } = CognitiveMemoryEvidenceRole.PrimarySource;

    public string Summary { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CognitiveMemoryRelationEvidenceRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RelationId { get; set; }

    public Guid EvidenceAnchorId { get; set; }

    public CognitiveMemoryEvidenceDirection Direction { get; set; } = CognitiveMemoryEvidenceDirection.Supports;

    public string Summary { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CognitiveMemoryProjectionRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ProjectId { get; set; }

    public Guid MemoryRecordId { get; set; }

    public CognitiveMemoryProjectionStoreKind ProjectionStoreKind { get; set; } = CognitiveMemoryProjectionStoreKind.GenericRag;

    public CognitiveMemoryProjectionKind ProjectionKind { get; set; } = CognitiveMemoryProjectionKind.VectorCollection;

    public string TargetProviderName { get; set; } = string.Empty;

    public string CollectionName { get; set; } = string.Empty;

    public string PointId { get; set; } = string.Empty;

    public string ProjectionProfileId { get; set; } = string.Empty;

    public string EmbeddingProfileId { get; set; } = string.Empty;

    public string ProjectionSchemaVersion { get; set; } = string.Empty;

    public string AlgorithmVersion { get; set; } = string.Empty;

    public int VectorDimensions { get; set; }

    public CognitiveMemoryHashAlgorithm SourceHashAlgorithm { get; set; } = CognitiveMemoryHashAlgorithm.Sha256;

    public string SourceHash { get; set; } = string.Empty;

    public CognitiveMemoryHashAlgorithm PayloadHashAlgorithm { get; set; } = CognitiveMemoryHashAlgorithm.Sha256;

    public string PayloadHash { get; set; } = string.Empty;

    public CognitiveMemoryProjectionStatus Status { get; set; } = CognitiveMemoryProjectionStatus.Pending;

    public CognitiveMemoryProjectionStaleReason StaleReason { get; set; } = CognitiveMemoryProjectionStaleReason.None;

    public bool RebuildRequired { get; set; }

    public string FailureCode { get; set; } = string.Empty;

    public string FailureMessage { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public DateTimeOffset? LastProjectedAtUtc { get; set; }

    public DateTimeOffset? DeletedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemorySourceItemLayoutRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SourceItemId { get; set; }

    public Guid? ProjectId { get; set; }

    public double? X { get; set; }

    public double? Y { get; set; }

    public int? ZIndex { get; set; }

    public DateTimeOffset? StartUtc { get; set; }

    public DateTimeOffset? EndUtc { get; set; }

    public int? DurationSeconds { get; set; }

    public CognitiveMemorySourceSurfaceKind SurfaceKind { get; set; }

    public string MetadataJson { get; set; } = "{}";

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class CognitiveMemorySourceItemGraphLinkRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SourceManifestId { get; set; }

    public Guid SourceItemId { get; set; }

    public Guid? ProjectId { get; set; }

    public string SourceItemKey { get; set; } = string.Empty;

    public string TargetSourceItemKey { get; set; } = string.Empty;

    public CognitiveMemorySourceLinkKind LinkKind { get; set; }

    public bool IsUserAuthored { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CognitiveMemorySourceItemContextHintRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SourceItemId { get; set; }

    public Guid ContextFrameId { get; set; }

    public Guid? ProjectId { get; set; }

    public CognitiveMemoryContextDimensionKind DimensionKind { get; set; } = CognitiveMemoryContextDimensionKind.Project;

    public string ValueKey { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CognitiveMemorySourceTombstoneRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ProjectId { get; set; }

    public string SourceSystem { get; set; } = string.Empty;

    public string SourceScopeKey { get; set; } = string.Empty;

    public string SourceItemKey { get; set; } = string.Empty;

    public Guid? PreviousSourceItemId { get; set; }

    public Guid DetectedInManifestId { get; set; }

    public DateTimeOffset TombstonedAtUtc { get; set; }

    public string Reason { get; set; } = string.Empty;

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemorySourceScanFailureRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RunId { get; set; }

    public Guid? ProjectId { get; set; }

    public string SourceSystem { get; set; } = string.Empty;

    public string SourceScopeKey { get; set; } = string.Empty;

    public string CursorHash { get; set; } = string.Empty;

    public string ExceptionCategory { get; set; } = string.Empty;

    public CognitiveMemorySourceScanFailureRetryPolicy RetryPolicy { get; set; } = CognitiveMemorySourceScanFailureRetryPolicy.Retryable;

    public string Message { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}

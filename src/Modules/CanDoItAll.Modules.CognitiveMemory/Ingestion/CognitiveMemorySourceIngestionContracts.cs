using CanDoItAll.AgentFramework.Core;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.Modules.CognitiveMemory;

public enum CognitiveMemorySourceIngestionStatus
{
    Ingested = 0,
    DuplicateRejected = 1,
    Failed = 2
}

public enum CognitiveMemorySourceScanFailureRetryPolicy
{
    Retryable = 0,
    NotRetryable = 1
}

public sealed record CognitiveMemorySourceIngestionRequest(
    MemorySourceKind SourceKind,
    Guid ScopeId,
    CognitiveMemoryIdempotencyKey IdempotencyKey,
    MemorySourceSnapshotCursor? Cursor = null,
    int? Take = null,
    Guid? ProjectId = null);

public sealed record CognitiveMemorySourceIngestionResult(
    CognitiveMemorySourceIngestionStatus Status,
    Guid RunId,
    Guid? ManifestId,
    MemorySourceSnapshotCursor? NextCursor,
    bool HasMore,
    int SourceItemCount,
    int CreatedSourceItemCount,
    int UpdatedSourceItemCount,
    int CreatedEvidenceAnchorCount,
    int CreatedContextHintCount,
    int CreatedLayoutCount,
    int CreatedGraphLinkCount,
    int CreatedTombstoneCount,
    Guid? FailureId,
    string? FailureCode);

public sealed record CognitiveMemorySourceItemProvenancePayload(
    string SourceItemId,
    string SourceRoute,
    string SourceEntityId,
    MemorySourceKind SourceKind,
    MemorySourceEntityKind EntityKind,
    MemorySourceHashClassification HashClassification,
    MemorySourceHashPayloadBasis HashPayloadBasis,
    string HashUsageSummary,
    MemorySourceAccessMode AccessMode,
    MemorySourceSensitivity Sensitivity,
    bool ContainsSensitivePayload,
    string RedactionPolicy,
    string AllowedFutureUsageSummary,
    string StorageProvider,
    CognitiveMemorySourceStorageLocatorKind StorageLocatorKind,
    string StorageLocator,
    string StorageContentType,
    string StorageFileName,
    CognitiveMemorySourceSurfaceKind LayoutSurfaceKind,
    string LayoutMetadataJson,
    Dictionary<string, string> Metadata,
    IReadOnlyList<CognitiveMemorySourceReferencePayload> References,
    IReadOnlyList<CognitiveMemorySourceLinkPayload> Links);

public sealed record CognitiveMemorySourceReferencePayload(
    CognitiveMemorySourceReferenceKind ReferenceKind,
    string ReferenceId,
    int OrderIndex);

public sealed record CognitiveMemorySourceLinkPayload(
    string SourceItemKey,
    string TargetSourceItemKey,
    CognitiveMemorySourceLinkKind Kind,
    bool IsUserAuthored);

[JsonConverter(typeof(CognitiveMemorySourceSurfaceKindJsonConverter))]
public readonly record struct CognitiveMemorySourceSurfaceKind
{
    public CognitiveMemorySourceSurfaceKind(string? value)
    {
        Value = value?.Trim() ?? string.Empty;
    }

    public string Value { get; }

    public static CognitiveMemorySourceSurfaceKind Required(string value)
        => new(CognitiveMemoryGuard.EnsureText(value, nameof(value)));

    public override string ToString() => Value ?? string.Empty;
}

[JsonConverter(typeof(CognitiveMemorySourceLinkKindJsonConverter))]
public readonly record struct CognitiveMemorySourceLinkKind
{
    public CognitiveMemorySourceLinkKind(string? value)
    {
        Value = value?.Trim() ?? string.Empty;
    }

    public string Value { get; }

    public static CognitiveMemorySourceLinkKind Required(string value)
        => new(CognitiveMemoryGuard.EnsureText(value, nameof(value)));

    public override string ToString() => Value ?? string.Empty;
}

[JsonConverter(typeof(CognitiveMemorySourceReferenceKindJsonConverter))]
public readonly record struct CognitiveMemorySourceReferenceKind
{
    public CognitiveMemorySourceReferenceKind(string? value)
    {
        Value = value?.Trim() ?? string.Empty;
    }

    public string Value { get; }

    public static CognitiveMemorySourceReferenceKind Required(string value)
        => new(CognitiveMemoryGuard.EnsureText(value, nameof(value)));

    public override string ToString() => Value ?? string.Empty;
}

[JsonConverter(typeof(CognitiveMemorySourceStorageLocatorKindJsonConverter))]
public readonly record struct CognitiveMemorySourceStorageLocatorKind
{
    public CognitiveMemorySourceStorageLocatorKind(string? value)
    {
        Value = value?.Trim() ?? string.Empty;
    }

    public string Value { get; }

    public override string ToString() => Value ?? string.Empty;
}

public interface ICognitiveMemorySourceIngestionService
{
    ValueTask<CognitiveMemorySourceIngestionResult> IngestAsync(
        CognitiveMemorySourceIngestionRequest request,
        CancellationToken cancellationToken = default);
}

internal sealed class CognitiveMemorySourceSurfaceKindJsonConverter : JsonConverter<CognitiveMemorySourceSurfaceKind>
{
    public override CognitiveMemorySourceSurfaceKind Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
        => new(reader.TokenType == JsonTokenType.Null ? string.Empty : reader.GetString());

    public override void Write(
        Utf8JsonWriter writer,
        CognitiveMemorySourceSurfaceKind value,
        JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}

internal sealed class CognitiveMemorySourceLinkKindJsonConverter : JsonConverter<CognitiveMemorySourceLinkKind>
{
    public override CognitiveMemorySourceLinkKind Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
        => new(reader.TokenType == JsonTokenType.Null ? string.Empty : reader.GetString());

    public override void Write(
        Utf8JsonWriter writer,
        CognitiveMemorySourceLinkKind value,
        JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}

internal sealed class CognitiveMemorySourceReferenceKindJsonConverter : JsonConverter<CognitiveMemorySourceReferenceKind>
{
    public override CognitiveMemorySourceReferenceKind Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
        => new(reader.TokenType == JsonTokenType.Null ? string.Empty : reader.GetString());

    public override void Write(
        Utf8JsonWriter writer,
        CognitiveMemorySourceReferenceKind value,
        JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}

internal sealed class CognitiveMemorySourceStorageLocatorKindJsonConverter : JsonConverter<CognitiveMemorySourceStorageLocatorKind>
{
    public override CognitiveMemorySourceStorageLocatorKind Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
        => new(reader.TokenType == JsonTokenType.Null ? string.Empty : reader.GetString());

    public override void Write(
        Utf8JsonWriter writer,
        CognitiveMemorySourceStorageLocatorKind value,
        JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}

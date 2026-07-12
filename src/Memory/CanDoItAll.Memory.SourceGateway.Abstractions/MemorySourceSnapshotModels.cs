namespace CanDoItAll.Memory.SourceGateway;

public sealed record MemorySourceSnapshot(
    MemorySourceSnapshotManifest Manifest,
    IReadOnlyList<MemorySourceItem> Items);

public sealed record MemorySourceSnapshotManifest(
    MemorySourceSnapshotId SnapshotId,
    MemorySourceKind SourceKind,
    Guid ScopeId,
    DateTimeOffset CapturedAtUtc,
    int TotalItemCount,
    MemorySourceSnapshotCursor? NextCursor,
    bool HasMore,
    MemorySourceSnapshotPageStatus PageStatus = MemorySourceSnapshotPageStatus.PageReturned,
    MemorySourceSnapshotHashScope SnapshotHashScope = MemorySourceSnapshotHashScope.FullSnapshot,
    string ProviderVersion = "");

public sealed record MemorySourceItem(
    MemorySourceItemId Id,
    MemorySourceKind SourceKind,
    MemorySourceEntityKind EntityKind,
    string Title,
    string Content,
    string ContentHash,
    DateTimeOffset? CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    MemorySourceProvenance Provenance,
    MemorySourcePermissionContext Permission,
    MemorySourceLayoutMetadata? Layout,
    IReadOnlyList<MemorySourceLink> Links,
    IReadOnlyList<MemorySourceReference> References,
    MemorySourceStorageReference? StorageReference,
    IReadOnlyDictionary<string, string> Metadata)
{
    public MemorySourceHashPolicy HashPolicy { get; init; } = MemorySourceHashPolicy.InternalIntegrity;
}

public sealed record MemorySourceProvenance(
    MemorySourceKind SourceKind,
    Guid ScopeId,
    MemorySourceEntityKind EntityKind,
    string SourceEntityId,
    string SourceRoute);

public sealed record MemorySourcePermissionContext(
    MemorySourceAccessMode AccessMode,
    MemorySourceSensitivity Sensitivity,
    bool ContainsSensitivePayload,
    string RedactionPolicy,
    string AllowedFutureUsageSummary);

public sealed record MemorySourceLayoutMetadata(
    double? X,
    double? Y,
    int? ZIndex,
    DateTimeOffset? StartUtc,
    DateTimeOffset? EndUtc,
    int? DurationSeconds,
    string SurfaceKind,
    string MetadataJson);

public sealed record MemorySourceLink(
    MemorySourceItemId SourceId,
    MemorySourceItemId TargetId,
    string Kind,
    bool IsUserAuthored);

public sealed record MemorySourceReference(
    string ReferenceKind,
    string ReferenceId,
    int OrderIndex);

public sealed record MemorySourceStorageReference(
    string Provider,
    string LocatorKind,
    string Locator,
    string ContentType,
    string OriginalFileName);

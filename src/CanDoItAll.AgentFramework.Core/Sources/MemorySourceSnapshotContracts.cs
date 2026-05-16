using System.Security.Cryptography;
using System.Text;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public interface IProjectStructureSourceSnapshotProvider
{
    Task<MemorySourceSnapshot> ReadSnapshotAsync(
        ProjectStructureSourceSnapshotRequest request,
        CancellationToken cancellationToken = default);
}

public interface IProcessRuntimeEvidenceSourceProvider
{
    Task<MemorySourceSnapshot> ReadSnapshotAsync(
        ProcessRuntimeEvidenceSourceRequest request,
        CancellationToken cancellationToken = default);
}

public interface IWorkflowRuntimeEvidenceSourceProvider
{
    Task<MemorySourceSnapshot> ReadSnapshotAsync(
        WorkflowRuntimeEvidenceSourceRequest request,
        CancellationToken cancellationToken = default);
}

public readonly record struct MemorySourceItemId
{
    public MemorySourceItemId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }

    public static MemorySourceItemId Create(
        MemorySourceKind sourceKind,
        Guid scopeId,
        MemorySourceEntityKind entityKind,
        string sourceEntityId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEntityId);
        return new MemorySourceItemId($"{sourceKind}:{scopeId:N}:{entityKind}:{sourceEntityId.Trim()}");
    }

    public override string ToString() => Value;
}

public readonly record struct MemorySourceSnapshotId
{
    public MemorySourceSnapshotId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }

    public static MemorySourceSnapshotId Create(
        MemorySourceKind sourceKind,
        Guid scopeId,
        string contentHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);
        return new MemorySourceSnapshotId($"{sourceKind}:{scopeId:N}:{contentHash.Trim()}");
    }

    public override string ToString() => Value;
}

public readonly record struct MemorySourceSnapshotCursor
{
    public MemorySourceSnapshotCursor(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public enum MemorySourceKind
{
    WorkbenchProjectStructure,
    ProcessRuntime,
    WorkflowRuntime
}

public enum MemorySourceEntityKind
{
    ProjectNode,
    ProjectLink,
    ProcessRun,
    ProcessStepRun,
    ProcessRunAssignment,
    ProcessWorkBrief,
    ProcessDecision,
    ProcessArtifact,
    ProcessJournal,
    ProcessConformanceObservation,
    ProcessImprovementCandidate,
    ProcessWorkflowRunLink,
    WorkflowRun,
    WorkflowEvent,
    WorkflowArtifact,
    WorkflowExternalRequest
}

public enum MemorySourceSensitivity
{
    Public,
    Internal,
    Confidential,
    Sensitive
}

public enum MemorySourceAccessMode
{
    ReadOnly,
    Redacted
}

public sealed record ProjectStructureSourceSnapshotRequest(
    Guid ProjectId,
    MemorySourceSnapshotCursor? Cursor = null,
    int? Take = null);

public sealed record ProcessRuntimeEvidenceSourceRequest(
    Guid? ProcessRunId = null,
    MemorySourceSnapshotCursor? Cursor = null,
    int? Take = null);

public sealed record WorkflowRuntimeEvidenceSourceRequest(
    WorkflowRunId? RunId = null,
    MemorySourceSnapshotCursor? Cursor = null,
    int? Take = null);

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
    bool HasMore);

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
    IReadOnlyDictionary<string, string> Metadata);

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

public static class MemorySourceSnapshotPage
{
    public const int DefaultTake = 250;
    public const int MaxTake = 1000;

    public static IReadOnlyList<MemorySourceItem> Apply(
        IReadOnlyList<MemorySourceItem> items,
        MemorySourceSnapshotCursor? cursor,
        int? take,
        out MemorySourceSnapshotCursor? nextCursor,
        out bool hasMore)
    {
        var orderedItems = items
            .OrderBy(item => item.Id.Value, StringComparer.Ordinal)
            .ToList();
        var startIndex = 0;
        if (cursor.HasValue)
        {
            var cursorValue = cursor.Value.Value;
            startIndex = orderedItems.FindIndex(item => string.Equals(item.Id.Value, cursorValue, StringComparison.Ordinal));
            startIndex = startIndex < 0 ? 0 : startIndex + 1;
        }

        var pageSize = NormalizeTake(take);
        var page = orderedItems
            .Skip(startIndex)
            .Take(pageSize)
            .ToList();
        hasMore = startIndex + page.Count < orderedItems.Count;
        nextCursor = hasMore && page.Count > 0
            ? new MemorySourceSnapshotCursor(page[^1].Id.Value)
            : null;
        return page;
    }

    public static int NormalizeTake(int? take)
        => Math.Clamp(take ?? DefaultTake, 1, MaxTake);
}

public static class MemorySourceSnapshotHasher
{
    public static string Compute(params string?[] parts)
    {
        var normalized = string.Join(
            '\u001f',
            parts.Select(part => part?.ReplaceLineEndings("\n").Trim() ?? string.Empty));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

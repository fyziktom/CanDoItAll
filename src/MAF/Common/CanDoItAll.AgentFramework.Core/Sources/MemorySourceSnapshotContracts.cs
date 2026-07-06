using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public static class MemorySourceSnapshotProviderVersions
{
    public const string WorkbenchProjectStructure = "workbench-project-structure-v2";
    public const string ProcessRuntime = "process-runtime-evidence-v2";
    public const string WorkflowRuntime = "workflow-runtime-evidence-v2";
    public const string CrmHr = "crm-hr-source-v1";
    public const string ResourceCatalog = "resource-catalog-source-v1";
    public const string ManualInput = "manual-input-source-v1";
}

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

public interface ICrmHrSourceSnapshotProvider
{
    Task<MemorySourceSnapshot> ReadSnapshotAsync(
        CrmHrSourceSnapshotRequest request,
        CancellationToken cancellationToken = default);
}

public interface IResourceSourceSnapshotProvider
{
    Task<MemorySourceSnapshot> ReadSnapshotAsync(
        ResourceSourceSnapshotRequest request,
        CancellationToken cancellationToken = default);
}

public interface IManualSourceSnapshotProvider
{
    Task<MemorySourceSnapshot> ReadSnapshotAsync(
        ManualSourceSnapshotRequest request,
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

    public static bool TryParse(MemorySourceItemId id, out MemorySourceItemKey key)
    {
        var parts = id.Value.Split(':', 4, StringSplitOptions.TrimEntries);
        if (parts.Length != 4 ||
            !Enum.TryParse(parts[0], ignoreCase: false, out MemorySourceKind sourceKind) ||
            !Guid.TryParseExact(parts[1], "N", out var scopeId) ||
            !Enum.TryParse(parts[2], ignoreCase: false, out MemorySourceEntityKind entityKind) ||
            string.IsNullOrWhiteSpace(parts[3]))
        {
            key = default;
            return false;
        }

        key = new MemorySourceItemKey(sourceKind, scopeId, entityKind, parts[3]);
        return true;
    }

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

public readonly record struct MemorySourceItemKey(
    MemorySourceKind SourceKind,
    Guid ScopeId,
    MemorySourceEntityKind EntityKind,
    string SourceEntityId);

public readonly record struct MemorySourceSnapshotId
{
    [JsonConstructor]
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

    public static MemorySourceSnapshotCursor Create(
        MemorySourceKind sourceKind,
        Guid scopeId,
        string providerVersion,
        int position,
        MemorySourceItemId lastItemId,
        string snapshotAnchor = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerVersion);
        if (position <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(position), "Cursor position must be greater than zero.");
        }

        var payload = new MemorySourceSnapshotCursorPayload(
            sourceKind.ToString(),
            scopeId,
            providerVersion.Trim(),
            position,
            lastItemId.Value,
            snapshotAnchor.Trim());
        var json = JsonSerializer.Serialize(payload);
        return new MemorySourceSnapshotCursor(Convert.ToBase64String(Encoding.UTF8.GetBytes(json)));
    }

    public static MemorySourceSnapshotCursorDescriptor? ReadDescriptorOrThrow(
        MemorySourceSnapshotCursor? cursor,
        MemorySourceKind expectedSourceKind,
        Guid expectedScopeId,
        string expectedProviderVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedProviderVersion);
        if (!cursor.HasValue)
        {
            return null;
        }

        MemorySourceSnapshotCursorPayload payload;
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(cursor.Value.Value));
            payload = JsonSerializer.Deserialize<MemorySourceSnapshotCursorPayload>(json)
                ?? throw new JsonException("Cursor payload is empty.");
        }
        catch (Exception exception) when (exception is FormatException or JsonException or ArgumentException)
        {
            throw new MemorySourceSnapshotCursorException(
                MemorySourceSnapshotCursorFailureReason.InvalidFormat,
                expectedSourceKind,
                expectedScopeId,
                expectedProviderVersion,
                cursor.Value,
                "Memory source snapshot cursor is not a supported cursor payload.",
                exception);
        }

        if (!Enum.TryParse(payload.SourceKind, ignoreCase: false, out MemorySourceKind sourceKind))
        {
            throw new MemorySourceSnapshotCursorException(
                MemorySourceSnapshotCursorFailureReason.InvalidFormat,
                expectedSourceKind,
                expectedScopeId,
                expectedProviderVersion,
                cursor.Value,
                $"Memory source snapshot cursor has unsupported source kind '{payload.SourceKind}'.");
        }

        if (!string.Equals(payload.ProviderVersion, expectedProviderVersion, StringComparison.Ordinal))
        {
            throw new MemorySourceSnapshotCursorException(
                MemorySourceSnapshotCursorFailureReason.ProviderVersionMismatch,
                expectedSourceKind,
                expectedScopeId,
                expectedProviderVersion,
                cursor.Value,
                $"Memory source snapshot cursor provider version '{payload.ProviderVersion}' does not match expected version '{expectedProviderVersion}'.");
        }

        if (sourceKind != expectedSourceKind)
        {
            throw new MemorySourceSnapshotCursorException(
                MemorySourceSnapshotCursorFailureReason.SourceKindMismatch,
                expectedSourceKind,
                expectedScopeId,
                expectedProviderVersion,
                cursor.Value,
                $"Memory source snapshot cursor source kind '{sourceKind}' does not match expected kind '{expectedSourceKind}'.");
        }

        if (payload.ScopeId != expectedScopeId)
        {
            throw new MemorySourceSnapshotCursorException(
                MemorySourceSnapshotCursorFailureReason.ScopeMismatch,
                expectedSourceKind,
                expectedScopeId,
                expectedProviderVersion,
                cursor.Value,
                $"Memory source snapshot cursor scope '{payload.ScopeId:D}' does not match expected scope '{expectedScopeId:D}'.");
        }

        if (payload.Position <= 0)
        {
            throw new MemorySourceSnapshotCursorException(
                MemorySourceSnapshotCursorFailureReason.InvalidFormat,
                expectedSourceKind,
                expectedScopeId,
                expectedProviderVersion,
                cursor.Value,
                "Memory source snapshot cursor position must be greater than zero.");
        }

        MemorySourceItemId lastItemId;
        try
        {
            lastItemId = new MemorySourceItemId(payload.LastItemId);
        }
        catch (ArgumentException exception)
        {
            throw new MemorySourceSnapshotCursorException(
                MemorySourceSnapshotCursorFailureReason.InvalidFormat,
                expectedSourceKind,
                expectedScopeId,
                expectedProviderVersion,
                cursor.Value,
                "Memory source snapshot cursor last item anchor is empty or unsupported.",
                exception);
        }

        if (!MemorySourceItemId.TryParse(lastItemId, out var lastItemKey) ||
            lastItemKey.SourceKind != expectedSourceKind)
        {
            throw new MemorySourceSnapshotCursorException(
                MemorySourceSnapshotCursorFailureReason.InvalidFormat,
                expectedSourceKind,
                expectedScopeId,
                expectedProviderVersion,
                cursor.Value,
                "Memory source snapshot cursor last item anchor is not a supported source item id.");
        }

        return new MemorySourceSnapshotCursorDescriptor(
            sourceKind,
            payload.ScopeId,
            payload.ProviderVersion,
            payload.Position,
            lastItemId,
            payload.SnapshotAnchor ?? string.Empty);
    }

    public static void ThrowStaleAnchor(
        MemorySourceSnapshotCursor cursor,
        MemorySourceKind expectedSourceKind,
        Guid expectedScopeId,
        string expectedProviderVersion,
        string message)
        => throw new MemorySourceSnapshotCursorException(
            MemorySourceSnapshotCursorFailureReason.StaleAnchor,
            expectedSourceKind,
            expectedScopeId,
            expectedProviderVersion,
            cursor,
            message);

    public override string ToString() => Value;

    private sealed record MemorySourceSnapshotCursorPayload(
        string SourceKind,
        Guid ScopeId,
        string ProviderVersion,
        int Position,
        string LastItemId,
        string? SnapshotAnchor);
}

public sealed record MemorySourceSnapshotCursorDescriptor(
    MemorySourceKind SourceKind,
    Guid ScopeId,
    string ProviderVersion,
    int Position,
    MemorySourceItemId LastItemId,
    string SnapshotAnchor);

public enum MemorySourceSnapshotCursorFailureReason
{
    InvalidFormat,
    SourceKindMismatch,
    ScopeMismatch,
    ProviderVersionMismatch,
    StaleAnchor
}

public sealed class MemorySourceSnapshotCursorException : InvalidOperationException
{
    public MemorySourceSnapshotCursorException(
        MemorySourceSnapshotCursorFailureReason reason,
        MemorySourceKind expectedSourceKind,
        Guid expectedScopeId,
        string expectedProviderVersion,
        MemorySourceSnapshotCursor cursor,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Reason = reason;
        ExpectedSourceKind = expectedSourceKind;
        ExpectedScopeId = expectedScopeId;
        ExpectedProviderVersion = expectedProviderVersion;
        Cursor = cursor;
    }

    public MemorySourceSnapshotCursorFailureReason Reason { get; }

    public MemorySourceKind ExpectedSourceKind { get; }

    public Guid ExpectedScopeId { get; }

    public string ExpectedProviderVersion { get; }

    public MemorySourceSnapshotCursor Cursor { get; }
}

public enum MemorySourceKind
{
    WorkbenchProjectStructure,
    ProcessRuntime,
    WorkflowRuntime,
    CrmHr,
    ResourceCatalog,
    ManualInput
}

public enum MemorySourceEntityKind
{
    ProjectNode,
    ProjectLink,
    ProcessRun,
    ProcessStepEvidence,
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
    WorkflowExternalRequest,
    ProcessDefinition,
    ProcessAgentSession,
    ProcessCompletionOutcome,
    CrmParty,
    CrmAccountProfile,
    CrmOpportunity,
    CrmInteraction,
    HrWorkforceProfile,
    ResourceReference,
    ManualText,
    ManualFileReference,
    ManualLinkReference
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

public enum MemorySourceSnapshotPageStatus
{
    PageReturned,
    EndOfSource
}

public enum MemorySourceSnapshotHashScope
{
    FullSnapshot,
    PageScoped,
    ProviderScope
}

public enum MemorySourceHashClassification
{
    PublicExportable,
    InternalIntegrity,
    RestrictedIntegrity
}

public enum MemorySourceHashPayloadBasis
{
    RedactedContent,
    SourceMetadata,
    RawSensitivePayload
}

public sealed record MemorySourceHashPolicy(
    MemorySourceHashClassification Classification,
    MemorySourceHashPayloadBasis PayloadBasis,
    bool Exportable,
    string UsageSummary)
{
    public static MemorySourceHashPolicy InternalIntegrity { get; } = new(
        MemorySourceHashClassification.InternalIntegrity,
        MemorySourceHashPayloadBasis.SourceMetadata,
        Exportable: false,
        "Internal source-change detection only. Do not expose as browser-visible metadata or vector payload data.");

    public static MemorySourceHashPolicy PublicRedactedContent { get; } = new(
        MemorySourceHashClassification.PublicExportable,
        MemorySourceHashPayloadBasis.RedactedContent,
        Exportable: true,
        "Hash is derived from redacted exposed content and may be used for non-sensitive public integrity checks.");

    public static MemorySourceHashPolicy RestrictedRawPayloadIntegrity(string usageSummary)
        => new(
            MemorySourceHashClassification.RestrictedIntegrity,
            MemorySourceHashPayloadBasis.RawSensitivePayload,
            Exportable: false,
            usageSummary);
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

public sealed record CrmHrSourceSnapshotRequest(
    Guid? PartyId = null,
    MemorySourceSnapshotCursor? Cursor = null,
    int? Take = null);

public sealed record ResourceSourceSnapshotRequest(
    Guid? ResourceId = null,
    Guid? ProjectId = null,
    MemorySourceSnapshotCursor? Cursor = null,
    int? Take = null);

public sealed record ManualSourceSnapshotRequest(
    Guid SourceId,
    string PayloadKind,
    string Title,
    string ContentText,
    string Locator,
    string ContentType,
    string SourceCategory,
    IReadOnlyList<string> Tags,
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

public static class MemorySourceSnapshotPage
{
    public const int DefaultTake = 250;
    public const int MaxTake = 1000;

    public static IReadOnlyList<MemorySourceItem> Apply(
        IReadOnlyList<MemorySourceItem> items,
        MemorySourceSnapshotCursor? cursor,
        int? take,
        MemorySourceKind sourceKind,
        Guid scopeId,
        string providerVersion,
        out MemorySourceSnapshotCursor? nextCursor,
        out bool hasMore,
        string snapshotAnchor = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerVersion);
        var orderedItems = items
            .OrderBy(item => item.Id.Value, StringComparer.Ordinal)
            .ToList();

        var descriptor = MemorySourceSnapshotCursor.ReadDescriptorOrThrow(
            cursor,
            sourceKind,
            scopeId,
            providerVersion);
        var startIndex = descriptor?.Position ?? 0;
        if (descriptor is not null)
        {
            var anchorIndex = descriptor.Position - 1;
            if (anchorIndex >= orderedItems.Count ||
                orderedItems[anchorIndex].Id != descriptor.LastItemId)
            {
                MemorySourceSnapshotCursor.ThrowStaleAnchor(
                    cursor!.Value,
                    sourceKind,
                    scopeId,
                    providerVersion,
                    "Memory source snapshot cursor anchor is stale or no longer matches the ordered source item at the recorded position.");
            }
        }

        var pageSize = NormalizeTake(take);
        var page = orderedItems
            .Skip(startIndex)
            .Take(pageSize)
            .ToList();
        hasMore = startIndex + page.Count < orderedItems.Count;
        nextCursor = hasMore && page.Count > 0
            ? MemorySourceSnapshotCursor.Create(
                sourceKind,
                scopeId,
                providerVersion,
                startIndex + page.Count,
                page[^1].Id,
                snapshotAnchor)
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

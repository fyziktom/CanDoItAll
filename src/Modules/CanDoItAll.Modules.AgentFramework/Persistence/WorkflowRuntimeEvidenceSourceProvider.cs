using CanDoItAll.Memory.SourceGateway;
using System.Globalization;
using System.Linq.Expressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class WorkflowRuntimeEvidenceSourceProvider(
    IDbContextFactory<AppDbContext> dbContextFactory) : IWorkflowRuntimeEvidenceSourceProvider
{
    public async Task<MemorySourceSnapshot> ReadSnapshotAsync(
        WorkflowRuntimeEvidenceSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var runId = request.RunId;
        var scopeId = runId ?? Guid.Empty;
        var sources = new[]
            {
                CreateSource(
                    MemorySourceEntityKind.WorkflowRun,
                    FilterByRunId(dbContext.Set<WorkflowRunRecordEntity>().AsNoTracking(), runId),
                    item => item.RunId,
                    MapRun),
                CreateSource(
                    MemorySourceEntityKind.WorkflowEvent,
                    FilterByRunId(dbContext.Set<WorkflowEventRecordEntity>().AsNoTracking(), runId),
                    item => item.Id,
                    MapEvent),
                CreateSource(
                    MemorySourceEntityKind.WorkflowExternalRequest,
                    FilterByRunId(dbContext.Set<WorkflowExternalRequestRecordEntity>().AsNoTracking(), runId),
                    item => item.Id,
                    MapExternalRequest),
                CreateSource(
                    MemorySourceEntityKind.WorkflowArtifact,
                    FilterByRunId(dbContext.Set<WorkflowArtifactRecordEntity>().AsNoTracking(), runId),
                    item => item.Id,
                    MapArtifact)
            }
            .OrderBy(source => source.EntityKind.ToString(), StringComparer.Ordinal)
            .ToList();
        var page = await ReadPageAsync(
            sources,
            request.Cursor,
            request.Take,
            scopeId,
            cancellationToken);

        return new MemorySourceSnapshot(
            new MemorySourceSnapshotManifest(
                MemorySourceSnapshotId.Create(MemorySourceKind.WorkflowRuntime, scopeId, page.SnapshotHash),
                MemorySourceKind.WorkflowRuntime,
                scopeId,
                DateTimeOffset.UtcNow,
                page.TotalItemCount,
                page.NextCursor,
                page.HasMore,
                page.HasMore ? MemorySourceSnapshotPageStatus.PageReturned : MemorySourceSnapshotPageStatus.EndOfSource,
                MemorySourceSnapshotHashScope.PageScoped,
                MemorySourceSnapshotProviderVersions.WorkflowRuntime),
            page.Items);
    }

    private static async Task<MemorySourcePageSlice> ReadPageAsync(
        IReadOnlyList<WorkflowSourcePage> sources,
        MemorySourceSnapshotCursor? cursor,
        int? take,
        Guid scopeId,
        CancellationToken cancellationToken)
    {
        var descriptor = MemorySourceSnapshotCursor.ReadDescriptorOrThrow(
            cursor,
            MemorySourceKind.WorkflowRuntime,
            scopeId,
            MemorySourceSnapshotProviderVersions.WorkflowRuntime);
        var sourceCounts = new List<WorkflowSourcePageCount>(sources.Count);
        foreach (var source in sources)
        {
            sourceCounts.Add(new WorkflowSourcePageCount(source, await source.CountAsync(cancellationToken)));
        }

        var totalItemCount = sourceCounts.Sum(item => item.Count);
        var startPosition = descriptor?.Position ?? 0;
        if (descriptor is not null)
        {
            var anchor = await ReadItemIdAtPositionAsync(sourceCounts, descriptor.Position - 1, cancellationToken);
            if (anchor is null || anchor.Value != descriptor.LastItemId)
            {
                MemorySourceSnapshotCursor.ThrowStaleAnchor(
                    cursor!.Value,
                    MemorySourceKind.WorkflowRuntime,
                    scopeId,
                    MemorySourceSnapshotProviderVersions.WorkflowRuntime,
                    "Workflow runtime source cursor anchor is stale or no longer matches the ordered source item at the recorded position.");
            }
        }

        var pageSize = MemorySourceSnapshotPage.NormalizeTake(take);
        var pageItems = new List<MemorySourceItem>(pageSize);
        var remainingSkip = startPosition;
        foreach (var sourceCount in sourceCounts)
        {
            if (pageItems.Count == pageSize)
            {
                break;
            }

            if (remainingSkip >= sourceCount.Count)
            {
                remainingSkip -= sourceCount.Count;
                continue;
            }

            var sourceSkip = remainingSkip;
            remainingSkip = 0;
            var sourceTake = Math.Min(pageSize - pageItems.Count, sourceCount.Count - sourceSkip);
            if (sourceTake <= 0)
            {
                continue;
            }

            pageItems.AddRange(await sourceCount.Source.ReadPageAsync(sourceSkip, sourceTake, cancellationToken));
        }

        var hasMore = startPosition + pageItems.Count < totalItemCount;
        MemorySourceSnapshotCursor? nextCursor = hasMore && pageItems.Count > 0
            ? MemorySourceSnapshotCursor.Create(
                MemorySourceKind.WorkflowRuntime,
                scopeId,
                MemorySourceSnapshotProviderVersions.WorkflowRuntime,
                startPosition + pageItems.Count,
                pageItems[^1].Id)
            : null;
        var snapshotHash = MemorySourceSnapshotHasher.Compute(
            MemorySourceSnapshotProviderVersions.WorkflowRuntime,
            scopeId.ToString("D"),
            startPosition.ToString(CultureInfo.InvariantCulture),
            string.Join("|", pageItems.Select(item => item.ContentHash)));
        return new MemorySourcePageSlice(pageItems, totalItemCount, nextCursor, hasMore, snapshotHash);
    }

    private static async Task<MemorySourceItemId?> ReadItemIdAtPositionAsync(
        IReadOnlyList<WorkflowSourcePageCount> sourceCounts,
        int position,
        CancellationToken cancellationToken)
    {
        if (position < 0)
        {
            return null;
        }

        var remaining = position;
        foreach (var sourceCount in sourceCounts)
        {
            if (remaining >= sourceCount.Count)
            {
                remaining -= sourceCount.Count;
                continue;
            }

            return await sourceCount.Source.ReadItemIdAsync(remaining, cancellationToken);
        }

        return null;
    }

    private static WorkflowSourcePage CreateSource<T>(
        MemorySourceEntityKind entityKind,
        IQueryable<T> query,
        Expression<Func<T, Guid>> orderKey,
        Func<T, MemorySourceItem> map)
        where T : class
        => new(
            entityKind,
            cancellationToken => query.CountAsync(cancellationToken),
            async (skip, take, cancellationToken) => (await query
                    .OrderBy(orderKey)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync(cancellationToken))
                .Select(map)
                .ToList(),
            async (index, cancellationToken) => (await query
                    .OrderBy(orderKey)
                    .Skip(index)
                    .Take(1)
                    .ToListAsync(cancellationToken))
                .Select(map)
                .FirstOrDefault()
                ?.Id);

    private static IQueryable<T> FilterByRunId<T>(IQueryable<T> query, Guid? runId)
        where T : class
        => runId.HasValue
            ? query.Where(item => EF.Property<Guid>(item, nameof(WorkflowRunRecordEntity.RunId)) == runId.Value)
            : query;

    private static MemorySourceItem MapRun(WorkflowRunRecordEntity run)
    {
        var itemId = BuildItemId(run.RunId, MemorySourceEntityKind.WorkflowRun, run.RunId);
        var content = BuildContent(
            ("Summary", run.Summary),
            ("State", run.State.ToString()),
            ("Backend", run.Backend.ToString()),
            ("Backend run id", run.BackendRunId));
        var contentHash = MemorySourceSnapshotHasher.Compute(
            run.RunId.ToString("D"),
            run.WorkflowId.ToString("D"),
            run.VersionId.ToString("D"),
            run.State.ToString(),
            run.Backend.ToString(),
            run.BackendRunId,
            run.Summary,
            run.CreatedAtUtc.ToString("O"),
            run.UpdatedAtUtc.ToString("O"));

        return new MemorySourceItem(
            itemId,
            MemorySourceKind.WorkflowRuntime,
            MemorySourceEntityKind.WorkflowRun,
            $"Workflow run {run.RunId:D}",
            content,
            contentHash,
            run.CreatedAtUtc,
            run.UpdatedAtUtc,
            BuildProvenance(run.RunId, MemorySourceEntityKind.WorkflowRun, run.RunId, $"/workflows/runs/{run.RunId:D}"),
            InternalReadOnlyPermission("Workflow run snapshots expose runtime status and backend identity only."),
            Layout: null,
            Links: [],
            References:
            [
                Reference("workflow-definition", run.WorkflowId, 0),
                Reference("workflow-version", run.VersionId, 1)
            ],
            StorageReference: null,
            Metadata(
                ("state", run.State.ToString()),
                ("backend", run.Backend.ToString()),
                ("backendRunId", run.BackendRunId)));
    }

    private static MemorySourceItem MapEvent(WorkflowEventRecordEntity workflowEvent)
    {
        var itemId = BuildItemId(workflowEvent.RunId, MemorySourceEntityKind.WorkflowEvent, workflowEvent.Id);
        var hasPayload = HasPayload(workflowEvent.PayloadJson);
        var content = BuildContent(
            ("Kind", workflowEvent.Kind.ToString()),
            ("Node id", workflowEvent.NodeId),
            ("Message", workflowEvent.Message),
            ("Payload", RedactJson(workflowEvent.PayloadJson)));
        var contentHash = MemorySourceSnapshotHasher.Compute(
            workflowEvent.Id.ToString("D"),
            workflowEvent.RunId.ToString("D"),
            workflowEvent.Kind.ToString(),
            workflowEvent.NodeId,
            workflowEvent.Message,
            workflowEvent.PayloadJson,
            workflowEvent.CreatedAtUtc.ToString("O"));

        return new MemorySourceItem(
            itemId,
            MemorySourceKind.WorkflowRuntime,
            MemorySourceEntityKind.WorkflowEvent,
            $"{workflowEvent.Kind} event",
            content,
            contentHash,
            workflowEvent.CreatedAtUtc,
            workflowEvent.CreatedAtUtc,
            BuildProvenance(workflowEvent.RunId, MemorySourceEntityKind.WorkflowEvent, workflowEvent.Id, $"/workflows/runs/{workflowEvent.RunId:D}/events/{workflowEvent.Id:D}"),
            InternalRedactedPermission(
                hasPayload,
                "Workflow event snapshots redact payload JSON before exposure."),
            Layout: null,
            Links: BuildLinks(workflowEvent.RunId, itemId, [new LinkTarget(MemorySourceEntityKind.WorkflowRun, workflowEvent.RunId, "BelongsToRun")]),
            References: [Reference("workflow-node", workflowEvent.NodeId, 0)],
            StorageReference: null,
            Metadata(
                ("kind", workflowEvent.Kind.ToString()),
                ("nodeId", workflowEvent.NodeId ?? string.Empty)))
        {
            HashPolicy = hasPayload
                ? MemorySourceHashPolicy.RestrictedRawPayloadIntegrity(
                    "Workflow event hash includes raw payload JSON. Use only for non-exportable source integrity checks.")
                : MemorySourceHashPolicy.InternalIntegrity
        };
    }

    private static MemorySourceItem MapExternalRequest(WorkflowExternalRequestRecordEntity request)
    {
        var itemId = BuildItemId(request.RunId, MemorySourceEntityKind.WorkflowExternalRequest, request.Id);
        var hasPayload = HasPayload(request.RequestJson) || HasPayload(request.ResponseJson);
        var content = BuildContent(
            ("Kind", request.Kind.ToString()),
            ("Node id", request.NodeId),
            ("Event name", request.EventName),
            ("Request", RedactJson(request.RequestJson)),
            ("Response", RedactJson(request.ResponseJson)),
            ("Responded", request.RespondedAtUtc.HasValue.ToString()));
        var contentHash = MemorySourceSnapshotHasher.Compute(
            request.Id.ToString("D"),
            request.RunId.ToString("D"),
            request.Kind.ToString(),
            request.NodeId,
            request.EventName,
            request.RequestJson,
            request.ResponseJson,
            request.CreatedAtUtc.ToString("O"),
            request.RespondedAtUtc?.ToString("O"));

        return new MemorySourceItem(
            itemId,
            MemorySourceKind.WorkflowRuntime,
            MemorySourceEntityKind.WorkflowExternalRequest,
            request.EventName,
            content,
            contentHash,
            request.CreatedAtUtc,
            request.RespondedAtUtc ?? request.CreatedAtUtc,
            BuildProvenance(request.RunId, MemorySourceEntityKind.WorkflowExternalRequest, request.Id, $"/workflows/runs/{request.RunId:D}/external-requests/{request.Id:D}"),
            InternalRedactedPermission(
                hasPayload,
                "Workflow external request snapshots redact request and response JSON before exposure."),
            Layout: null,
            Links: BuildLinks(request.RunId, itemId, [new LinkTarget(MemorySourceEntityKind.WorkflowRun, request.RunId, "BelongsToRun")]),
            References: [Reference("workflow-node", request.NodeId, 0)],
            StorageReference: null,
            Metadata(
                ("kind", request.Kind.ToString()),
                ("nodeId", request.NodeId),
                ("eventName", request.EventName),
                ("responded", request.RespondedAtUtc.HasValue.ToString())))
        {
            HashPolicy = hasPayload
                ? MemorySourceHashPolicy.RestrictedRawPayloadIntegrity(
                    "Workflow external request hash includes raw request or response JSON. Use only for non-exportable source integrity checks.")
                : MemorySourceHashPolicy.InternalIntegrity
        };
    }

    private static MemorySourceItem MapArtifact(WorkflowArtifactRecordEntity artifact)
    {
        var itemId = BuildItemId(artifact.RunId, MemorySourceEntityKind.WorkflowArtifact, artifact.Id);
        var content = BuildContent(
            ("Name", artifact.Name),
            ("Kind", artifact.Kind.ToString()),
            ("Content type", artifact.ContentType),
            ("Storage path", artifact.StoragePath),
            ("Summary", artifact.Summary),
            ("Node id", artifact.NodeId));
        var contentHash = MemorySourceSnapshotHasher.Compute(
            artifact.Id.ToString("D"),
            artifact.RunId.ToString("D"),
            artifact.Kind.ToString(),
            artifact.NodeId,
            artifact.Name,
            artifact.ContentType,
            artifact.StoragePath,
            artifact.Summary,
            artifact.CreatedAtUtc.ToString("O"));

        return new MemorySourceItem(
            itemId,
            MemorySourceKind.WorkflowRuntime,
            MemorySourceEntityKind.WorkflowArtifact,
            artifact.Name,
            content,
            contentHash,
            artifact.CreatedAtUtc,
            artifact.CreatedAtUtc,
            BuildProvenance(artifact.RunId, MemorySourceEntityKind.WorkflowArtifact, artifact.Id, $"/workflows/runs/{artifact.RunId:D}/artifacts/{artifact.Id:D}"),
            InternalReadOnlyPermission("Workflow artifact snapshots expose artifact summaries and storage locators, not artifact payload bytes."),
            Layout: null,
            Links: BuildLinks(artifact.RunId, itemId, [new LinkTarget(MemorySourceEntityKind.WorkflowRun, artifact.RunId, "BelongsToRun")]),
            References: [Reference("workflow-node", artifact.NodeId, 0)],
            StorageReference: ResolveStorageReference(artifact),
            Metadata: Metadata(
                ("kind", artifact.Kind.ToString()),
                ("nodeId", artifact.NodeId ?? string.Empty),
                ("contentType", artifact.ContentType),
                ("storagePath", artifact.StoragePath)));
    }

    private static MemorySourceItemId BuildItemId(
        Guid scopeId,
        MemorySourceEntityKind entityKind,
        Guid sourceEntityId)
        => MemorySourceItemId.Create(
            MemorySourceKind.WorkflowRuntime,
            scopeId,
            entityKind,
            sourceEntityId.ToString("D"));

    private static MemorySourceProvenance BuildProvenance(
        Guid scopeId,
        MemorySourceEntityKind entityKind,
        Guid sourceEntityId,
        string sourceRoute)
        => new(
            MemorySourceKind.WorkflowRuntime,
            scopeId,
            entityKind,
            sourceEntityId.ToString("D"),
            sourceRoute);

    private static MemorySourcePermissionContext InternalReadOnlyPermission(string redactionPolicy)
        => new(
            MemorySourceAccessMode.ReadOnly,
            MemorySourceSensitivity.Internal,
            ContainsSensitivePayload: false,
            redactionPolicy,
            "Source-grounded workflow runtime evidence.");

    private static MemorySourcePermissionContext InternalRedactedPermission(
        bool containsSensitivePayload,
        string redactionPolicy)
        => new(
            containsSensitivePayload ? MemorySourceAccessMode.Redacted : MemorySourceAccessMode.ReadOnly,
            MemorySourceSensitivity.Internal,
            containsSensitivePayload,
            redactionPolicy,
            "Source-grounded workflow runtime evidence.");

    private static string BuildContent(params (string Label, string? Value)[] fields)
        => string.Join(
            Environment.NewLine,
            fields
                .Where(field => !string.IsNullOrWhiteSpace(field.Value))
                .Select(field => $"{field.Label}: {WorkflowExecutorRedaction.RedactText(field.Value)}"));

    private static string RedactJson(string? json)
        => HasPayload(json) ? WorkflowExecutorRedaction.RedactSettingsJson(json) : string.Empty;

    private static bool HasPayload(string? value)
        => !string.IsNullOrWhiteSpace(value) &&
           !string.Equals(value.Trim(), "{}", StringComparison.Ordinal);

    private static IReadOnlyList<MemorySourceLink> BuildLinks(
        Guid scopeId,
        MemorySourceItemId sourceId,
        IReadOnlyList<LinkTarget> targets)
        => targets
            .Select(target => new MemorySourceLink(
                sourceId,
                BuildItemId(scopeId, target.EntityKind, target.EntityId),
                target.Kind,
                IsUserAuthored: false))
            .OrderBy(link => link.TargetId.Value, StringComparer.Ordinal)
            .ThenBy(link => link.Kind, StringComparer.Ordinal)
            .ToList();

    private static MemorySourceReference Reference(string referenceKind, Guid referenceId, int orderIndex)
        => new(referenceKind, referenceId.ToString("D"), orderIndex);

    private static MemorySourceReference Reference(string referenceKind, string? referenceId, int orderIndex)
        => new(referenceKind, referenceId ?? string.Empty, orderIndex);

    private static MemorySourceStorageReference? ResolveStorageReference(WorkflowArtifactRecordEntity artifact)
    {
        if (string.IsNullOrWhiteSpace(artifact.StoragePath))
        {
            return null;
        }

        return new MemorySourceStorageReference(
            "workflow-runtime",
            "storage-path",
            artifact.StoragePath.Trim(),
            artifact.ContentType,
            artifact.Name);
    }

    private static IReadOnlyDictionary<string, string> Metadata(params (string Key, string Value)[] values)
        => values.ToDictionary(
            value => value.Key,
            value => value.Value,
            StringComparer.Ordinal);

    private sealed record MemorySourcePageSlice(
        IReadOnlyList<MemorySourceItem> Items,
        int TotalItemCount,
        MemorySourceSnapshotCursor? NextCursor,
        bool HasMore,
        string SnapshotHash);

    private sealed record WorkflowSourcePage(
        MemorySourceEntityKind EntityKind,
        Func<CancellationToken, Task<int>> CountAsync,
        Func<int, int, CancellationToken, Task<IReadOnlyList<MemorySourceItem>>> ReadPageAsync,
        Func<int, CancellationToken, Task<MemorySourceItemId?>> ReadItemIdAsync);

    private sealed record WorkflowSourcePageCount(
        WorkflowSourcePage Source,
        int Count);

    private sealed record LinkTarget(
        MemorySourceEntityKind EntityKind,
        Guid EntityId,
        string Kind);
}

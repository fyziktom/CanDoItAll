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
        var runId = request.RunId?.Value;
        var scopeId = runId ?? Guid.Empty;
        var items = new List<MemorySourceItem>();

        items.AddRange((await FilterByRunId(dbContext.Set<WorkflowRunRecordEntity>().AsNoTracking(), runId)
                .ToListAsync(cancellationToken))
            .Select(MapRun));
        items.AddRange((await FilterByRunId(dbContext.Set<WorkflowEventRecordEntity>().AsNoTracking(), runId)
                .ToListAsync(cancellationToken))
            .Select(MapEvent));
        items.AddRange((await FilterByRunId(dbContext.Set<WorkflowExternalRequestRecordEntity>().AsNoTracking(), runId)
                .ToListAsync(cancellationToken))
            .Select(MapExternalRequest));
        items.AddRange((await FilterByRunId(dbContext.Set<WorkflowArtifactRecordEntity>().AsNoTracking(), runId)
                .ToListAsync(cancellationToken))
            .Select(MapArtifact));

        var allItems = items
            .OrderBy(item => item.Id.Value, StringComparer.Ordinal)
            .ToList();
        var pageItems = MemorySourceSnapshotPage.Apply(
            allItems,
            request.Cursor,
            request.Take,
            out var nextCursor,
            out var hasMore);
        var snapshotHash = MemorySourceSnapshotHasher.Compute(allItems.Select(item => item.ContentHash).ToArray());

        return new MemorySourceSnapshot(
            new MemorySourceSnapshotManifest(
                MemorySourceSnapshotId.Create(MemorySourceKind.WorkflowRuntime, scopeId, snapshotHash),
                MemorySourceKind.WorkflowRuntime,
                scopeId,
                DateTimeOffset.UtcNow,
                allItems.Count,
                nextCursor,
                hasMore),
            pageItems);
    }

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
                ("nodeId", workflowEvent.NodeId ?? string.Empty)));
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
                ("responded", request.RespondedAtUtc.HasValue.ToString())));
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

    private sealed record LinkTarget(
        MemorySourceEntityKind EntityKind,
        Guid EntityId,
        string Kind);
}

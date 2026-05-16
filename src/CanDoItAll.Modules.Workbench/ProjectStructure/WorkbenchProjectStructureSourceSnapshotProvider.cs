using System.Globalization;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

public sealed class WorkbenchProjectStructureSourceSnapshotProvider(
    ProjectWorkbenchService projectWorkbenchService,
    IDbContextFactory<AppDbContext> dbContextFactory) : IProjectStructureSourceSnapshotProvider
{
    private const string SurfaceKind = "project-structure";

    public async Task<MemorySourceSnapshot> ReadSnapshotAsync(
        ProjectStructureSourceSnapshotRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ProjectId == Guid.Empty)
        {
            throw new ArgumentException("Project id is required.", nameof(request));
        }

        var surface = await projectWorkbenchService.GetStructureAsync(request.ProjectId, cancellationToken);
        var nodeTimestamps = await LoadNodeTimestampsAsync(request.ProjectId, cancellationToken);
        var nodeItems = surface.Nodes
            .Select(node => MapNode(request.ProjectId, node, surface.Links, nodeTimestamps.GetValueOrDefault(node.Id)))
            .ToList();
        var linkItems = surface.Links
            .Select(link => MapLink(request.ProjectId, link))
            .ToList();
        var allItems = nodeItems
            .Concat(linkItems)
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
                MemorySourceSnapshotId.Create(MemorySourceKind.WorkbenchProjectStructure, request.ProjectId, snapshotHash),
                MemorySourceKind.WorkbenchProjectStructure,
                request.ProjectId,
                DateTimeOffset.UtcNow,
                allItems.Count,
                nextCursor,
                hasMore),
            pageItems);
    }

    private async Task<IReadOnlyDictionary<string, NodeTimestamp>> LoadNodeTimestampsAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        return await dbContext.Set<ProjectObjectRecord>()
            .AsNoTracking()
            .Where(item => item.ProjectId == projectId)
            .Select(item => new
            {
                item.NodeKey,
                item.CreatedAtUtc,
                item.UpdatedAtUtc
            })
            .ToDictionaryAsync(
                item => item.NodeKey,
                item => new NodeTimestamp(item.CreatedAtUtc, item.UpdatedAtUtc),
                StringComparer.Ordinal,
                cancellationToken);
    }

    private static MemorySourceItem MapNode(
        Guid projectId,
        ProjectStructureNode node,
        IReadOnlyList<ProjectStructureLink> surfaceLinks,
        NodeTimestamp? timestamp)
    {
        var itemId = BuildItemId(projectId, MemorySourceEntityKind.ProjectNode, node.Id);
        var links = surfaceLinks
            .Where(link => string.Equals(link.SourceId, node.Id, StringComparison.Ordinal) ||
                           string.Equals(link.TargetId, node.Id, StringComparison.Ordinal))
            .Select(link => new MemorySourceLink(
                BuildItemId(projectId, MemorySourceEntityKind.ProjectNode, link.SourceId),
                BuildItemId(projectId, MemorySourceEntityKind.ProjectNode, link.TargetId),
                link.Kind.ToString(),
                link.IsUserAuthored))
            .OrderBy(link => link.SourceId.Value, StringComparer.Ordinal)
            .ThenBy(link => link.TargetId.Value, StringComparer.Ordinal)
            .ThenBy(link => link.Kind, StringComparer.Ordinal)
            .ToList();
        var references = node.NodeReferences?.Entries
            .Where(reference => !string.IsNullOrWhiteSpace(reference.ReferenceKind) &&
                                !string.IsNullOrWhiteSpace(reference.ReferenceId))
            .OrderBy(reference => reference.ReferenceKind, StringComparer.Ordinal)
            .ThenBy(reference => reference.OrderIndex)
            .Select(reference => new MemorySourceReference(
                reference.ReferenceKind.Trim(),
                reference.ReferenceId.Trim(),
                reference.OrderIndex))
            .ToList() ?? [];
        var content = string.Join(
            Environment.NewLine,
            new[]
            {
                $"Title: {node.Title}",
                $"Subtitle: {node.Subtitle}",
                $"Object type: {node.ObjectType}",
                $"Subtype: {node.ObjectSubtype}",
                $"Status: {node.Status}",
                $"Progress: {node.ProgressMode} {node.ProgressPercent}",
                $"Notes: {node.Notes}",
                $"Route: {node.Route}"
            });
        var contentHash = MemorySourceSnapshotHasher.Compute(
            node.Id,
            node.ParentId,
            node.ObjectType.ToString(),
            node.ObjectSubtype,
            node.Title,
            node.Subtitle,
            node.Status,
            node.Notes,
            node.Route,
            node.ArtifactKind,
            node.ArtifactId?.ToString("D"),
            node.MediaRelativePath,
            node.MediaContentType,
            node.MediaOriginalFileName,
            node.X.ToString("R", CultureInfo.InvariantCulture),
            node.Y.ToString("R", CultureInfo.InvariantCulture),
            ResolveZIndex(node.MetadataJson)?.ToString(),
            node.StartUtc?.ToString("O"),
            node.EndUtc?.ToString("O"),
            node.DurationSeconds?.ToString(),
            node.MetadataJson,
            node.StorageObjectReferenceJson,
            string.Join("|", links.Select(link => $"{link.SourceId.Value}>{link.TargetId.Value}>{link.Kind}>{link.IsUserAuthored}")),
            string.Join("|", references.Select(reference => $"{reference.ReferenceKind}>{reference.ReferenceId}>{reference.OrderIndex}")));

        return new MemorySourceItem(
            itemId,
            MemorySourceKind.WorkbenchProjectStructure,
            MemorySourceEntityKind.ProjectNode,
            node.Title,
            content,
            contentHash,
            timestamp?.CreatedAtUtc,
            timestamp?.UpdatedAtUtc,
            new MemorySourceProvenance(
                MemorySourceKind.WorkbenchProjectStructure,
                projectId,
                MemorySourceEntityKind.ProjectNode,
                node.Id,
                node.Route),
            new MemorySourcePermissionContext(
                MemorySourceAccessMode.ReadOnly,
                MemorySourceSensitivity.Internal,
                ContainsSensitivePayload: false,
                RedactionPolicy: "Workbench snapshots expose source metadata and summaries only.",
                AllowedFutureUsageSummary: "Source-grounded project structure evidence."),
            new MemorySourceLayoutMetadata(
                node.X,
                node.Y,
                ResolveZIndex(node.MetadataJson),
                node.StartUtc,
                node.EndUtc,
                node.DurationSeconds,
                SurfaceKind,
                string.IsNullOrWhiteSpace(node.MetadataJson) ? "{}" : node.MetadataJson),
            links,
            references,
            ResolveStorageReference(node),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["parentId"] = node.ParentId ?? string.Empty,
                ["objectType"] = node.ObjectType.ToString(),
                ["objectSubtype"] = node.ObjectSubtype,
                ["status"] = node.Status,
                ["artifactKind"] = node.ArtifactKind,
                ["artifactId"] = node.ArtifactId?.ToString("D") ?? string.Empty,
                ["projectRole"] = node.ProjectRole.ToString()
            });
    }

    private static MemorySourceItem MapLink(Guid projectId, ProjectStructureLink link)
    {
        var sourceEntityId = $"{link.SourceId}>{link.TargetId}>{link.Kind}>{link.IsUserAuthored}";
        var itemId = BuildItemId(projectId, MemorySourceEntityKind.ProjectLink, sourceEntityId);
        var title = $"{link.SourceId} {link.Kind} {link.TargetId}";
        var contentHash = MemorySourceSnapshotHasher.Compute(
            link.SourceId,
            link.TargetId,
            link.Kind.ToString(),
            link.IsUserAuthored.ToString());

        return new MemorySourceItem(
            itemId,
            MemorySourceKind.WorkbenchProjectStructure,
            MemorySourceEntityKind.ProjectLink,
            title,
            title,
            contentHash,
            CreatedAtUtc: null,
            UpdatedAtUtc: null,
            new MemorySourceProvenance(
                MemorySourceKind.WorkbenchProjectStructure,
                projectId,
                MemorySourceEntityKind.ProjectLink,
                sourceEntityId,
                $"/projects/{projectId:D}/structure"),
            new MemorySourcePermissionContext(
                MemorySourceAccessMode.ReadOnly,
                MemorySourceSensitivity.Internal,
                ContainsSensitivePayload: false,
                RedactionPolicy: "Workbench link snapshots contain relationship metadata only.",
                AllowedFutureUsageSummary: "Source-grounded project relationship evidence."),
            Layout: null,
            [
                new MemorySourceLink(
                    BuildItemId(projectId, MemorySourceEntityKind.ProjectNode, link.SourceId),
                    BuildItemId(projectId, MemorySourceEntityKind.ProjectNode, link.TargetId),
                    link.Kind.ToString(),
                    link.IsUserAuthored)
            ],
            References: [],
            StorageReference: null,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["sourceId"] = link.SourceId,
                ["targetId"] = link.TargetId,
                ["kind"] = link.Kind.ToString(),
                ["isUserAuthored"] = link.IsUserAuthored.ToString()
            });
    }

    private static MemorySourceItemId BuildItemId(
        Guid projectId,
        MemorySourceEntityKind entityKind,
        string sourceEntityId)
        => MemorySourceItemId.Create(
            MemorySourceKind.WorkbenchProjectStructure,
            projectId,
            entityKind,
            sourceEntityId);

    private static int? ResolveZIndex(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            foreach (var propertyName in new[] { "zIndex", "z", "layoutZ" })
            {
                if (document.RootElement.TryGetProperty(propertyName, out var property) &&
                    property.TryGetInt32(out var zIndex))
                {
                    return zIndex;
                }
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static MemorySourceStorageReference? ResolveStorageReference(ProjectStructureNode node)
    {
        if (!string.IsNullOrWhiteSpace(node.StorageObjectReferenceJson))
        {
            return new MemorySourceStorageReference(
                "workbench",
                "storage-reference-json",
                node.StorageObjectReferenceJson.Trim(),
                node.MediaContentType,
                node.MediaOriginalFileName);
        }

        if (string.IsNullOrWhiteSpace(node.MediaRelativePath))
        {
            return null;
        }

        return new MemorySourceStorageReference(
            "workbench",
            "relative-path",
            node.MediaRelativePath.Trim(),
            node.MediaContentType,
            node.MediaOriginalFileName);
    }

    private sealed record NodeTimestamp(DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
}

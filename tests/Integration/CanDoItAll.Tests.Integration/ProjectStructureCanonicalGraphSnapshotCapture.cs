using System.Security.Cryptography;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.ProjectStructure;

namespace CanDoItAll.Tests.Integration;

internal sealed class ProjectStructureCanonicalGraphSnapshotCapture(
    ProjectStructureAgentService projectStructureService,
    ProjectsService projectsService,
    IWorkspacePathAccessGuard workspacePathAccessGuard)
{
    public async Task<ProjectStructureCanonicalGraphSnapshot> CaptureAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var graph = await projectStructureService.GetStructureAsync(
            projectId,
            new ProjectStructureReadRequest(
                IncludeLinks: true,
                IncludeLayout: true,
                IncludeMetadata: true,
                IncludeNotes: true,
                IncludeAssets: true,
                Source: ProjectStructureReadSource.CanonicalCurrent),
            cancellationToken);
        var managedAssets = await CaptureManagedAssetsAsync(graph.Nodes, cancellationToken);
        var relevantProjectIds = graph.Nodes
            .Where(node => node.RelatedProjectId.HasValue)
            .Select(node => node.RelatedProjectId!.Value)
            .Append(projectId)
            .ToHashSet();
        var hierarchyEdges = (await projectsService.ListHierarchyLinksAsync(cancellationToken))
            .Where(link =>
                relevantProjectIds.Contains(link.ParentProjectId) ||
                relevantProjectIds.Contains(link.ChildProjectId))
            .Select(link => new ProjectStructureCanonicalHierarchyEdgeSnapshot(
                link.ParentProjectId,
                link.ChildProjectId))
            .OrderBy(edge => edge.ParentProjectId)
            .ThenBy(edge => edge.ChildProjectId)
            .ToArray();

        return new ProjectStructureCanonicalGraphSnapshot(
            graph.Nodes
                .Select(ToNodeSnapshot)
                .OrderBy(node => node.Id, StringComparer.Ordinal)
                .ToArray(),
            graph.Links
                .Select(link => new ProjectStructureCanonicalLinkSnapshot(
                    link.SourceId,
                    link.TargetId,
                    link.Kind,
                    link.IsUserAuthored))
                .OrderBy(link => link.SourceId, StringComparer.Ordinal)
                .ThenBy(link => link.TargetId, StringComparer.Ordinal)
                .ThenBy(link => link.Kind)
                .ThenBy(link => link.IsUserAuthored)
                .ToArray(),
            managedAssets,
            hierarchyEdges);
    }

    private async Task<ProjectStructureCanonicalManagedAssetSnapshot[]> CaptureManagedAssetsAsync(
        IReadOnlyList<ProjectStructureNodeSummary> nodes,
        CancellationToken cancellationToken)
    {
        var assets = new List<ProjectStructureCanonicalManagedAssetSnapshot>();
        foreach (var node in nodes
                     .Where(node => !string.IsNullOrWhiteSpace(node.MediaRelativePath))
                     .OrderBy(node => node.Id, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var resolution = workspacePathAccessGuard.ResolveManagedFilePath(node.MediaRelativePath!);
            if (!resolution.IsSuccess)
            {
                throw new InvalidOperationException(
                    $"Canonical managed asset '{node.Id}' could not be resolved: {resolution.Message}");
            }

            await using var stream = new FileStream(
                resolution.FullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var contentLength = stream.Length;
            var sha256 = Convert.ToHexString(
                await SHA256.HashDataAsync(stream, cancellationToken));
            assets.Add(new ProjectStructureCanonicalManagedAssetSnapshot(
                node.Id,
                node.MediaRelativePath!,
                node.MediaContentType,
                node.MediaOriginalFileName,
                contentLength,
                sha256));
        }

        return assets.ToArray();
    }

    private static ProjectStructureCanonicalNodeSnapshot ToNodeSnapshot(
        ProjectStructureNodeSummary node)
    {
        return new ProjectStructureCanonicalNodeSnapshot(
            node.Id,
            node.ParentId,
            node.ObjectType,
            node.ObjectSubtype,
            node.Title,
            node.Subtitle,
            node.Status,
            node.Notes,
            node.MetadataJson,
            node.ArtifactKind,
            node.ArtifactId,
            node.ProgressMode,
            node.ProgressPercent,
            node.Priority,
            node.EffectivePriority,
            node.StartUtc,
            node.EndUtc,
            node.ProjectRole,
            node.RelatedProjectId,
            node.ParentProjectCount,
            node.X,
            node.Y,
            node.DurationSeconds);
    }
}

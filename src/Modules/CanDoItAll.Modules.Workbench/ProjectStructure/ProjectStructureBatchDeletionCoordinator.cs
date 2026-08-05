namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureBatchDeletionCoordinator
{
    private readonly ProjectStructureBatchDeletionOperations operations;

    public ProjectStructureBatchDeletionCoordinator(ProjectWorkbenchService projectWorkbenchService)
        : this(ProjectStructureBatchDeletionOperations.Create(projectWorkbenchService))
    {
    }

    internal ProjectStructureBatchDeletionCoordinator(ProjectStructureBatchDeletionOperations operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        this.operations = operations;
    }

    public Task<ProjectStructureDeletionResult> DeleteNodesAsync(
        Guid projectId,
        IReadOnlyList<string>? nodeIds,
        CancellationToken cancellationToken = default)
    {
        return DeleteNodesAsync(
            projectId,
            NormalizeSelection(nodeIds),
            cancellationToken);
    }

    internal ProjectStructureBatchDeletionSelection NormalizeSelection(
        IReadOnlyList<string>? nodeIds)
    {
        var requestedNodeIds = NormalizeNodeIds(nodeIds);
        if (requestedNodeIds.Count == 0)
        {
            throw new ProjectStructureDeletionBatchRejectedException(
                ProjectStructureDeletionBatchRejectionReason.SelectedNodesRequired,
                "At least one project-structure node id is required.",
                requestedNodeIds);
        }

        return new ProjectStructureBatchDeletionSelection(requestedNodeIds);
    }

    internal async Task<ProjectStructureDeletionResult> DeleteNodesAsync(
        Guid projectId,
        ProjectStructureBatchDeletionSelection selection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selection);
        var requestedNodeIds = selection.NodeIds;
        var surface = await operations.GetStructureAsync(projectId, cancellationToken);
        var visibleNodeIds = surface.Nodes
            .Select(node => node.Id)
            .ToHashSet(StringComparer.Ordinal);
        var deletedNodeIds = new HashSet<string>(StringComparer.Ordinal);
        var deletionWarnings = new List<ProjectStructureDeletionWarning>();
        var recoveries = new List<ProjectStructureDeletionRecovery>();

        await ReplayMissingBranchesAsync(
            projectId,
            requestedNodeIds,
            visibleNodeIds,
            deletedNodeIds,
            deletionWarnings,
            recoveries,
            cancellationToken);

        surface = await operations.GetStructureAsync(projectId, cancellationToken);
        var deleteRootIds = ResolveDeleteRootNodeIds(surface.Nodes, requestedNodeIds);
        if (deleteRootIds.Count == 0)
        {
            return ResolveReplayOnlyResult(
                projectId,
                requestedNodeIds,
                deletedNodeIds,
                deletionWarnings,
                recoveries);
        }

        await DeleteIndependentBranchesAsync(
            projectId,
            deleteRootIds,
            deletedNodeIds,
            deletionWarnings,
            recoveries,
            cancellationToken);

        if (recoveries.Count > 0)
        {
            throw CreateBatchDeletionPartialCommitException(
                projectId,
                recoveries,
                deletedNodeIds.Count,
                deletionWarnings);
        }

        return new ProjectStructureDeletionResult(
            deletedNodeIds.Count,
            deletionWarnings);
    }

    private async Task ReplayMissingBranchesAsync(
        Guid projectId,
        IReadOnlyList<string> requestedNodeIds,
        IReadOnlySet<string> visibleNodeIds,
        HashSet<string> deletedNodeIds,
        List<ProjectStructureDeletionWarning> deletionWarnings,
        List<ProjectStructureDeletionRecovery> recoveries,
        CancellationToken cancellationToken)
    {
        foreach (var requestedNodeId in requestedNodeIds.Where(id => !visibleNodeIds.Contains(id)))
        {
            try
            {
                var replay = await operations.ReplayDeletionAsync(
                    projectId,
                    requestedNodeId,
                    cancellationToken);
                if (replay is null)
                {
                    continue;
                }

                deletedNodeIds.UnionWith(replay.DeletedNodeKeys);
                deletionWarnings.AddRange(replay.Warnings);
            }
            catch (ProjectStructureDeletionPartialCommitException exception)
            {
                recoveries.Add(exception.Recovery);
            }
        }
    }

    private async Task DeleteIndependentBranchesAsync(
        Guid projectId,
        IReadOnlyList<string> deleteRootIds,
        HashSet<string> deletedNodeIds,
        List<ProjectStructureDeletionWarning> deletionWarnings,
        List<ProjectStructureDeletionRecovery> recoveries,
        CancellationToken cancellationToken)
    {
        foreach (var nodeId in deleteRootIds)
        {
            try
            {
                var deletion = await operations.DeleteObjectDetailedAsync(
                    projectId,
                    nodeId,
                    cancellationToken);
                deletionWarnings.AddRange(deletion.DeletionWarnings);
                var replay = await operations.ReplayDeletionAsync(
                    projectId,
                    nodeId,
                    cancellationToken);
                if (replay is not null)
                {
                    deletedNodeIds.UnionWith(replay.DeletedNodeKeys);
                }
            }
            catch (ProjectStructureDeletionPartialCommitException exception)
            {
                recoveries.Add(exception.Recovery);
            }
        }
    }

    private static ProjectStructureDeletionResult ResolveReplayOnlyResult(
        Guid projectId,
        IReadOnlyList<string> requestedNodeIds,
        IReadOnlySet<string> deletedNodeIds,
        IReadOnlyList<ProjectStructureDeletionWarning> deletionWarnings,
        IReadOnlyList<ProjectStructureDeletionRecovery> recoveries)
    {
        if (recoveries.Count > 0)
        {
            throw CreateBatchDeletionPartialCommitException(
                projectId,
                recoveries,
                deletedNodeIds.Count,
                deletionWarnings);
        }

        if (deletedNodeIds.Count == 0)
        {
            throw new ProjectStructureDeletionBatchRejectedException(
                ProjectStructureDeletionBatchRejectionReason.SelectedNodesNotFound,
                "None of the selected project-structure node ids were found.",
                requestedNodeIds);
        }

        return new ProjectStructureDeletionResult(
            deletedNodeIds.Count,
            deletionWarnings);
    }

    private static ProjectStructureDeletionBatchPartialCommitException CreateBatchDeletionPartialCommitException(
        Guid projectId,
        IReadOnlyList<ProjectStructureDeletionRecovery> recoveries,
        int completedNodeCount,
        IReadOnlyList<ProjectStructureDeletionWarning> warnings)
    {
        return new ProjectStructureDeletionBatchPartialCommitException(
            new ProjectStructureDeletionBatchRecovery(
                projectId,
                recoveries
                    .GroupBy(recovery => recovery.DurableMutationId)
                    .Select(group => group.First())
                    .ToArray(),
                completedNodeCount,
                warnings),
            $"{recoveries.Count} deleted branch cleanup operation(s) remain incomplete. Retry each exact durable mutation id; independent requested branches were still processed.");
    }

    private static IReadOnlyList<string> NormalizeNodeIds(IReadOnlyList<string>? nodeIds)
        => nodeIds?
            .Where(nodeId => !string.IsNullOrWhiteSpace(nodeId))
            .Select(nodeId => nodeId.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList()
            ?? [];

    private static IReadOnlyList<string> ResolveDeleteRootNodeIds(
        IReadOnlyList<ProjectStructureNode> nodes,
        IReadOnlyList<string> requestedNodeIds)
    {
        var nodesById = nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        var selectedIds = requestedNodeIds
            .Where(nodesById.ContainsKey)
            .ToHashSet(StringComparer.Ordinal);
        if (selectedIds.Count < 2)
        {
            return requestedNodeIds
                .Where(selectedIds.Contains)
                .ToList();
        }

        return requestedNodeIds
            .Where(selectedIds.Contains)
            .Where(nodeId => !HasSelectedAncestor(nodesById[nodeId], selectedIds, nodesById))
            .ToList();
    }

    private static bool HasSelectedAncestor(
        ProjectStructureNode node,
        IReadOnlySet<string> selectedIds,
        IReadOnlyDictionary<string, ProjectStructureNode> nodesById)
    {
        var visitedIds = new HashSet<string>(StringComparer.Ordinal);
        var parentId = node.ParentId;
        while (!string.IsNullOrWhiteSpace(parentId) && visitedIds.Add(parentId))
        {
            if (selectedIds.Contains(parentId))
            {
                return true;
            }

            parentId = nodesById.TryGetValue(parentId, out var parent)
                ? parent.ParentId
                : null;
        }

        return false;
    }
}

internal sealed record ProjectStructureBatchDeletionSelection(
    IReadOnlyList<string> NodeIds);

internal sealed record ProjectStructureBatchDeletionOperations(
    Func<Guid, CancellationToken, Task<ProjectStructureSurface>> GetStructureAsync,
    Func<Guid, string, CancellationToken, Task<ProjectStructureDeletionReplayResult?>> ReplayDeletionAsync,
    Func<Guid, string, CancellationToken, Task<ProjectStructureDeletionResult>> DeleteObjectDetailedAsync)
{
    public static ProjectStructureBatchDeletionOperations Create(
        ProjectWorkbenchService projectWorkbenchService)
    {
        ArgumentNullException.ThrowIfNull(projectWorkbenchService);

        return new ProjectStructureBatchDeletionOperations(
            projectWorkbenchService.GetStructureAsync,
            projectWorkbenchService.ReplayDeletionAsync,
            projectWorkbenchService.DeleteObjectDetailedAsync);
    }
}

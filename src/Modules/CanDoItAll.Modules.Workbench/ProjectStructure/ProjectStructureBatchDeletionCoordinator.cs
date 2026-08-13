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
        => DeleteNodesAsync(
            projectId,
            nodeIds,
            ProjectStructureManagedStorageDisposition.DeleteOwnedManagedFiles,
            cancellationToken);

    public Task<ProjectStructureDeletionResult> DeleteNodesAsync(
        Guid projectId,
        IReadOnlyList<string>? nodeIds,
        ProjectStructureManagedStorageDisposition managedStorageDisposition,
        CancellationToken cancellationToken = default)
    {
        return DeleteNodesAsync(
            projectId,
            NormalizeSelection(nodeIds),
            managedStorageDisposition,
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
        ProjectStructureManagedStorageDisposition managedStorageDisposition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ProjectStructureManagedStorageDispositionPolicy.EnsureSpecified(managedStorageDisposition);
        var requestedNodeIds = selection.NodeIds;
        var surface = await operations.GetStructureAsync(projectId, cancellationToken);
        var visibleNodeIds = surface.Nodes
            .Select(node => node.Id)
            .ToHashSet(StringComparer.Ordinal);
        var deletedNodeIds = new HashSet<string>(StringComparer.Ordinal);
        var replaylessDeletedNodeCount = 0;
        var deletionWarnings = new List<ProjectStructureDeletionWarning>();
        var recoveries = new List<ProjectStructureDeletionRecovery>();
        var branchFailures = new List<ProjectStructureDeletionBranchFailure>();

        await ReplayMissingBranchesAsync(
            projectId,
            requestedNodeIds,
            visibleNodeIds,
            deletedNodeIds,
            deletionWarnings,
            recoveries,
            branchFailures,
            managedStorageDisposition,
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
                recoveries,
                branchFailures);
        }

        replaylessDeletedNodeCount = await DeleteIndependentBranchesAsync(
            projectId,
            deleteRootIds,
            deletedNodeIds,
            deletionWarnings,
            recoveries,
            branchFailures,
            managedStorageDisposition,
            cancellationToken);

        if (recoveries.Count > 0 || branchFailures.Count > 0)
        {
            throw CreateBatchDeletionPartialCommitException(
                projectId,
                recoveries,
                branchFailures,
                ResolveCompletedNodeCount(
                    deletedNodeIds.Count,
                    replaylessDeletedNodeCount,
                    branchFailures),
                deletionWarnings);
        }

        return new ProjectStructureDeletionResult(
            ResolveCompletedNodeCount(
                deletedNodeIds.Count,
                replaylessDeletedNodeCount,
                branchFailures),
            deletionWarnings);
    }

    private async Task ReplayMissingBranchesAsync(
        Guid projectId,
        IReadOnlyList<string> requestedNodeIds,
        IReadOnlySet<string> visibleNodeIds,
        HashSet<string> deletedNodeIds,
        List<ProjectStructureDeletionWarning> deletionWarnings,
        List<ProjectStructureDeletionRecovery> recoveries,
        List<ProjectStructureDeletionBranchFailure> branchFailures,
        ProjectStructureManagedStorageDisposition managedStorageDisposition,
        CancellationToken cancellationToken)
    {
        foreach (var requestedNodeId in requestedNodeIds.Where(id => !visibleNodeIds.Contains(id)))
        {
            try
            {
                var replay = await operations.ReplayDeletionAsync(
                    projectId,
                    requestedNodeId,
                    managedStorageDisposition,
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
            catch (ProjectStructureDeletionDispositionMismatchException exception)
            {
                branchFailures.Add(CreateDispositionMismatchFailure(
                    requestedNodeId,
                    managedStorageDisposition,
                    exception.PersistedDisposition,
                    exception.CompletedNodeCount));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (
                deletedNodeIds.Count > 0 || recoveries.Count > 0 || branchFailures.Count > 0)
            {
                branchFailures.Add(CreateOperationFailure(
                    requestedNodeId,
                    managedStorageDisposition));
                throw CreateBatchDeletionPartialCommitException(
                    projectId,
                    recoveries,
                    branchFailures,
                    ResolveCompletedNodeCount(
                        deletedNodeIds.Count,
                        replaylessDeletedNodeCount: 0,
                        branchFailures),
                    deletionWarnings,
                    exception);
            }
        }
    }

    private async Task<int> DeleteIndependentBranchesAsync(
        Guid projectId,
        IReadOnlyList<string> deleteRootIds,
        HashSet<string> deletedNodeIds,
        List<ProjectStructureDeletionWarning> deletionWarnings,
        List<ProjectStructureDeletionRecovery> recoveries,
        List<ProjectStructureDeletionBranchFailure> branchFailures,
        ProjectStructureManagedStorageDisposition managedStorageDisposition,
        CancellationToken cancellationToken)
    {
        var replaylessDeletedNodeCount = 0;
        foreach (var nodeId in deleteRootIds)
        {
            try
            {
                var deletion = await operations.DeleteObjectDetailedAsync(
                    projectId,
                    nodeId,
                    managedStorageDisposition,
                    cancellationToken);
                deletionWarnings.AddRange(deletion.DeletionWarnings);
                if (deletion.DeletedNodeCount > 0)
                {
                    replaylessDeletedNodeCount += deletion.DeletedNodeCount;
                }
            }
            catch (ProjectStructureDeletionPartialCommitException exception)
            {
                recoveries.Add(exception.Recovery);
            }
            catch (ProjectStructureDeletionDispositionMismatchException exception)
            {
                branchFailures.Add(CreateDispositionMismatchFailure(
                    nodeId,
                    managedStorageDisposition,
                    exception.PersistedDisposition,
                    exception.CompletedNodeCount));
            }
            catch (ProjectManagedStorageBindingException exception)
            {
                branchFailures.Add(new ProjectStructureDeletionBranchFailure(
                    nodeId,
                    ProjectStructureDeletionBranchFailureKind.ManagedStorageValidation,
                    managedStorageDisposition,
                    exception.BindingId,
                    "The branch was not deleted because current managed-file ownership could not be verified.",
                    "Retry this branch with the node-only deletion option to preserve its files, or migrate the files into the active workspace first.")
                {
                    SuggestedRetryDisposition = ProjectStructureManagedStorageDisposition.RetainManagedFiles
                });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (
                ResolveCompletedNodeCount(
                    deletedNodeIds.Count,
                    replaylessDeletedNodeCount,
                    branchFailures) > 0 ||
                recoveries.Count > 0 ||
                branchFailures.Count > 0)
            {
                branchFailures.Add(CreateOperationFailure(
                    nodeId,
                    managedStorageDisposition));
                throw CreateBatchDeletionPartialCommitException(
                    projectId,
                    recoveries,
                    branchFailures,
                    ResolveCompletedNodeCount(
                        deletedNodeIds.Count,
                        replaylessDeletedNodeCount,
                        branchFailures),
                    deletionWarnings,
                    exception);
            }
        }

        return replaylessDeletedNodeCount;
    }

    private static ProjectStructureDeletionResult ResolveReplayOnlyResult(
        Guid projectId,
        IReadOnlyList<string> requestedNodeIds,
        IReadOnlySet<string> deletedNodeIds,
        IReadOnlyList<ProjectStructureDeletionWarning> deletionWarnings,
        IReadOnlyList<ProjectStructureDeletionRecovery> recoveries,
        IReadOnlyList<ProjectStructureDeletionBranchFailure> branchFailures)
    {
        if (recoveries.Count > 0 || branchFailures.Count > 0)
        {
            throw CreateBatchDeletionPartialCommitException(
                projectId,
                recoveries,
                branchFailures,
                ResolveCompletedNodeCount(
                    deletedNodeIds.Count,
                    replaylessDeletedNodeCount: 0,
                    branchFailures),
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
        IReadOnlyList<ProjectStructureDeletionBranchFailure> branchFailures,
        int completedNodeCount,
        IReadOnlyList<ProjectStructureDeletionWarning> warnings,
        Exception? innerException = null)
    {
        var message = branchFailures.Count switch
        {
            0 => $"{recoveries.Count} deleted branch cleanup operation(s) remain incomplete. Retry each exact durable mutation id; independent requested branches were still processed.",
            _ when recoveries.Count == 0 => $"{completedNodeCount} node(s) were confirmed deleted, and {branchFailures.Count} independent branch(es) require separate follow-up.",
            _ => $"{completedNodeCount} node(s) were confirmed deleted, {recoveries.Count} cleanup operation(s) remain incomplete, and {branchFailures.Count} independent branch(es) require separate follow-up."
        };
        return new ProjectStructureDeletionBatchPartialCommitException(
            new ProjectStructureDeletionBatchRecovery(
                projectId,
                recoveries
                    .GroupBy(recovery => recovery.DurableMutationId)
                    .Select(group => group.First())
                    .ToArray(),
                completedNodeCount,
                warnings)
            {
                BranchFailures = branchFailures.ToArray()
            },
            message,
            innerException);
    }

    private static ProjectStructureDeletionBranchFailure CreateOperationFailure(
        string rootNodeId,
        ProjectStructureManagedStorageDisposition managedStorageDisposition)
        => new(
            rootNodeId,
            ProjectStructureDeletionBranchFailureKind.OperationFailed,
            managedStorageDisposition,
            BindingId: null,
            "The branch could not be deleted because an unexpected operation failure occurred.",
            "Inspect the server log for this root, correct the failure, and retry the branch separately.");

    private static ProjectStructureDeletionBranchFailure CreateDispositionMismatchFailure(
        string rootNodeId,
        ProjectStructureManagedStorageDisposition requestedDisposition,
        ProjectStructureManagedStorageDisposition requiredDisposition,
        int completedNodeCount)
        => new(
            rootNodeId,
            ProjectStructureDeletionBranchFailureKind.DispositionMismatch,
            requestedDisposition,
            BindingId: null,
            "This branch was deleted, but its durable cleanup uses a different managed-file choice.",
            "Retry this branch separately using the originally recorded managed-file choice.")
        {
            SuggestedRetryDisposition = requiredDisposition,
            CompletedNodeCount = completedNodeCount
        };

    private static int ResolveCompletedNodeCount(
        int deletedNodeIdCount,
        int replaylessDeletedNodeCount,
        IReadOnlyList<ProjectStructureDeletionBranchFailure> branchFailures)
        => deletedNodeIdCount +
           replaylessDeletedNodeCount +
           branchFailures.Sum(static failure => failure.CompletedNodeCount);

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
    Func<Guid, string, ProjectStructureManagedStorageDisposition, CancellationToken, Task<ProjectStructureDeletionReplayResult?>> ReplayDeletionAsync,
    Func<Guid, string, ProjectStructureManagedStorageDisposition, CancellationToken, Task<ProjectStructureDeletionResult>> DeleteObjectDetailedAsync)
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

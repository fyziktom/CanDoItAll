using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

internal sealed record ProjectStructureDeletionReplayResult(
    IReadOnlyList<string> DeletedNodeKeys,
    IReadOnlyList<ProjectStructureDeletionWarning> Warnings);

public sealed class ProjectWorkbenchCrossModuleMutationService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock,
    ProjectCrossModuleMutationProcessingOptions processingOptions,
    ProjectCrossModuleMutationCoordinator mutationCoordinator,
    ProjectCrossModuleMutationProcessor mutationProcessor,
    ProjectManagedStorageDeletionPlanner managedStorageDeletionPlanner,
    ProjectStructureAssemblyService projectStructureAssemblyService)
{
    private const string TransferRetryGuidance =
        "Do not repeat the node move. The Workbench transfer is already committed; retry durable assignment reconciliation using the durable mutation id.";
    private const string DeletionRetryGuidance =
        "Retry the exact durable mutation id for this project and root through the deletion-cleanup operation; do not create or select a newer same-root deletion.";

    public async Task<int> DeleteObjectAsync(
        Guid projectId,
        string nodeKey,
        CancellationToken cancellationToken = default)
        => (await DeleteObjectDetailedAsync(projectId, nodeKey, cancellationToken))
            .DeletedNodeCount;

    public Task<ProjectStructureDeletionResult> DeleteObjectDetailedAsync(
        Guid projectId,
        string nodeKey,
        CancellationToken cancellationToken = default)
        => DeleteObjectDetailedAsync(
            projectId,
            nodeKey,
            ProjectStructureManagedStorageDisposition.DeleteOwnedManagedFiles,
            cancellationToken);

    public Task<ProjectStructureDeletionResult> DeleteObjectDetailedAsync(
        Guid projectId,
        string nodeKey,
        ProjectStructureManagedStorageDisposition managedStorageDisposition,
        CancellationToken cancellationToken = default)
        => DeleteObjectCoreAsync(
            projectId,
            nodeKey,
            managedStorageDisposition,
            reconcileDetachedTaskResource: true,
            cancellationToken);

    internal async Task<int> DeleteCanonicalTaskResourceAsync(
        Guid projectId,
        string nodeKey,
        CancellationToken cancellationToken = default)
        => (await DeleteObjectCoreAsync(
                projectId,
                nodeKey,
                ProjectStructureManagedStorageDisposition.DeleteOwnedManagedFiles,
                reconcileDetachedTaskResource: false,
                cancellationToken))
            .DeletedNodeCount;

    internal Task<ProjectStructureDeletionReplayResult?> ReplayDeletionAsync(
        Guid projectId,
        string rootNodeKey,
        Guid? durableMutationId = null,
        CancellationToken cancellationToken = default)
        => ReplayDeletionCoreAsync(
            projectId,
            rootNodeKey,
            durableMutationId,
            expectedDisposition: null,
            cancellationToken);

    internal Task<ProjectStructureDeletionReplayResult?> ReplayDeletionAsync(
        Guid projectId,
        string rootNodeKey,
        Guid durableMutationId,
        ProjectStructureManagedStorageDisposition expectedDisposition,
        CancellationToken cancellationToken = default)
        => ReplayDeletionCoreAsync(
            projectId,
            rootNodeKey,
            durableMutationId,
            expectedDisposition,
            cancellationToken);

    internal Task<ProjectStructureDeletionReplayResult?> ReplayDeletionAsync(
        Guid projectId,
        string rootNodeKey,
        ProjectStructureManagedStorageDisposition expectedDisposition,
        CancellationToken cancellationToken = default)
        => ReplayDeletionCoreAsync(
            projectId,
            rootNodeKey,
            durableMutationId: null,
            expectedDisposition,
            cancellationToken);

    private async Task<ProjectStructureDeletionReplayResult?> ReplayDeletionCoreAsync(
        Guid projectId,
        string rootNodeKey,
        Guid? durableMutationId,
        ProjectStructureManagedStorageDisposition? expectedDisposition,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootNodeKey);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        await using var mutationScope =
            await ProjectStructureSerializableMutationScope.BeginBindingWriteAsync(
                dbContext,
                ProjectStructureSerializableMutationScope.ForProject(projectId),
                cancellationToken);
        var mutation = await dbContext.Set<ProjectCrossModuleMutationRecord>()
            .Where(record =>
                record.ProjectId == projectId &&
                record.ScopeNodeKey == rootNodeKey &&
                (!durableMutationId.HasValue || record.Id == durableMutationId.Value) &&
                record.MutationKind == ProjectCrossModuleMutationKind.DeleteSubtree)
            .OrderBy(record => record.Status == ProjectCrossModuleMutationStatus.Completed)
            .ThenByDescending(record => record.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (mutation is null)
        {
            await mutationScope.CommitAsync(cancellationToken);
            if (durableMutationId.HasValue)
            {
                throw new ProjectStructureDeletionRecoveryNotFoundException(
                    projectId,
                    rootNodeKey,
                    durableMutationId.Value);
            }

            return null;
        }

        var payload = JsonSerializer.Deserialize<DeleteSubtreeMutationPayload>(
                          mutation.PayloadJson,
                          new JsonSerializerOptions(JsonSerializerDefaults.Web))
                      ?? throw new InvalidOperationException(
                          "Unable to deserialize the durable subtree-deletion payload.");
        if (!string.Equals(payload.RootNodeKey, rootNodeKey, StringComparison.Ordinal))
        {
            throw new ProjectStructureDeletionRecoveryNotFoundException(
                projectId,
                rootNodeKey,
                mutation.Id);
        }

        EnsureDispositionMatches(
            projectId,
            rootNodeKey,
            mutation.Id,
            payload,
            expectedDisposition);

        await mutationScope.CommitAsync(cancellationToken);
        await mutationScope.DisposeAsync();
        var completedPayload = payload;
        if (mutation.Status != ProjectCrossModuleMutationStatus.Completed)
        {
            completedPayload = await ProcessDeletionMutationOrThrowAsync(
                projectId,
                rootNodeKey,
                mutation.Id,
                ProjectStructureManagedStorageDispositionPolicy.ResolvePersisted(
                    payload.ManagedStorageDisposition),
                "The subtree is deleted, but durable cleanup remains incomplete.",
                cancellationToken);
        }

        return new ProjectStructureDeletionReplayResult(
            completedPayload.DeletedNodeKeys,
            MapDeletionWarnings(completedPayload.ManagedStorageOutcomes));
    }

    internal async Task<IReadOnlyList<ProjectStructureDeletionRecovery>> ListPendingDeletionRecoveriesAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var mutations = await dbContext.Set<ProjectCrossModuleMutationRecord>()
            .AsNoTracking()
            .Where(record =>
                record.ProjectId == projectId &&
                record.MutationKind == ProjectCrossModuleMutationKind.DeleteSubtree &&
                record.Status != ProjectCrossModuleMutationStatus.Completed)
            .OrderBy(record => record.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        var now = await ProjectCrossModuleMutationTimeSource.GetUtcNowAsync(
            dbContext,
            clock,
            cancellationToken);
        return mutations.Select(mutation =>
        {
            var payload = JsonSerializer.Deserialize<DeleteSubtreeMutationPayload>(
                              mutation.PayloadJson,
                              new JsonSerializerOptions(JsonSerializerDefaults.Web))
                          ?? throw new InvalidOperationException(
                              "Unable to deserialize the durable subtree-deletion payload.");
            if (!string.Equals(payload.RootNodeKey, mutation.ScopeNodeKey, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The durable subtree-deletion payload has an invalid root scope.");
            }

            return new ProjectStructureDeletionRecovery(
                projectId,
                payload.RootNodeKey,
                mutation.Id,
                MapDeletionReconciliationStatus(mutation.Status),
                ProjectStructureDeletionCommitState.WorkbenchCommitted,
                CanRetryNow(mutation, now),
                ResolveRetryAvailableAtUtc(mutation),
                DeletionRetryGuidance,
                ProjectStructureManagedStorageDispositionPolicy.ResolvePersisted(
                    payload.ManagedStorageDisposition));
        }).ToArray();
    }

    internal async Task<IReadOnlyList<ProjectStructureDeletionCompletionNotice>> ListDeletionCompletionNoticesAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var mutations = await dbContext.Set<ProjectCrossModuleMutationRecord>()
            .AsNoTracking()
            .Where(record =>
                record.ProjectId == projectId &&
                record.MutationKind == ProjectCrossModuleMutationKind.DeleteSubtree &&
                record.Status == ProjectCrossModuleMutationStatus.Completed)
            .OrderBy(record => record.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        return mutations
            .Select(mutation =>
            {
                var payload = JsonSerializer.Deserialize<DeleteSubtreeMutationPayload>(
                                  mutation.PayloadJson,
                                  new JsonSerializerOptions(JsonSerializerDefaults.Web))
                              ?? throw new InvalidOperationException(
                                  "Unable to deserialize the durable subtree-deletion payload.");
                return new ProjectStructureDeletionCompletionNotice(
                    projectId,
                    payload.RootNodeKey,
                    mutation.Id,
                    MapDeletionWarnings(payload.ManagedStorageOutcomes));
            })
            .Where(notice => notice.Warnings.Count > 0)
            .ToArray();
    }

    private async Task<ProjectStructureDeletionResult> DeleteObjectCoreAsync(
        Guid projectId,
        string nodeKey,
        ProjectStructureManagedStorageDisposition managedStorageDisposition,
        bool reconcileDetachedTaskResource,
        CancellationToken cancellationToken)
    {
        ProjectStructureManagedStorageDispositionPolicy.EnsureSpecified(managedStorageDisposition);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        await using var mutationScope =
            await ProjectStructureSerializableMutationScope.BeginBindingWriteAsync(
                dbContext,
                ProjectStructureSerializableMutationScope.ForProject(projectId),
                cancellationToken);

        var records = await dbContext.Set<ProjectObjectRecord>()
            .Where(item => item.ProjectId == projectId)
            .ToListAsync(cancellationToken);
        var root = records.FirstOrDefault(item => item.NodeKey == nodeKey && !item.IsSystemManaged);
        if (root is null)
        {
            var pendingDeletion = await dbContext.Set<ProjectCrossModuleMutationRecord>()
                .Where(record =>
                    record.ProjectId == projectId &&
                    record.ScopeNodeKey == nodeKey &&
                    record.MutationKind == ProjectCrossModuleMutationKind.DeleteSubtree &&
                    record.Status != ProjectCrossModuleMutationStatus.Completed)
                .OrderByDescending(record => record.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);
            if (pendingDeletion is not null)
            {
                var payload = JsonSerializer.Deserialize<DeleteSubtreeMutationPayload>(
                                  pendingDeletion.PayloadJson,
                                  new JsonSerializerOptions(JsonSerializerDefaults.Web))
                              ?? throw new InvalidOperationException(
                                  "Unable to deserialize the durable subtree-deletion payload.");
                EnsureDispositionMatches(
                    projectId,
                    nodeKey,
                    pendingDeletion.Id,
                    payload,
                    managedStorageDisposition);
                await mutationScope.CommitAsync(cancellationToken);
                await mutationScope.DisposeAsync();
                var completedPayload = await ProcessDeletionMutationOrThrowAsync(
                    projectId,
                    nodeKey,
                    pendingDeletion.Id,
                    ProjectStructureManagedStorageDispositionPolicy.ResolvePersisted(
                        payload.ManagedStorageDisposition),
                    "The subtree is deleted, but durable cleanup remains incomplete.",
                    cancellationToken);
                return new ProjectStructureDeletionResult(
                    completedPayload.DeletedNodeKeys.Count,
                    MapDeletionWarnings(completedPayload.ManagedStorageOutcomes));
            }

            var hiddenCount = await HideProjectedNodeAsync(
                dbContext,
                projectId,
                nodeKey,
                reconcileDetachedTaskResource,
                cancellationToken);
            await mutationScope.CommitAsync(cancellationToken);
            return new ProjectStructureDeletionResult(hiddenCount, []);
        }

        var keysToDelete = CollectEditableDescendantKeys(records, root.NodeKey);
        var linksToDelete = await dbContext.Set<ProjectObjectLinkRecord>()
            .Where(item =>
                item.ProjectId == projectId &&
                (keysToDelete.Contains(item.SourceNodeKey) || keysToDelete.Contains(item.TargetNodeKey)))
            .ToListAsync(cancellationToken);
        var recordsToDelete = records
            .Where(item => !item.IsSystemManaged && keysToDelete.Contains(item.NodeKey))
            .ToList();
        var candidateTaskNodeIds = reconcileDetachedTaskResource
            ? recordsToDelete
                .Where(record =>
                    !string.IsNullOrWhiteSpace(record.ParentNodeKey) &&
                    !keysToDelete.Contains(record.ParentNodeKey))
                .Select(record => record.ParentNodeKey!)
                .Concat(linksToDelete
                    .Where(link =>
                        link.LinkKind == ProjectObjectLinkKind.Uses &&
                        !keysToDelete.Contains(link.SourceNodeKey))
                    .Select(link => link.SourceNodeKey))
                .Distinct(StringComparer.Ordinal)
                .ToArray()
            : [];

        var storageDeletionPlan = managedStorageDisposition ==
            ProjectStructureManagedStorageDisposition.DeleteOwnedManagedFiles
                ? await managedStorageDeletionPlanner.PlanAsync(
                    dbContext,
                    recordsToDelete.Select(record => record.Id).ToArray(),
                    cancellationToken)
                : new ProjectManagedStorageDeletionPlan([], []);

        var mutationRecord = mutationCoordinator.Begin(
            projectId,
            root.NodeKey,
            ProjectCrossModuleMutationKind.DeleteSubtree,
            JsonSerializer.Serialize(new DeleteSubtreeMutationPayload(
                root.NodeKey,
                keysToDelete.OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                linksToDelete.Count,
                storageDeletionPlan.References,
                storageDeletionPlan.Outcomes,
                storageDeletionPlan.Candidates,
                managedStorageDisposition)));
        await dbContext.Set<ProjectCrossModuleMutationRecord>().AddAsync(mutationRecord, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (linksToDelete.Count > 0)
        {
            dbContext.RemoveRange(linksToDelete);
        }

        dbContext.RemoveRange(recordsToDelete);
        mutationCoordinator.MarkWorkbenchCommitted(mutationRecord);
        await dbContext.SaveChangesAsync(cancellationToken);
        await ProjectStructureTaskResourceGraphPolicy
            .ReconcileAfterStructureMutationAsync(
                dbContext,
                projectId,
                candidateTaskNodeIds,
                clock.GetUtcNow(),
                cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await mutationScope.CommitAsync(cancellationToken);
        await mutationScope.DisposeAsync();

        var completedDeletionPayload = await ProcessDeletionMutationOrThrowAsync(
            projectId,
            root.NodeKey,
            mutationRecord.Id,
            managedStorageDisposition,
            "Deleting the subtree committed the Workbench change, but durable cleanup failed.",
            cancellationToken);
        return new ProjectStructureDeletionResult(
            recordsToDelete.Count,
            MapDeletionWarnings(completedDeletionPayload.ManagedStorageOutcomes));
    }

    private static void EnsureDispositionMatches(
        Guid projectId,
        string rootNodeKey,
        Guid durableMutationId,
        DeleteSubtreeMutationPayload payload,
        ProjectStructureManagedStorageDisposition? expectedDisposition)
    {
        var persistedDisposition =
            ProjectStructureManagedStorageDispositionPolicy.ResolvePersisted(
                payload.ManagedStorageDisposition);
        if (!expectedDisposition.HasValue)
        {
            return;
        }

        ProjectStructureManagedStorageDispositionPolicy.EnsureSpecified(
            expectedDisposition.Value);
        if (persistedDisposition == expectedDisposition.Value)
        {
            return;
        }

        throw new ProjectStructureDeletionDispositionMismatchException(
            projectId,
            rootNodeKey,
            durableMutationId,
            expectedDisposition.Value,
            persistedDisposition,
            payload.DeletedNodeKeys.Count);
    }

    private static IReadOnlyList<ProjectStructureDeletionWarning> MapDeletionWarnings(
        IReadOnlyList<ProjectManagedStorageDeletionOutcome>? outcomes)
    {
        return (outcomes ?? [])
            .Where(outcome =>
                outcome.Kind != ProjectManagedStorageDeletionOutcomeKind.DeletedOrAlreadyAbsent)
            .Select(outcome => outcome.Kind switch
            {
                ProjectManagedStorageDeletionOutcomeKind.RetainedByProvider =>
                    new ProjectStructureDeletionWarning(
                        ProjectStructureDeletionWarningKind.ManagedStorageRetainedByProvider,
                        MapRetainedObject(outcome),
                        $"Managed media was retained by the immutable '{outcome.Reference.ProviderKind}' provider.",
                        "No cleanup retry is required. Retain the content address or remove any external pin according to provider policy."),
                ProjectManagedStorageDeletionOutcomeKind.RetainedWithoutOwnershipProof =>
                    new ProjectStructureDeletionWarning(
                        ProjectStructureDeletionWarningKind.ManagedStorageRetainedWithoutOwnershipProof,
                        MapRetainedObject(outcome),
                        $"Legacy managed media on '{outcome.Reference.ProviderKind}' was retained because physical ownership could not be proven.",
                        "Migrate the asset to a currently managed storage reference or remove the legacy object manually after verifying ownership."),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(outcome.Kind),
                    outcome.Kind,
                    null)
            })
            .ToArray();
    }

    private static ProjectDeletionRetainedObjectDescriptor MapRetainedObject(
        ProjectManagedStorageDeletionOutcome outcome)
        => new(
            outcome.Reference.ProviderKind,
            outcome.Reference.StorageId,
            outcome.Reference.LocatorKind,
            outcome.Reference.Locator,
            outcome.Reason);

    private async Task<int> HideProjectedNodeAsync(
        AppDbContext dbContext,
        Guid projectId,
        string nodeKey,
        bool reconcileDetachedTaskResource,
        CancellationToken cancellationToken)
    {
        var snapshot = await projectStructureAssemblyService.LoadAsync(dbContext, projectId, cancellationToken);
        var root = snapshot.Nodes.FirstOrDefault(item =>
            item.IsSystemManaged &&
            string.Equals(item.NodeKey, nodeKey, StringComparison.Ordinal));
        if (root is null || !IsProjectedDeleteCandidate(root))
        {
            return 0;
        }

        var removedNodeKeys = CollectSystemManagedDescendantKeys(snapshot.Nodes, root.NodeKey);
        var removedNodeKeyArray = removedNodeKeys.ToArray();
        var removedNodesByKey = snapshot.Nodes
            .Where(item => removedNodeKeys.Contains(item.NodeKey))
            .ToDictionary(item => item.NodeKey, StringComparer.Ordinal);
        var userLinksToDelete = await dbContext.Set<ProjectObjectLinkRecord>()
            .Where(item =>
                item.ProjectId == projectId &&
                !item.IsSystemManaged &&
                (removedNodeKeyArray.Contains(item.SourceNodeKey) || removedNodeKeyArray.Contains(item.TargetNodeKey)))
            .ToListAsync(cancellationToken);
        var candidateTaskNodeIds = reconcileDetachedTaskResource
            ? userLinksToDelete
                .Where(link =>
                    link.LinkKind == ProjectObjectLinkKind.Uses &&
                    !removedNodeKeys.Contains(link.SourceNodeKey))
                .Select(link => link.SourceNodeKey)
                .Distinct(StringComparer.Ordinal)
                .ToArray()
            : [];

        if (userLinksToDelete.Count > 0)
        {
            dbContext.RemoveRange(userLinksToDelete);
        }

        var layoutsByNodeKey = await dbContext.Set<ProjectStructureProjectionLayoutRecord>()
            .Where(item => item.ProjectId == projectId && removedNodeKeyArray.Contains(item.NodeKey))
            .ToDictionaryAsync(item => item.NodeKey, StringComparer.Ordinal, cancellationToken);
        var updatedAtUtc = clock.GetUtcNow();
        foreach (var removedNodeKey in removedNodeKeyArray)
        {
            if (removedNodesByKey.TryGetValue(removedNodeKey, out var removedNode))
            {
                UpsertHiddenProjectionLayout(dbContext, projectId, removedNode, updatedAtUtc, layoutsByNodeKey);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await ProjectStructureTaskResourceGraphPolicy
            .ReconcileAfterStructureMutationAsync(
                dbContext,
                projectId,
                candidateTaskNodeIds,
                clock.GetUtcNow(),
                cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return removedNodeKeys.Count;
    }

    private static void UpsertHiddenProjectionLayout(
        AppDbContext dbContext,
        Guid projectId,
        ProjectObjectRecord node,
        DateTimeOffset updatedAtUtc,
        IDictionary<string, ProjectStructureProjectionLayoutRecord> layoutsByNodeKey)
    {
        if (!layoutsByNodeKey.TryGetValue(node.NodeKey, out var layout))
        {
            layout = new ProjectStructureProjectionLayoutRecord
            {
                ProjectId = projectId,
                NodeKey = node.NodeKey
            };
            dbContext.Set<ProjectStructureProjectionLayoutRecord>().Add(layout);
            layoutsByNodeKey[node.NodeKey] = layout;
        }

        layout.PositionX = node.PositionX;
        layout.PositionY = node.PositionY;
        layout.IsHidden = true;
        layout.UpdatedAtUtc = updatedAtUtc;
    }

    public async Task<ProjectStructureSubprojectTransferResult?> MoveDescendantsToProjectAsync(
        Guid sourceProjectId,
        string sourceNodeKey,
        Guid targetProjectId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceNodeKey) || sourceProjectId == targetProjectId)
        {
            return null;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        await using var mutationScope =
            await ProjectStructureSerializableMutationScope.BeginBindingWriteAsync(
                dbContext,
                ProjectStructureSerializableMutationScope.ForProjects(
                    sourceProjectId,
                    targetProjectId),
                cancellationToken);

        var sourceRecords = await dbContext.Set<ProjectObjectRecord>()
            .Where(item => item.ProjectId == sourceProjectId)
            .ToListAsync(cancellationToken);
        if (sourceRecords.Count == 0)
        {
            return null;
        }

        var (movedNodeKeys, movedRootKeys) = CollectEditableMoveKeys(sourceRecords, sourceNodeKey);
        if (movedNodeKeys.Count == 0)
        {
            return new ProjectStructureSubprojectTransferResult(targetProjectId, [], 0, 0, []);
        }

        return await MoveCollectedNodesToProjectAsync(
            dbContext,
            sourceProjectId,
            sourceNodeKey,
            targetProjectId,
            sourceRecords,
            movedNodeKeys,
            movedRootKeys,
            ProjectCrossModuleMutationKind.MoveDescendants,
            "Moving descendants committed the Workbench change, but canonical assignment reconciliation failed.",
            mutationScope,
            cancellationToken);
    }

    public async Task<ProjectStructureSubprojectTransferResult?> MoveNodesToProjectAsync(
        Guid sourceProjectId,
        IReadOnlyCollection<string> sourceNodeKeys,
        Guid targetProjectId,
        bool includeDescendants = true,
        CancellationToken cancellationToken = default)
    {
        var normalizedSourceNodeKeys = sourceNodeKeys
            .Where(nodeKey => !string.IsNullOrWhiteSpace(nodeKey))
            .Select(nodeKey => nodeKey.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (normalizedSourceNodeKeys.Count == 0 || sourceProjectId == targetProjectId)
        {
            return null;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        await using var mutationScope =
            await ProjectStructureSerializableMutationScope.BeginBindingWriteAsync(
                dbContext,
                ProjectStructureSerializableMutationScope.ForProjects(
                    sourceProjectId,
                    targetProjectId),
                cancellationToken);

        var sourceRecords = await dbContext.Set<ProjectObjectRecord>()
            .Where(item => item.ProjectId == sourceProjectId)
            .ToListAsync(cancellationToken);
        if (sourceRecords.Count == 0)
        {
            return null;
        }

        var (movedNodeKeys, movedRootKeys) = CollectEditableSelectedMoveKeys(sourceRecords, normalizedSourceNodeKeys, includeDescendants);
        if (movedNodeKeys.Count == 0)
        {
            return new ProjectStructureSubprojectTransferResult(targetProjectId, [], 0, 0, []);
        }

        return await MoveCollectedNodesToProjectAsync(
            dbContext,
            sourceProjectId,
            BuildSelectedNodesScopeNodeKey(normalizedSourceNodeKeys),
            targetProjectId,
            sourceRecords,
            movedNodeKeys,
            movedRootKeys,
            ProjectCrossModuleMutationKind.MoveSelectedNodes,
            "Moving selected nodes committed the Workbench change, but canonical assignment reconciliation failed.",
            mutationScope,
            cancellationToken);
    }

    private async Task<ProjectStructureSubprojectTransferResult?> MoveCollectedNodesToProjectAsync(
        AppDbContext dbContext,
        Guid sourceProjectId,
        string scopeNodeKey,
        Guid targetProjectId,
        IReadOnlyCollection<ProjectObjectRecord> sourceRecords,
        HashSet<string> movedNodeKeys,
        HashSet<string> movedRootKeys,
        ProjectCrossModuleMutationKind mutationKind,
        string failureMessage,
        ProjectStructureSerializableMutationScope mutationScope,
        CancellationToken cancellationToken)
    {
        var targetProjectExists = await dbContext.Set<Project>()
            .AnyAsync(project => project.Id == targetProjectId, cancellationToken);
        if (!targetProjectExists)
        {
            throw new InvalidOperationException(
                $"Target project '{targetProjectId:D}' does not exist and cannot receive project-structure nodes.");
        }

        var targetNodeKeys = await dbContext.Set<ProjectObjectRecord>()
            .Where(item => item.ProjectId == targetProjectId)
            .Select(item => item.NodeKey)
            .ToListAsync(cancellationToken);
        if (targetNodeKeys.Any(movedNodeKeys.Contains))
        {
            return null;
        }

        var movedNodeIds = movedNodeKeys
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();

        var mutationRecord = mutationCoordinator.Begin(
            sourceProjectId,
            scopeNodeKey,
            mutationKind,
            JsonSerializer.Serialize(new MoveDescendantsMutationPayload(
                sourceProjectId,
                targetProjectId,
                scopeNodeKey,
                movedNodeIds,
                movedRootKeys.OrderBy(item => item, StringComparer.Ordinal).ToArray())));
        await dbContext.Set<ProjectCrossModuleMutationRecord>().AddAsync(mutationRecord, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var targetRootNodeKey = ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(targetProjectId);
        var sourceRootNodeKey = ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(sourceProjectId);
        var movedRecords = sourceRecords
            .Where(item => movedNodeKeys.Contains(item.NodeKey))
            .ToList();
        await ProjectNodeBindingStorage.LoadAsync(dbContext, movedRecords, cancellationToken);
        var movedRecordByNodeKey = movedRecords.ToDictionary(item => item.NodeKey, StringComparer.Ordinal);
        var originalParentByMovedNodeKey = movedRecords.ToDictionary(item => item.NodeKey, item => item.ParentNodeKey, StringComparer.Ordinal);
        var sourceCandidateTaskNodeIds = movedRecords
            .Select(record => record.ParentNodeKey)
            .Where(parentNodeKey =>
                !string.IsNullOrWhiteSpace(parentNodeKey) &&
                !movedNodeKeys.Contains(parentNodeKey))
            .Select(static parentNodeKey => parentNodeKey!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var updatedAtUtc = clock.GetUtcNow();

        foreach (var record in movedRecords)
        {
            var originalParentNodeKey = record.ParentNodeKey;
            var binding = ProjectNodeBindingStorage.ResolveForRuntime(record);
            record.ProjectId = targetProjectId;
            record.ParentNodeKey = movedNodeKeys.Contains(originalParentNodeKey ?? string.Empty)
                ? originalParentNodeKey
                : targetRootNodeKey;
            record.Binding = binding with
            {
                Route = RewriteProjectScopedRoute(binding.Route, sourceProjectId, targetProjectId)
            };
            record.UpdatedAtUtc = updatedAtUtc;
        }

        var leftBehindChildren = sourceRecords
            .Where(item =>
                !item.IsSystemManaged &&
                !movedNodeKeys.Contains(item.NodeKey) &&
                movedNodeKeys.Contains(item.ParentNodeKey ?? string.Empty))
            .ToList();
        foreach (var child in leftBehindChildren)
        {
            var originalMovedParentNodeKey = child.ParentNodeKey ?? string.Empty;
            var fallbackParentNodeKey = originalParentByMovedNodeKey.TryGetValue(originalMovedParentNodeKey, out var originalParentNodeKey) &&
                                        !string.IsNullOrWhiteSpace(originalParentNodeKey) &&
                                        !movedNodeKeys.Contains(originalParentNodeKey)
                ? originalParentNodeKey
                : sourceRootNodeKey;
            child.ParentNodeKey = fallbackParentNodeKey;
            child.UpdatedAtUtc = updatedAtUtc;
        }

        var linksToProcess = await dbContext.Set<ProjectObjectLinkRecord>()
            .Where(item =>
                item.ProjectId == sourceProjectId &&
                (movedNodeKeys.Contains(item.SourceNodeKey) || movedNodeKeys.Contains(item.TargetNodeKey)))
            .ToListAsync(cancellationToken);
        var movedLinkCount = 0;
        var removedBoundaryLinks = new List<ProjectStructureBoundaryLinkRemoval>();
        foreach (var link in linksToProcess)
        {
            if (IsLegacyEditableHierarchyLink(link, movedRecordByNodeKey))
            {
                dbContext.Remove(link);
                continue;
            }

            var hasMovedSource = movedNodeKeys.Contains(link.SourceNodeKey);
            var hasMovedTarget = movedNodeKeys.Contains(link.TargetNodeKey);
            if (hasMovedSource && hasMovedTarget)
            {
                link.ProjectId = targetProjectId;
                movedLinkCount++;
                continue;
            }

            removedBoundaryLinks.Add(new ProjectStructureBoundaryLinkRemoval(
                link.Id,
                link.SourceNodeKey,
                link.TargetNodeKey,
                link.LinkKind,
                link.IsSystemManaged));
            dbContext.Remove(link);
        }
        sourceCandidateTaskNodeIds.AddRange(linksToProcess
            .Where(link =>
                link.LinkKind == ProjectObjectLinkKind.Uses &&
                !movedNodeKeys.Contains(link.SourceNodeKey))
            .Select(link => link.SourceNodeKey));

        foreach (var record in movedRecords)
        {
            await ProjectNodeBindingStorage.PersistAsync(dbContext, record, cancellationToken);
        }

        mutationCoordinator.MarkWorkbenchCommitted(mutationRecord);
        await dbContext.SaveChangesAsync(cancellationToken);
        await ProjectStructureTaskResourceGraphPolicy
            .ReconcileAfterStructureMutationAsync(
                dbContext,
                sourceProjectId,
                sourceCandidateTaskNodeIds,
                updatedAtUtc,
                cancellationToken);
        await ProjectStructureTaskResourceGraphPolicy
            .ReconcileAfterStructureMutationAsync(
                dbContext,
                targetProjectId,
                movedRecords.Select(record => record.NodeKey),
                updatedAtUtc,
                cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await mutationScope.CommitAsync(cancellationToken);
        await mutationScope.DisposeAsync();

        await ProcessTransferMutationOrThrowAsync(
            targetProjectId,
            mutationRecord.Id,
            failureMessage,
            cancellationToken);
        return new ProjectStructureSubprojectTransferResult(
            targetProjectId,
            movedNodeIds,
            movedRootKeys.Count,
            movedLinkCount,
            removedBoundaryLinks
                .OrderBy(link => link.SourceNodeId, StringComparer.Ordinal)
                .ThenBy(link => link.TargetNodeId, StringComparer.Ordinal)
                .ThenBy(link => link.LinkKind)
                .ThenBy(link => link.LinkId)
                .ToList());
    }

    private async Task ProcessTransferMutationOrThrowAsync(
        Guid targetProjectId,
        Guid mutationId,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        ProjectCrossModuleMutationStatus status;
        try
        {
            status = await mutationProcessor.ProcessAsync(mutationId, cancellationToken)
                ?? ProjectCrossModuleMutationStatus.WorkbenchCommitted;
        }
        catch (Exception exception)
        {
            throw CreateTransferPartialCommitException(
                targetProjectId,
                mutationId,
                ProjectCrossModuleMutationStatus.WorkbenchCommitted,
                failureMessage,
                exception);
        }

        if (status != ProjectCrossModuleMutationStatus.Completed)
        {
            throw CreateTransferPartialCommitException(
                targetProjectId,
                mutationId,
                status,
                failureMessage);
        }
    }

    private static ProjectStructureTransferPartialCommitException CreateTransferPartialCommitException(
        Guid targetProjectId,
        Guid mutationId,
        ProjectCrossModuleMutationStatus mutationStatus,
        string failureMessage,
        Exception? innerException = null)
    {
        var recovery = new ProjectStructureTransferRecovery(
            targetProjectId,
            mutationId,
            MapTransferReconciliationStatus(mutationStatus),
            ProjectStructureTransferCommitState.WorkbenchCommitted,
            TransferRetryGuidance);
        return new ProjectStructureTransferPartialCommitException(
            recovery,
            $"{failureMessage} {TransferRetryGuidance}",
            innerException);
    }

    private static ProjectStructureTransferReconciliationStatus MapTransferReconciliationStatus(
        ProjectCrossModuleMutationStatus status)
    {
        return status switch
        {
            ProjectCrossModuleMutationStatus.Pending => ProjectStructureTransferReconciliationStatus.Pending,
            ProjectCrossModuleMutationStatus.WorkbenchCommitted => ProjectStructureTransferReconciliationStatus.WorkbenchCommitted,
            ProjectCrossModuleMutationStatus.Processing => ProjectStructureTransferReconciliationStatus.WorkbenchCommitted,
            ProjectCrossModuleMutationStatus.Completed => ProjectStructureTransferReconciliationStatus.Completed,
            ProjectCrossModuleMutationStatus.Failed => ProjectStructureTransferReconciliationStatus.Failed,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };
    }

    private async Task<DeleteSubtreeMutationPayload> ProcessDeletionMutationOrThrowAsync(
        Guid projectId,
        string rootNodeId,
        Guid mutationId,
        ProjectStructureManagedStorageDisposition managedStorageDisposition,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        ProjectStructureManagedStorageDispositionPolicy.EnsureSpecified(
            managedStorageDisposition);
        ProjectCrossModuleMutationProcessingResult? processingResult;
        try
        {
            processingResult = await mutationProcessor.ProcessWithPayloadAsync(
                mutationId,
                cancellationToken);
        }
        catch (Exception exception)
        {
            throw await CreateDeletionPartialCommitExceptionAsync(
                projectId,
                rootNodeId,
                mutationId,
                managedStorageDisposition,
                failureMessage,
                exception,
                CancellationToken.None);
        }

        var status = processingResult?.Status ??
            ProjectCrossModuleMutationStatus.WorkbenchCommitted;
        if (status != ProjectCrossModuleMutationStatus.Completed)
        {
            throw await CreateDeletionPartialCommitExceptionAsync(
                projectId,
                rootNodeId,
                mutationId,
                managedStorageDisposition,
                failureMessage);
        }

        try
        {
            return JsonSerializer.Deserialize<DeleteSubtreeMutationPayload>(
                       processingResult!.PayloadJson,
                       new JsonSerializerOptions(JsonSerializerDefaults.Web))
                   ?? throw new JsonException(
                       "The durable subtree-deletion completion receipt is empty.");
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw await CreateDeletionPartialCommitExceptionAsync(
                projectId,
                rootNodeId,
                mutationId,
                managedStorageDisposition,
                "The subtree is deleted and durable cleanup completed, but its completion receipt is invalid.",
                exception,
                CancellationToken.None);
        }
    }

    private async Task<ProjectStructureDeletionPartialCommitException>
        CreateDeletionPartialCommitExceptionAsync(
        Guid projectId,
        string rootNodeId,
        Guid mutationId,
        ProjectStructureManagedStorageDisposition managedStorageDisposition,
        string failureMessage,
        Exception? innerException = null,
        CancellationToken cancellationToken = default)
    {
        ProjectStructureManagedStorageDispositionPolicy.EnsureSpecified(
            managedStorageDisposition);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(
            cancellationToken);
        var mutation = await dbContext.Set<ProjectCrossModuleMutationRecord>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                record => record.Id == mutationId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"Durable subtree-deletion recovery '{mutationId:D}' is missing.",
                innerException);
        if (mutation.ProjectId != projectId ||
            mutation.MutationKind != ProjectCrossModuleMutationKind.DeleteSubtree ||
            !string.Equals(
                mutation.ScopeNodeKey,
                rootNodeId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Durable subtree-deletion recovery '{mutationId:D}' does not match project '{projectId:D}' and root '{rootNodeId}'.",
                innerException);
        }

        var now = await ProjectCrossModuleMutationTimeSource.GetUtcNowAsync(
            dbContext,
            clock,
            cancellationToken);
        var recovery = new ProjectStructureDeletionRecovery(
            projectId,
            rootNodeId,
            mutationId,
            MapDeletionReconciliationStatus(mutation.Status),
            ProjectStructureDeletionCommitState.WorkbenchCommitted,
            CanRetryNow(mutation, now),
            ResolveRetryAvailableAtUtc(mutation),
            DeletionRetryGuidance,
            managedStorageDisposition);
        return new ProjectStructureDeletionPartialCommitException(
            recovery,
            $"{failureMessage} {DeletionRetryGuidance}",
            innerException);
    }

    private static ProjectStructureDeletionReconciliationStatus MapDeletionReconciliationStatus(
        ProjectCrossModuleMutationStatus status)
    {
        return status switch
        {
            ProjectCrossModuleMutationStatus.Pending => ProjectStructureDeletionReconciliationStatus.Pending,
            ProjectCrossModuleMutationStatus.WorkbenchCommitted => ProjectStructureDeletionReconciliationStatus.WorkbenchCommitted,
            ProjectCrossModuleMutationStatus.Processing => ProjectStructureDeletionReconciliationStatus.Processing,
            ProjectCrossModuleMutationStatus.Completed => ProjectStructureDeletionReconciliationStatus.Completed,
            ProjectCrossModuleMutationStatus.Failed => ProjectStructureDeletionReconciliationStatus.Failed,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };
    }

    private bool CanRetryNow(
        ProjectCrossModuleMutationRecord mutation,
        DateTimeOffset now)
        => mutation.Status != ProjectCrossModuleMutationStatus.Processing ||
           !mutation.LastAttemptAtUtc.HasValue ||
           mutation.LastAttemptAtUtc.Value + processingOptions.LeaseDuration <= now;

    private DateTimeOffset? ResolveRetryAvailableAtUtc(
        ProjectCrossModuleMutationRecord mutation)
        => mutation.Status == ProjectCrossModuleMutationStatus.Processing &&
           mutation.LastAttemptAtUtc.HasValue
            ? mutation.LastAttemptAtUtc.Value + processingOptions.LeaseDuration
            : null;

    private static HashSet<string> CollectEditableDescendantKeys(
        IReadOnlyCollection<ProjectObjectRecord> records,
        string rootNodeKey)
    {
        var childrenByParent = records
            .Where(item => !item.IsSystemManaged && !string.IsNullOrWhiteSpace(item.ParentNodeKey))
            .GroupBy(item => item.ParentNodeKey!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        var collectedKeys = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        queue.Enqueue(rootNodeKey);

        while (queue.Count > 0)
        {
            var currentNodeKey = queue.Dequeue();
            if (!collectedKeys.Add(currentNodeKey))
            {
                continue;
            }

            if (!childrenByParent.TryGetValue(currentNodeKey, out var children))
            {
                continue;
            }

            foreach (var child in children)
            {
                queue.Enqueue(child.NodeKey);
            }
        }

        return collectedKeys;
    }

    private static HashSet<string> CollectSystemManagedDescendantKeys(
        IReadOnlyCollection<ProjectObjectRecord> records,
        string rootNodeKey)
    {
        var childrenByParent = records
            .Where(item => item.IsSystemManaged && !string.IsNullOrWhiteSpace(item.ParentNodeKey))
            .GroupBy(item => item.ParentNodeKey!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        var collectedKeys = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        queue.Enqueue(rootNodeKey);

        while (queue.Count > 0)
        {
            var currentNodeKey = queue.Dequeue();
            if (!collectedKeys.Add(currentNodeKey))
            {
                continue;
            }

            if (!childrenByParent.TryGetValue(currentNodeKey, out var children))
            {
                continue;
            }

            foreach (var child in children)
            {
                queue.Enqueue(child.NodeKey);
            }
        }

        return collectedKeys;
    }

    private static bool IsProjectedDeleteCandidate(ProjectObjectRecord node)
        => node.IsSystemManaged &&
           node.ObjectType is not ProjectObjectType.ProjectRoot and not ProjectObjectType.Phase;

    private static (HashSet<string> MovedNodeKeys, HashSet<string> MovedRootKeys) CollectEditableMoveKeys(
        IReadOnlyCollection<ProjectObjectRecord> sourceRecords,
        string sourceNodeKey)
    {
        var editableChildrenByParent = sourceRecords
            .Where(item => !item.IsSystemManaged && !string.IsNullOrWhiteSpace(item.ParentNodeKey))
            .GroupBy(item => item.ParentNodeKey!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        var movedNodeKeys = new HashSet<string>(StringComparer.Ordinal);
        var movedRootKeys = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        queue.Enqueue(sourceNodeKey);

        while (queue.Count > 0)
        {
            var currentNodeKey = queue.Dequeue();
            if (!editableChildrenByParent.TryGetValue(currentNodeKey, out var children))
            {
                continue;
            }

            foreach (var child in children)
            {
                if (!movedNodeKeys.Add(child.NodeKey))
                {
                    continue;
                }

                if (string.Equals(child.ParentNodeKey, sourceNodeKey, StringComparison.Ordinal))
                {
                    movedRootKeys.Add(child.NodeKey);
                }

                queue.Enqueue(child.NodeKey);
            }
        }

        return (movedNodeKeys, movedRootKeys);
    }

    private static (HashSet<string> MovedNodeKeys, HashSet<string> MovedRootKeys) CollectEditableSelectedMoveKeys(
        IReadOnlyCollection<ProjectObjectRecord> sourceRecords,
        IReadOnlyCollection<string> sourceNodeKeys,
        bool includeDescendants)
    {
        var editableRecordsByKey = sourceRecords
            .Where(item => !item.IsSystemManaged)
            .ToDictionary(item => item.NodeKey, StringComparer.Ordinal);
        var editableChildrenByParent = sourceRecords
            .Where(item => !item.IsSystemManaged && !string.IsNullOrWhiteSpace(item.ParentNodeKey))
            .GroupBy(item => item.ParentNodeKey!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        var movedNodeKeys = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();

        foreach (var sourceNodeKey in sourceNodeKeys)
        {
            if (!editableRecordsByKey.ContainsKey(sourceNodeKey))
            {
                continue;
            }

            if (movedNodeKeys.Add(sourceNodeKey) && includeDescendants)
            {
                queue.Enqueue(sourceNodeKey);
            }
        }

        while (queue.Count > 0)
        {
            var currentNodeKey = queue.Dequeue();
            if (!editableChildrenByParent.TryGetValue(currentNodeKey, out var children))
            {
                continue;
            }

            foreach (var child in children)
            {
                if (!movedNodeKeys.Add(child.NodeKey))
                {
                    continue;
                }

                queue.Enqueue(child.NodeKey);
            }
        }

        var movedRootKeys = movedNodeKeys
            .Where(nodeKey =>
                editableRecordsByKey.TryGetValue(nodeKey, out var record) &&
                !movedNodeKeys.Contains(record.ParentNodeKey ?? string.Empty))
            .ToHashSet(StringComparer.Ordinal);

        return (movedNodeKeys, movedRootKeys);
    }

    private static string BuildSelectedNodesScopeNodeKey(IReadOnlyList<string> sourceNodeKeys)
    {
        var scopeNodeKey = sourceNodeKeys.Count == 1
            ? sourceNodeKeys[0]
            : $"selected:{sourceNodeKeys[0]}";
        return scopeNodeKey.Length <= 160
            ? scopeNodeKey
            : scopeNodeKey[..160];
    }

    private static bool IsLegacyEditableHierarchyLink(
        ProjectObjectLinkRecord link,
        IReadOnlyDictionary<string, ProjectObjectRecord> movedRecordByNodeKey)
    {
        if (link.LinkKind != ProjectObjectLinkKind.Contains && link.LinkKind != ProjectObjectLinkKind.BelongsTo)
        {
            return false;
        }

        if (!movedRecordByNodeKey.TryGetValue(link.TargetNodeKey, out var targetNode) ||
            string.IsNullOrWhiteSpace(targetNode.ParentNodeKey))
        {
            return false;
        }

        var expectedKind = ResolveEditableHierarchyLinkKind(targetNode.ProjectId, targetNode.ParentNodeKey);
        return string.Equals(link.SourceNodeKey, targetNode.ParentNodeKey, StringComparison.Ordinal) &&
               link.LinkKind == expectedKind;
    }

    private static ProjectObjectLinkKind ResolveEditableHierarchyLinkKind(Guid projectId, string? parentNodeKey)
    {
        return string.Equals(
            parentNodeKey,
            ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(projectId),
            StringComparison.Ordinal)
            ? ProjectObjectLinkKind.Contains
            : ProjectObjectLinkKind.BelongsTo;
    }

    private static string RewriteProjectScopedRoute(string route, Guid sourceProjectId, Guid targetProjectId)
    {
        if (string.IsNullOrWhiteSpace(route))
        {
            return route;
        }

        var sourceStructureRoute = $"/projects/{sourceProjectId}/structure";
        if (string.Equals(route, sourceStructureRoute, StringComparison.OrdinalIgnoreCase))
        {
            return $"/projects/{targetProjectId}/structure";
        }

        var sourceCalendarRoute = $"/projects/{sourceProjectId}/calendar";
        if (string.Equals(route, sourceCalendarRoute, StringComparison.OrdinalIgnoreCase))
        {
            return $"/projects/{targetProjectId}/calendar";
        }

        var sourceProjectQueryRoute = $"/projects?projectId={sourceProjectId}";
        if (string.Equals(route, sourceProjectQueryRoute, StringComparison.OrdinalIgnoreCase))
        {
            return $"/projects?projectId={targetProjectId}";
        }

        return route;
    }
}

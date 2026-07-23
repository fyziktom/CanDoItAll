using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectWorkbenchCrossModuleMutationService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock,
    ProjectCrossModuleMutationCoordinator mutationCoordinator,
    ProjectCrossModuleMutationProcessor mutationProcessor,
    ProjectStructureAssemblyService projectStructureAssemblyService)
{
    public Task<int> DeleteObjectAsync(
        Guid projectId,
        string nodeKey,
        CancellationToken cancellationToken = default)
        => DeleteObjectCoreAsync(
            projectId,
            nodeKey,
            reconcileDetachedTaskResource: true,
            cancellationToken);

    internal Task<int> DeleteCanonicalTaskResourceAsync(
        Guid projectId,
        string nodeKey,
        CancellationToken cancellationToken = default)
        => DeleteObjectCoreAsync(
            projectId,
            nodeKey,
            reconcileDetachedTaskResource: false,
            cancellationToken);

    private async Task<int> DeleteObjectCoreAsync(
        Guid projectId,
        string nodeKey,
        bool reconcileDetachedTaskResource,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        await using var mutationScope =
            await ProjectStructureSerializableMutationScope.BeginAsync(
                dbContext,
                ProjectStructureSerializableMutationScope.ForProject(projectId),
                cancellationToken);

        var records = await dbContext.Set<ProjectObjectRecord>()
            .Where(item => item.ProjectId == projectId)
            .ToListAsync(cancellationToken);
        var root = records.FirstOrDefault(item => item.NodeKey == nodeKey && !item.IsSystemManaged);
        if (root is null)
        {
            var hiddenCount = await HideProjectedNodeAsync(
                dbContext,
                projectId,
                nodeKey,
                reconcileDetachedTaskResource,
                cancellationToken);
            await mutationScope.CommitAsync(cancellationToken);
            return hiddenCount;
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

        await ProjectNodeBindingStorage.LoadAsync(dbContext, recordsToDelete, cancellationToken);

        var mutationRecord = mutationCoordinator.Begin(
            projectId,
            root.NodeKey,
            ProjectCrossModuleMutationKind.DeleteSubtree,
            JsonSerializer.Serialize(new DeleteSubtreeMutationPayload(
                root.NodeKey,
                keysToDelete.OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                linksToDelete.Count)));
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

        await ProcessMutationOrThrowAsync(
            mutationRecord.Id,
            "Deleting the subtree committed the Workbench change, but canonical assignment reconciliation failed.",
            cancellationToken);
        return recordsToDelete.Count;
    }

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
            await ProjectStructureSerializableMutationScope.BeginAsync(
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
            return new ProjectStructureSubprojectTransferResult(targetProjectId, 0, 0);
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
            await ProjectStructureSerializableMutationScope.BeginAsync(
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
            return new ProjectStructureSubprojectTransferResult(targetProjectId, 0, 0);
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
        var targetNodeKeys = await dbContext.Set<ProjectObjectRecord>()
            .Where(item => item.ProjectId == targetProjectId)
            .Select(item => item.NodeKey)
            .ToListAsync(cancellationToken);
        if (targetNodeKeys.Any(movedNodeKeys.Contains))
        {
            return null;
        }

        var mutationRecord = mutationCoordinator.Begin(
            sourceProjectId,
            scopeNodeKey,
            mutationKind,
            JsonSerializer.Serialize(new MoveDescendantsMutationPayload(
                sourceProjectId,
                targetProjectId,
                scopeNodeKey,
                movedNodeKeys.OrderBy(item => item, StringComparer.Ordinal).ToArray(),
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
                continue;
            }

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

        await ProcessMutationOrThrowAsync(
            mutationRecord.Id,
            failureMessage,
            cancellationToken);
        return new ProjectStructureSubprojectTransferResult(targetProjectId, movedNodeKeys.Count, movedRootKeys.Count);
    }

    private async Task ProcessMutationOrThrowAsync(
        Guid mutationId,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        var status = await mutationProcessor.ProcessAsync(mutationId, cancellationToken);
        if (status == ProjectCrossModuleMutationStatus.Failed)
        {
            throw new InvalidOperationException(
                $"{failureMessage} The durable mutation is now marked failed for retry.");
        }
    }

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

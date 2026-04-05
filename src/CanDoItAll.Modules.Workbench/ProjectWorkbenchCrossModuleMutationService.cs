using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectWorkbenchCrossModuleMutationService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock,
    ProjectCrossModuleMutationCoordinator mutationCoordinator,
    ProjectCrossModuleMutationProcessor mutationProcessor)
{
    public async Task<int> DeleteObjectAsync(
        Guid projectId,
        string nodeKey,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);

        var records = await dbContext.Set<ProjectObjectRecord>()
            .Where(item => item.ProjectId == projectId)
            .ToListAsync(cancellationToken);
        var root = records.FirstOrDefault(item => item.NodeKey == nodeKey && !item.IsSystemManaged);
        if (root is null)
        {
            return 0;
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
        await ProjectNodeBindingStorage.NormalizeAndHydrateAsync(dbContext, recordsToDelete, cancellationToken);

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

        await ProcessMutationOrThrowAsync(
            mutationRecord.Id,
            "Deleting the subtree committed the Workbench change, but canonical assignment reconciliation failed.",
            cancellationToken);
        return recordsToDelete.Count;
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
            sourceNodeKey,
            ProjectCrossModuleMutationKind.MoveDescendants,
            JsonSerializer.Serialize(new MoveDescendantsMutationPayload(
                sourceProjectId,
                targetProjectId,
                sourceNodeKey,
                movedNodeKeys.OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                movedRootKeys.OrderBy(item => item, StringComparer.Ordinal).ToArray())));
        await dbContext.Set<ProjectCrossModuleMutationRecord>().AddAsync(mutationRecord, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var targetRootNodeKey = ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(targetProjectId);
        var movedRecords = sourceRecords
            .Where(item => movedNodeKeys.Contains(item.NodeKey))
            .ToList();
        await ProjectNodeBindingStorage.NormalizeAndHydrateAsync(dbContext, movedRecords, cancellationToken);
        var movedRecordByNodeKey = movedRecords.ToDictionary(item => item.NodeKey, StringComparer.Ordinal);
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

        foreach (var record in movedRecords)
        {
            await ProjectNodeBindingStorage.PersistAsync(dbContext, record, cancellationToken);
        }

        mutationCoordinator.MarkWorkbenchCommitted(mutationRecord);
        await dbContext.SaveChangesAsync(cancellationToken);

        await ProcessMutationOrThrowAsync(
            mutationRecord.Id,
            "Moving descendants committed the Workbench change, but canonical assignment reconciliation failed.",
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

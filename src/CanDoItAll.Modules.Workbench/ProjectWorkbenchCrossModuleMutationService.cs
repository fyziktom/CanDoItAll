using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectWorkbenchCrossModuleMutationService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock,
    IProjectPartyIntegrationBridge projectPartyIntegrationBridge,
    ProjectCrossModuleMutationCoordinator mutationCoordinator)
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

        var childrenByParent = records
            .Where(item => !item.IsSystemManaged && !string.IsNullOrWhiteSpace(item.ParentNodeKey))
            .GroupBy(item => item.ParentNodeKey!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        var keysToDelete = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        queue.Enqueue(root.NodeKey);

        while (queue.Count > 0)
        {
            var currentNodeKey = queue.Dequeue();
            if (!keysToDelete.Add(currentNodeKey))
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

        var linksToDelete = await dbContext.Set<ProjectObjectLinkRecord>()
            .Where(item => item.ProjectId == projectId &&
                (keysToDelete.Contains(item.SourceNodeKey) || keysToDelete.Contains(item.TargetNodeKey)))
            .ToListAsync(cancellationToken);
        var linkSnapshots = linksToDelete
            .Select(CloneProjectObjectLinkRecord)
            .ToList();
        var recordsToDelete = records
            .Where(item => !item.IsSystemManaged && keysToDelete.Contains(item.NodeKey))
            .ToList();
        await ProjectNodeBindingStorage.NormalizeAndHydrateAsync(dbContext, recordsToDelete, cancellationToken);
        var recordSnapshots = recordsToDelete
            .Select(CloneProjectObjectRecord)
            .ToList();

        var mutationRecord = mutationCoordinator.Begin(
            projectId,
            root.NodeKey,
            ProjectCrossModuleMutationKind.DeleteSubtree,
            JsonSerializer.Serialize(new
            {
                RootNodeKey = root.NodeKey,
                DeletedNodeKeys = keysToDelete.OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                LinkCount = linkSnapshots.Count
            }));
        await dbContext.Set<ProjectCrossModuleMutationRecord>().AddAsync(mutationRecord, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (linksToDelete.Count > 0)
        {
            dbContext.RemoveRange(linksToDelete);
        }

        dbContext.RemoveRange(recordsToDelete);
        mutationCoordinator.MarkWorkbenchCommitted(mutationRecord);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await projectPartyIntegrationBridge.DeleteAssignmentsForNodesAsync(
                projectId,
                keysToDelete.Select(key => new ProjectNodeReference(key)).ToList(),
                cancellationToken);
            await MarkMutationCompletedAsync(mutationRecord.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            try
            {
                await RestoreDeletedSubtreeAsync(recordSnapshots, linkSnapshots, cancellationToken);
                await MarkMutationCompensatedAsync(mutationRecord.Id, ex.Message, cancellationToken);
            }
            catch (Exception compensationEx)
            {
                await TryMarkFailedAsync(
                    mutationRecord.Id,
                    $"Delete compensation failed: {ex.Message} | rollback: {compensationEx.Message}",
                    cancellationToken);
                throw new InvalidOperationException(
                    "Deleting the subtree failed during canonical assignment reconciliation and Workbench rollback also failed.",
                    new AggregateException(ex, compensationEx));
            }

            throw new InvalidOperationException(
                "Deleting the subtree failed during canonical assignment reconciliation. The Workbench subtree was restored.",
                ex);
        }

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
            JsonSerializer.Serialize(new
            {
                SourceProjectId = sourceProjectId,
                TargetProjectId = targetProjectId,
                SourceNodeKey = sourceNodeKey,
                MovedNodeKeys = movedNodeKeys.OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                MovedRootKeys = movedRootKeys.OrderBy(item => item, StringComparer.Ordinal).ToArray()
            }));
        await dbContext.Set<ProjectCrossModuleMutationRecord>().AddAsync(mutationRecord, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var targetRootNodeKey = BuildProjectRootNodeKey(targetProjectId);
        var movedRecords = sourceRecords
            .Where(item => movedNodeKeys.Contains(item.NodeKey))
            .ToList();
        await ProjectNodeBindingStorage.NormalizeAndHydrateAsync(dbContext, movedRecords, cancellationToken);
        var movedRecordSnapshots = movedRecords
            .Select(CloneProjectObjectRecord)
            .ToList();
        var movedRecordByNodeKey = movedRecords.ToDictionary(item => item.NodeKey, StringComparer.Ordinal);
        var updatedAtUtc = clock.GetUtcNow();

        foreach (var record in movedRecords)
        {
            var originalParentNodeKey = record.ParentNodeKey;
            record.ProjectId = targetProjectId;
            record.ParentNodeKey = movedNodeKeys.Contains(originalParentNodeKey ?? string.Empty)
                ? originalParentNodeKey
                : targetRootNodeKey;
            record.Route = RewriteProjectScopedRoute(record.Route, sourceProjectId, targetProjectId);
            record.UpdatedAtUtc = updatedAtUtc;
        }

        var linksToProcess = await dbContext.Set<ProjectObjectLinkRecord>()
            .Where(item => item.ProjectId == sourceProjectId &&
                (movedNodeKeys.Contains(item.SourceNodeKey) || movedNodeKeys.Contains(item.TargetNodeKey)))
            .ToListAsync(cancellationToken);
        var linkSnapshots = linksToProcess
            .Select(CloneProjectObjectLinkRecord)
            .ToList();

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

        try
        {
            await projectPartyIntegrationBridge.MoveAssignmentsToProjectAsync(
                sourceProjectId,
                movedNodeKeys.Select(key => new ProjectNodeReference(key)).ToList(),
                targetProjectId,
                cancellationToken);
            await MarkMutationCompletedAsync(mutationRecord.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            try
            {
                await RestoreMovedDescendantsAsync(
                    sourceProjectId,
                    targetProjectId,
                    movedRecordSnapshots,
                    linkSnapshots,
                    movedRootKeys,
                    cancellationToken);
                await MarkMutationCompensatedAsync(mutationRecord.Id, ex.Message, cancellationToken);
            }
            catch (Exception compensationEx)
            {
                await TryMarkFailedAsync(
                    mutationRecord.Id,
                    $"Move compensation failed: {ex.Message} | rollback: {compensationEx.Message}",
                    cancellationToken);
                throw new InvalidOperationException(
                    "Moving descendants failed during canonical assignment reconciliation and Workbench rollback also failed.",
                    new AggregateException(ex, compensationEx));
            }

            throw new InvalidOperationException(
                "Moving descendants failed during canonical assignment reconciliation. The Workbench subtree move was rolled back.",
                ex);
        }

        return new ProjectStructureSubprojectTransferResult(targetProjectId, movedNodeKeys.Count, movedRootKeys.Count);
    }

    private async Task RestoreDeletedSubtreeAsync(
        IReadOnlyList<ProjectObjectRecord> recordSnapshots,
        IReadOnlyList<ProjectObjectLinkRecord> linkSnapshots,
        CancellationToken cancellationToken)
    {
        if (recordSnapshots.Count == 0 && linkSnapshots.Count == 0)
        {
            return;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);

        var recordIds = recordSnapshots.Select(item => item.Id).ToList();
        var existingRecordIds = await dbContext.Set<ProjectObjectRecord>()
            .Where(item => recordIds.Contains(item.Id))
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        var existingRecordIdSet = existingRecordIds.ToHashSet();
        foreach (var snapshot in recordSnapshots)
        {
            if (existingRecordIdSet.Contains(snapshot.Id))
            {
                continue;
            }

            dbContext.Set<ProjectObjectRecord>().Add(CloneProjectObjectRecord(snapshot));
        }

        var linkIds = linkSnapshots.Select(item => item.Id).ToList();
        var existingLinkIds = await dbContext.Set<ProjectObjectLinkRecord>()
            .Where(item => linkIds.Contains(item.Id))
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        var existingLinkIdSet = existingLinkIds.ToHashSet();
        foreach (var snapshot in linkSnapshots)
        {
            if (existingLinkIdSet.Contains(snapshot.Id))
            {
                continue;
            }

            dbContext.Set<ProjectObjectLinkRecord>().Add(CloneProjectObjectLinkRecord(snapshot));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        var restoredRecords = await dbContext.Set<ProjectObjectRecord>()
            .Where(item => recordIds.Contains(item.Id))
            .ToListAsync(cancellationToken);
        await ProjectNodeBindingStorage.NormalizeAndHydrateAsync(dbContext, restoredRecords, cancellationToken);
    }

    private async Task RestoreMovedDescendantsAsync(
        Guid sourceProjectId,
        Guid targetProjectId,
        IReadOnlyList<ProjectObjectRecord> recordSnapshots,
        IReadOnlyList<ProjectObjectLinkRecord> linkSnapshots,
        IReadOnlyCollection<string> movedRootKeys,
        CancellationToken cancellationToken)
    {
        if (recordSnapshots.Count == 0)
        {
            return;
        }

        var movedNodeKeys = recordSnapshots
            .Select(item => item.NodeKey)
            .ToHashSet(StringComparer.Ordinal);
        var targetRootNodeKey = BuildProjectRootNodeKey(targetProjectId);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);

        var currentMovedRecords = await dbContext.Set<ProjectObjectRecord>()
            .Where(item => movedNodeKeys.Contains(item.NodeKey))
            .ToListAsync(cancellationToken);
        var snapshotsByNodeKey = recordSnapshots.ToDictionary(item => item.NodeKey, StringComparer.Ordinal);
        foreach (var currentRecord in currentMovedRecords)
        {
            if (!snapshotsByNodeKey.TryGetValue(currentRecord.NodeKey, out var snapshot))
            {
                continue;
            }

            RestoreProjectObjectRecord(currentRecord, snapshot);
        }

        var existingMovedNodeKeys = currentMovedRecords
            .Select(item => item.NodeKey)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var snapshot in recordSnapshots)
        {
            if (existingMovedNodeKeys.Contains(snapshot.NodeKey))
            {
                continue;
            }

            dbContext.Set<ProjectObjectRecord>().Add(CloneProjectObjectRecord(snapshot));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var currentRelevantLinks = await dbContext.Set<ProjectObjectLinkRecord>()
            .Where(item =>
                (item.ProjectId == sourceProjectId || item.ProjectId == targetProjectId) &&
                (movedNodeKeys.Contains(item.SourceNodeKey) ||
                 movedNodeKeys.Contains(item.TargetNodeKey) ||
                 (item.ProjectId == targetProjectId &&
                  item.SourceNodeKey == targetRootNodeKey &&
                  movedRootKeys.Contains(item.TargetNodeKey))))
            .ToListAsync(cancellationToken);
        if (currentRelevantLinks.Count > 0)
        {
            dbContext.RemoveRange(currentRelevantLinks);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        foreach (var snapshot in linkSnapshots)
        {
            dbContext.Set<ProjectObjectLinkRecord>().Add(CloneProjectObjectLinkRecord(snapshot));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        var restoredRecords = await dbContext.Set<ProjectObjectRecord>()
            .Where(item => movedNodeKeys.Contains(item.NodeKey))
            .ToListAsync(cancellationToken);
        await ProjectNodeBindingStorage.NormalizeAndHydrateAsync(dbContext, restoredRecords, cancellationToken);
    }

    private Task MarkMutationCompletedAsync(Guid mutationId, CancellationToken cancellationToken)
    {
        return UpdateMutationStatusAsync(
            mutationId,
            mutationCoordinator.MarkCompleted,
            cancellationToken);
    }

    private Task MarkMutationCompensatedAsync(
        Guid mutationId,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        return UpdateMutationStatusAsync(
            mutationId,
            record => mutationCoordinator.MarkCompensated(record, errorMessage),
            cancellationToken);
    }

    private async Task TryMarkFailedAsync(
        Guid mutationId,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            await UpdateMutationStatusAsync(
                mutationId,
                record => mutationCoordinator.MarkFailed(record, errorMessage),
                cancellationToken);
        }
        catch
        {
        }
    }

    private async Task UpdateMutationStatusAsync(
        Guid mutationId,
        Action<ProjectCrossModuleMutationRecord> update,
        CancellationToken cancellationToken)
    {
        var mutationState = new ProjectCrossModuleMutationRecord();
        update(mutationState);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.Set<ProjectCrossModuleMutationRecord>()
            .Where(item => item.Id == mutationId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, mutationState.Status)
                .SetProperty(item => item.ErrorMessage, mutationState.ErrorMessage)
                .SetProperty(item => item.UpdatedAtUtc, mutationState.UpdatedAtUtc),
                cancellationToken);
    }

    private static ProjectObjectRecord CloneProjectObjectRecord(ProjectObjectRecord record)
    {
        return new ProjectObjectRecord
        {
            Id = record.Id,
            ProjectId = record.ProjectId,
            NodeKey = record.NodeKey,
            ObjectType = record.ObjectType,
            Title = record.Title,
            Subtitle = record.Subtitle,
            Status = record.Status,
            Notes = record.Notes,
            Route = record.Route,
            ExternalArtifactKind = record.ExternalArtifactKind,
            ExternalArtifactId = record.ExternalArtifactId,
            ObjectSubtype = record.ObjectSubtype,
            MediaRelativePath = record.MediaRelativePath,
            MediaContentType = record.MediaContentType,
            MediaOriginalFileName = record.MediaOriginalFileName,
            StorageObjectReferenceJson = record.StorageObjectReferenceJson,
            ProgressMode = record.ProgressMode,
            ProgressPercent = record.ProgressPercent,
            MarkerIcon = record.MarkerIcon,
            MarkerTone = record.MarkerTone,
            MarkerLabel = record.MarkerLabel,
            Priority = record.Priority,
            MetadataJson = record.MetadataJson,
            ParentNodeKey = record.ParentNodeKey,
            PositionX = record.PositionX,
            PositionY = record.PositionY,
            StartUtc = record.StartUtc,
            EndUtc = record.EndUtc,
            DurationSeconds = record.DurationSeconds,
            IsSystemManaged = record.IsSystemManaged,
            CreatedAtUtc = record.CreatedAtUtc,
            UpdatedAtUtc = record.UpdatedAtUtc
        };
    }

    private static void RestoreProjectObjectRecord(ProjectObjectRecord current, ProjectObjectRecord snapshot)
    {
        current.ProjectId = snapshot.ProjectId;
        current.NodeKey = snapshot.NodeKey;
        current.ObjectType = snapshot.ObjectType;
        current.Title = snapshot.Title;
        current.Subtitle = snapshot.Subtitle;
        current.Status = snapshot.Status;
        current.Notes = snapshot.Notes;
        current.Route = snapshot.Route;
        current.ExternalArtifactKind = snapshot.ExternalArtifactKind;
        current.ExternalArtifactId = snapshot.ExternalArtifactId;
        current.ObjectSubtype = snapshot.ObjectSubtype;
        current.MediaRelativePath = snapshot.MediaRelativePath;
        current.MediaContentType = snapshot.MediaContentType;
        current.MediaOriginalFileName = snapshot.MediaOriginalFileName;
        current.StorageObjectReferenceJson = snapshot.StorageObjectReferenceJson;
        current.ProgressMode = snapshot.ProgressMode;
        current.ProgressPercent = snapshot.ProgressPercent;
        current.MarkerIcon = snapshot.MarkerIcon;
        current.MarkerTone = snapshot.MarkerTone;
        current.MarkerLabel = snapshot.MarkerLabel;
        current.Priority = snapshot.Priority;
        current.MetadataJson = snapshot.MetadataJson;
        current.ParentNodeKey = snapshot.ParentNodeKey;
        current.PositionX = snapshot.PositionX;
        current.PositionY = snapshot.PositionY;
        current.StartUtc = snapshot.StartUtc;
        current.EndUtc = snapshot.EndUtc;
        current.DurationSeconds = snapshot.DurationSeconds;
        current.IsSystemManaged = snapshot.IsSystemManaged;
        current.CreatedAtUtc = snapshot.CreatedAtUtc;
        current.UpdatedAtUtc = snapshot.UpdatedAtUtc;
    }

    private static ProjectObjectLinkRecord CloneProjectObjectLinkRecord(ProjectObjectLinkRecord link)
    {
        return new ProjectObjectLinkRecord
        {
            Id = link.Id,
            ProjectId = link.ProjectId,
            SourceNodeKey = link.SourceNodeKey,
            TargetNodeKey = link.TargetNodeKey,
            LinkKind = link.LinkKind,
            IsSystemManaged = link.IsSystemManaged,
            CreatedAtUtc = link.CreatedAtUtc
        };
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

        var expectedKind = ResolveHierarchyLinkKind(targetNode.ProjectId, targetNode.ParentNodeKey!);
        return string.Equals(link.SourceNodeKey, targetNode.ParentNodeKey, StringComparison.Ordinal) &&
               link.LinkKind == expectedKind;
    }

    private static string BuildProjectRootNodeKey(Guid projectId)
    {
        return $"project:{projectId}";
    }

    private static ProjectObjectLinkKind ResolveHierarchyLinkKind(Guid projectId, string parentNodeKey)
    {
        return string.Equals(parentNodeKey, BuildProjectRootNodeKey(projectId), StringComparison.Ordinal)
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

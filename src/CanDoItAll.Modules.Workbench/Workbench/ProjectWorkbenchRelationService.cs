using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Workbench.CanvasAdapters;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectWorkbenchRelationService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock,
    ProjectStructureAssemblyService projectStructureAssemblyService)
{
    private static readonly ProjectStructureInvariantService InvariantService = new();

    public async Task LinkObjectsAsync(
        Guid projectId,
        string sourceNodeKey,
        string targetNodeKey,
        ProjectObjectLinkKind linkKind,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        var existingNodes = (await projectStructureAssemblyService.LoadAsync(dbContext, projectId, cancellationToken)).Nodes;
        InvariantService.ValidateUserAuthoredLink(
            projectId,
            sourceNodeKey,
            targetNodeKey,
            linkKind,
            existingNodes,
            IsProcessProjectionNodeKey);
        await UpsertUserAuthoredLinkAsync(
            dbContext,
            projectId,
            sourceNodeKey,
            targetNodeKey,
            linkKind,
            clock.GetUtcNow(),
            cancellationToken);
        await ClearProjectionVisibilityOverrideAsync(dbContext, projectId, sourceNodeKey, cancellationToken);
        await ClearProjectionVisibilityOverrideAsync(dbContext, projectId, targetNodeKey, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static Guid? TryResolveProcessDefinitionId(string nodeKey)
    {
        return nodeKey.StartsWith("process-definition:", StringComparison.Ordinal) &&
               Guid.TryParse(nodeKey["process-definition:".Length..], out var definitionId)
            ? definitionId
            : null;
    }

    private static Guid? TryResolveProcessRunId(string nodeKey)
    {
        return nodeKey.StartsWith("process-run:", StringComparison.Ordinal) &&
               Guid.TryParse(nodeKey["process-run:".Length..], out var runId)
            ? runId
            : null;
    }

    private static bool IsProjectionLayoutResetCandidate(string nodeKey)
    {
        return IsProcessProjectionNodeKey(nodeKey);
    }

    private static bool IsProcessProjectionNodeKey(string nodeKey)
    {
        return TryResolveProcessDefinitionId(nodeKey).HasValue ||
               TryResolveProcessRunId(nodeKey).HasValue;
    }

    private static string BuildProcessDefinitionNodeKey(Guid definitionId)
    {
        return $"process-definition:{definitionId:D}";
    }

    private static string BuildProcessRunNodeKey(Guid runId)
    {
        return $"process-run:{runId:D}";
    }

    public async Task<bool> UnlinkObjectsAsync(
        Guid projectId,
        string sourceNodeKey,
        string targetNodeKey,
        ProjectObjectLinkKind linkKind,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);

        var link = await dbContext.Set<ProjectObjectLinkRecord>()
            .FirstOrDefaultAsync(item =>
                item.ProjectId == projectId &&
                item.SourceNodeKey == sourceNodeKey &&
                item.TargetNodeKey == targetNodeKey &&
                item.LinkKind == linkKind &&
                !item.IsSystemManaged,
                cancellationToken);
        if (link is null)
        {
            return false;
        }

        dbContext.Remove(link);
        await ResetProjectionLayoutsAsync(
            dbContext,
            projectId,
            sourceNodeKey,
            targetNodeKey,
            cancellationToken);
        await UpdateProjectionVisibilityAfterUnlinkAsync(dbContext, projectId, sourceNodeKey, cancellationToken);
        await UpdateProjectionVisibilityAfterUnlinkAsync(dbContext, projectId, targetNodeKey, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static async Task ResetProjectionLayoutsAsync(
        AppDbContext dbContext,
        Guid projectId,
        string sourceNodeKey,
        string targetNodeKey,
        CancellationToken cancellationToken)
    {
        var projectedNodeKeys = new[] { sourceNodeKey, targetNodeKey }
            .Where(IsProjectionLayoutResetCandidate)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (projectedNodeKeys.Length == 0)
        {
            return;
        }

        var layouts = await dbContext.Set<ProjectStructureProjectionLayoutRecord>()
            .Where(item => item.ProjectId == projectId && projectedNodeKeys.Contains(item.NodeKey))
            .ToListAsync(cancellationToken);
        if (layouts.Count > 0)
        {
            dbContext.RemoveRange(layouts);
        }
    }

    private async Task ClearProjectionVisibilityOverrideAsync(
        AppDbContext dbContext,
        Guid projectId,
        string nodeKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(nodeKey) ||
            await HasCanonicalNodeAsync(dbContext, projectId, nodeKey, cancellationToken))
        {
            return;
        }

        var layout = await dbContext.Set<ProjectStructureProjectionLayoutRecord>()
            .SingleOrDefaultAsync(
                item => item.ProjectId == projectId && item.NodeKey == nodeKey,
                cancellationToken);
        if (layout is null || !layout.IsHidden)
        {
            return;
        }

        layout.IsHidden = false;
        layout.UpdatedAtUtc = clock.GetUtcNow();
    }

    private async Task UpdateProjectionVisibilityAfterUnlinkAsync(
        AppDbContext dbContext,
        Guid projectId,
        string nodeKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(nodeKey) ||
            await HasCanonicalNodeAsync(dbContext, projectId, nodeKey, cancellationToken))
        {
            return;
        }

        var layout = await dbContext.Set<ProjectStructureProjectionLayoutRecord>()
            .SingleOrDefaultAsync(
                item => item.ProjectId == projectId && item.NodeKey == nodeKey,
                cancellationToken);
        var deletedIncomingLinkIds = dbContext.ChangeTracker
            .Entries<ProjectObjectLinkRecord>()
            .Where(entry =>
                entry.State == EntityState.Deleted &&
                entry.Entity.ProjectId == projectId &&
                string.Equals(entry.Entity.TargetNodeKey, nodeKey, StringComparison.Ordinal) &&
                !entry.Entity.IsSystemManaged)
            .Select(entry => entry.Entity.Id)
            .ToHashSet();
        var hasPendingIncomingLink = dbContext.ChangeTracker
            .Entries<ProjectObjectLinkRecord>()
            .Any(entry =>
                entry.State == EntityState.Added &&
                entry.Entity.ProjectId == projectId &&
                string.Equals(entry.Entity.TargetNodeKey, nodeKey, StringComparison.Ordinal) &&
                !entry.Entity.IsSystemManaged);
        var hasRemainingIncomingLinks = await dbContext.Set<ProjectObjectLinkRecord>()
            .AnyAsync(
                item =>
                    item.ProjectId == projectId &&
                    item.TargetNodeKey == nodeKey &&
                    !item.IsSystemManaged &&
                    !deletedIncomingLinkIds.Contains(item.Id),
                cancellationToken);
        if (hasPendingIncomingLink || hasRemainingIncomingLinks)
        {
            if (layout is not null && layout.IsHidden)
            {
                layout.IsHidden = false;
                layout.UpdatedAtUtc = clock.GetUtcNow();
            }

            return;
        }

        if (layout is null)
        {
            await dbContext.Set<ProjectStructureProjectionLayoutRecord>().AddAsync(
                new ProjectStructureProjectionLayoutRecord
                {
                    ProjectId = projectId,
                    NodeKey = nodeKey,
                    PositionX = 0,
                    PositionY = 0,
                    IsHidden = true,
                    UpdatedAtUtc = clock.GetUtcNow()
                },
                cancellationToken);
            return;
        }

        layout.IsHidden = true;
        layout.UpdatedAtUtc = clock.GetUtcNow();
    }

    private static async Task<bool> HasCanonicalNodeAsync(
        AppDbContext dbContext,
        Guid projectId,
        string nodeKey,
        CancellationToken cancellationToken)
    {
        return await dbContext.Set<ProjectObjectRecord>()
            .AnyAsync(
                item =>
                    item.ProjectId == projectId &&
                    item.NodeKey == nodeKey &&
                    !item.IsSystemManaged,
                cancellationToken);
    }

    public async Task<ProjectStructureNode?> ReparentObjectAsync(
        Guid projectId,
        string nodeKey,
        string? parentNodeKey,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        var existingNodes = (await projectStructureAssemblyService.LoadAsync(dbContext, projectId, cancellationToken)).Nodes;
        var node = await dbContext.Set<ProjectObjectRecord>()
            .FirstOrDefaultAsync(item => item.ProjectId == projectId && item.NodeKey == nodeKey && !item.IsSystemManaged, cancellationToken);
        if (node is null)
        {
            return null;
        }

        await ProjectNodeBindingStorage.LoadAsync(dbContext, [node], cancellationToken);

        var normalizedParentNodeKey = ProjectWorkbenchGraphConventions.NormalizeEditableParentNodeKey(projectId, parentNodeKey);
        if (string.Equals(node.ParentNodeKey, normalizedParentNodeKey, StringComparison.Ordinal))
        {
            return ProjectWorkbenchNodeMapper.MapStructureNode(node);
        }

        InvariantService.ValidateParentAssignment(projectId, node.NodeKey, normalizedParentNodeKey, existingNodes);

        var parentLinks = await dbContext.Set<ProjectObjectLinkRecord>()
            .Where(item => item.ProjectId == projectId &&
                item.TargetNodeKey == node.NodeKey &&
                !item.IsSystemManaged &&
                (item.LinkKind == ProjectObjectLinkKind.BelongsTo || item.LinkKind == ProjectObjectLinkKind.Contains))
            .ToListAsync(cancellationToken);
        if (parentLinks.Count > 0)
        {
            dbContext.RemoveRange(parentLinks);
        }

        node.ParentNodeKey = normalizedParentNodeKey;
        node.UpdatedAtUtc = clock.GetUtcNow();

        await dbContext.SaveChangesAsync(cancellationToken);
        return ProjectWorkbenchNodeMapper.MapStructureNode(node);
    }

    public Task MoveObjectAsync(
        Guid projectId,
        string nodeKey,
        double x,
        double y,
        CancellationToken cancellationToken = default)
    {
        return MoveObjectsAsync(
            projectId,
            [new ProjectNodeMoveRequest(nodeKey, x, y)],
            cancellationToken);
    }

    public async Task<IReadOnlyList<string>> MoveObjectsAsync(
        Guid projectId,
        IReadOnlyCollection<ProjectNodeMoveRequest> positions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(positions);
        if (positions.Count == 0)
        {
            return [];
        }

        var requestedPositions = positions
            .Where(position => !string.IsNullOrWhiteSpace(position.NodeId))
            .GroupBy(position => position.NodeId, StringComparer.Ordinal)
            .Select(group => group.Last())
            .ToList();
        if (requestedPositions.Count == 0)
        {
            return [];
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        return await projectStructureAssemblyService.UpdatePositionsAsync(dbContext, projectId, requestedPositions, cancellationToken);
    }

    public async Task<ProjectStructureSubtreeRecompositionResult?> RecomposeSubtreeAsync(
        Guid projectId,
        string rootNodeKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rootNodeKey))
        {
            return null;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        var assembly = await projectStructureAssemblyService.LoadAsync(dbContext, projectId, cancellationToken);
        var plan = ProjectStructureSubtreeRecompositionEngine.Recompose(
            ProjectWorkbenchNodeMapper.MapStructureNodes(assembly.Nodes, assembly.Links),
            rootNodeKey);
        if (plan is null)
        {
            return null;
        }

        if (plan.DescendantCount == 0)
        {
            return new ProjectStructureSubtreeRecompositionResult(rootNodeKey, 0, 0);
        }

        var repositionedNodeIds = await projectStructureAssemblyService.UpdatePositionsAsync(
            dbContext,
            projectId,
            plan.Positions.Select(position => new ProjectNodeMoveRequest(position.NodeId, position.X, position.Y)).ToList(),
            cancellationToken);
        return new ProjectStructureSubtreeRecompositionResult(rootNodeKey, plan.DescendantCount, repositionedNodeIds.Count);
    }

    private static async Task UpsertUserAuthoredLinkAsync(
        AppDbContext dbContext,
        Guid projectId,
        string sourceNodeKey,
        string targetNodeKey,
        ProjectObjectLinkKind linkKind,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken)
    {
        var existingLink = await dbContext.Set<ProjectObjectLinkRecord>()
            .FirstOrDefaultAsync(item =>
                item.ProjectId == projectId &&
                item.SourceNodeKey == sourceNodeKey &&
                item.TargetNodeKey == targetNodeKey &&
                item.LinkKind == linkKind,
                cancellationToken);
        if (existingLink is not null)
        {
            existingLink.IsSystemManaged = false;
            return;
        }

        await dbContext.Set<ProjectObjectLinkRecord>().AddAsync(new ProjectObjectLinkRecord
        {
            ProjectId = projectId,
            SourceNodeKey = sourceNodeKey,
            TargetNodeKey = targetNodeKey,
            LinkKind = linkKind,
            IsSystemManaged = false,
            CreatedAtUtc = createdAtUtc
        }, cancellationToken);
    }
}

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
        InvariantService.ValidateUserAuthoredLink(projectId, sourceNodeKey, targetNodeKey, linkKind, existingNodes);
        await ProjectWorkbenchGraphConventions.UpsertLinkAsync(
            dbContext,
            projectId,
            sourceNodeKey,
            targetNodeKey,
            linkKind,
            isSystemManaged: false,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
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
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
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

        await ProjectNodeBindingStorage.NormalizeAndHydrateAsync(dbContext, [node], cancellationToken);

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
}

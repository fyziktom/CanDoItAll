using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectNodeScopeBridge(IDbContextFactory<AppDbContext> dbContextFactory) : IProjectNodeScopeBridge
{
    public async Task<ProjectNodeScopeResolution> ResolveAsync(
        Guid projectId,
        string nodeKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedNodeKey = nodeKey?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedNodeKey))
        {
            return new ProjectNodeScopeResolution(false, false, null, string.Empty);
        }

        if (string.Equals(normalizedNodeKey, $"project:{projectId}", StringComparison.Ordinal))
        {
            return new ProjectNodeScopeResolution(true, false, ProjectObjectType.ProjectRoot, string.Empty);
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);

        var node = await dbContext.Set<ProjectObjectRecord>()
            .Where(item => item.NodeKey == normalizedNodeKey)
            .Select(item => new
            {
                item.ProjectId,
                item.ObjectType,
                item.ObjectSubtype
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (node is null)
        {
            return new ProjectNodeScopeResolution(false, false, null, string.Empty);
        }

        return new ProjectNodeScopeResolution(
            node.ProjectId == projectId,
            node.ProjectId != projectId,
            node.ObjectType,
            node.ObjectSubtype);
    }
}

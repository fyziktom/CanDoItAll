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

        if (TryParseProjectRootNodeKey(normalizedNodeKey, out var rootProjectId))
        {
            return new ProjectNodeScopeResolution(
                rootProjectId == projectId,
                rootProjectId != projectId,
                ProjectObjectType.ProjectRoot,
                string.Empty);
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);

        var projectNode = await dbContext.Set<ProjectObjectRecord>()
            .Where(item => item.ProjectId == projectId && item.NodeKey == normalizedNodeKey)
            .Select(item => new
            {
                item.ProjectId,
                item.ObjectType,
                item.ObjectSubtype
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (projectNode is not null)
        {
            return new ProjectNodeScopeResolution(true, false, projectNode.ObjectType, projectNode.ObjectSubtype);
        }

        var foreignNode = await dbContext.Set<ProjectObjectRecord>()
            .Where(item => item.NodeKey == normalizedNodeKey)
            .Select(item => new
            {
                item.ProjectId,
                item.ObjectType,
                item.ObjectSubtype
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (foreignNode is null)
        {
            return new ProjectNodeScopeResolution(false, false, null, string.Empty);
        }

        return new ProjectNodeScopeResolution(
            false,
            true,
            foreignNode.ObjectType,
            foreignNode.ObjectSubtype);
    }

    private static bool TryParseProjectRootNodeKey(string nodeKey, out Guid projectId)
    {
        const string prefix = "project:";
        if (nodeKey.StartsWith(prefix, StringComparison.Ordinal) &&
            Guid.TryParse(nodeKey[prefix.Length..], out projectId))
        {
            return true;
        }

        projectId = Guid.Empty;
        return false;
    }
}

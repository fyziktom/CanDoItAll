using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectNodeDetailsBridge(
    IDbContextFactory<AppDbContext> dbContextFactory) : IProjectNodeDetailsBridge
{
    public async Task<ProjectNodeDetails?> GetAsync(
        Guid projectId,
        ProjectNodeReference nodeReference,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);

        return await dbContext.Set<ProjectObjectRecord>()
            .AsNoTracking()
            .Where(item => item.ProjectId == projectId && item.NodeKey == nodeReference.NodeKey)
            .Select(item => new ProjectNodeDetails(
                item.ProjectId,
                item.NodeKey,
                item.ObjectType,
                item.ObjectSubtype,
                item.Title,
                item.Subtitle,
                item.Status,
                item.ProgressMode,
                item.ProgressPercent,
                item.StartUtc,
                item.EndUtc,
                item.ParentNodeKey ?? string.Empty))
            .SingleOrDefaultAsync(cancellationToken);
    }
}

using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.Modules.Workbench;

public static class ProjectStructureAgentSchemaInitializer
{
    public static Task EnsureAsync(WorkbenchDbContext dbContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        return Task.CompletedTask;
    }
}

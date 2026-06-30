using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.Modules.Workbench;

public static class ProjectStructureAgentSchemaInitializer
{
    public static Task EnsureAsync(AppDbContext dbContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        return Task.CompletedTask;
    }
}

using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.Modules.Projects;

public static class ProjectsSchemaInitializer
{
    public static Task EnsureAsync(AppDbContext dbContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        return Task.CompletedTask;
    }
}

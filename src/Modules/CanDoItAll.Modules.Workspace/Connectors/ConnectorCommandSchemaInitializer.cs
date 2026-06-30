using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.Modules.Workspace;

public static class ConnectorCommandSchemaInitializer
{
    public static Task EnsureAsync(AppDbContext dbContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        return Task.CompletedTask;
    }
}

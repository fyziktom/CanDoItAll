using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.Modules.Factory;

public static class PromptFactorySchemaInitializer
{
    public static Task EnsureAsync(AppDbContext dbContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        return Task.CompletedTask;
    }
}

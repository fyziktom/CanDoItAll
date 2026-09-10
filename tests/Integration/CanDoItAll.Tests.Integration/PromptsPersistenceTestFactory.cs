using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Prompts;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Integration;

internal sealed class PromptsPersistenceTestFactory(DbContextOptions<PromptsDbContext> options)
    : IDbContextFactory<PromptsDbContext> {
    public PromptsDbContext CreateDbContext() => new(options);
    public Task<PromptsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(CreateDbContext());

    public static PromptsPersistenceTestFactory FromCanonical(IDbContextFactory<AppDbContext> factory) {
        using var canonical = factory.CreateDbContext();
        if (!canonical.Database.IsNpgsql()) {
            throw new InvalidOperationException("This fixture requires PostgreSQL.");
        }
        return new(new DbContextOptionsBuilder<PromptsDbContext>().UseNpgsql(canonical.Database.GetConnectionString()
            ?? throw new InvalidOperationException("The canonical fixture has no connection string.")).Options);
    }
}

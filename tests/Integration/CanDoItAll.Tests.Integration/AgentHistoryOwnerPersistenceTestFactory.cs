using CanDoItAll.AgentFramework.ProviderHistory.Persistence;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CanDoItAll.Tests.Integration;

internal static class AgentHistoryOwnerPersistenceTestFactory {
    public static IDbContextFactory<AgentHistoryDbContext> Locators(IDbContextFactory<AppDbContext> canonical)
        => new Factory<AgentHistoryDbContext>(Options<AgentHistoryDbContext>(canonical), static options => new(options));

    public static ProjectIdentityQueryService Projects(IDbContextFactory<AppDbContext> canonical, CoordinatedDatabaseTransaction transactions) {
        var options = Options<ProjectsDbContext>(canonical);
        return new(new Factory<ProjectsDbContext>(options, static value => new(value)), options, transactions);
    }

    public static AgentHistoryPublicationStore Publications(IDbContextFactory<AppDbContext> canonical,
        HistoryPartitionStore partitions, HistoryProjectionWriter projection, CoordinatedDatabaseTransaction transactions)
        => new(Locators(canonical), Projects(canonical, transactions), partitions, projection, transactions);

    public static DbContextOptions<TContext> Options<TContext>(IDbContextFactory<AppDbContext> canonical) where TContext : DbContext {
        using var db = canonical.CreateDbContext();
        return new(db.GetService<IDbContextOptions>().Extensions.ToDictionary(extension => extension.GetType()));
    }

    private sealed class Factory<TContext>(DbContextOptions<TContext> options, Func<DbContextOptions<TContext>, TContext> create)
        : IDbContextFactory<TContext> where TContext : DbContext {
        public TContext CreateDbContext() => create(options);

        public Task<TContext> CreateDbContextAsync(CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreateDbContext());
        }
    }
}

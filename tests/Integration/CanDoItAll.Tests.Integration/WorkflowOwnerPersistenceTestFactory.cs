using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CanDoItAll.Tests.Integration;

internal static class WorkflowOwnerPersistenceTestFactory {
    public static IDbContextFactory<WorkflowDbContext> FromCanonical(IDbContextFactory<AppDbContext> canonicalFactory) {
        using var canonical = canonicalFactory.CreateDbContext();
        var extensions = canonical.GetService<IDbContextOptions>().Extensions.ToDictionary(extension => extension.GetType());
        return new Factory(new DbContextOptions<WorkflowDbContext>(extensions));
    }

    private sealed class Factory(DbContextOptions<WorkflowDbContext> options) : IDbContextFactory<WorkflowDbContext> {
        public WorkflowDbContext CreateDbContext() => new(options);

        public Task<WorkflowDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreateDbContext());
        }
    }
}

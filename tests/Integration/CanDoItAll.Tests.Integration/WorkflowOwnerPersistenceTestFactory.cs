using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Prompts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CanDoItAll.Tests.Integration;

internal static class WorkflowOwnerPersistenceTestFactory {
    public static PersistentWorkflowCatalogService CreateCatalog(WorkflowChanges changes,
        IDbContextFactory<AppDbContext> canonicalFactory, IWorkflowDefinitionValidator validator, PromptsService gallery,
        Func<IPromptGalleryMutationService, IPromptGalleryMutationService>? decorate = null) {
        using var canonical = canonicalFactory.CreateDbContext();
        var connection = canonical.Database.GetConnectionString()
            ?? throw new InvalidOperationException("The workflow fixture has no PostgreSQL connection.");
        var profile = new ResolvedDatabaseProfile(new DatabaseProfileRecord { ProviderKind = DatabaseProviderKind.PostgreSql },
            DatabaseProfileResolutionSource.ExplicitOverride, connection);
        var transactions = CoordinatedDatabaseTransaction.ForProfile(profile);
        var options = new DbContextOptionsBuilder<PromptsDbContext>().UseNpgsql(connection).Options;
        var promptFactory = new PromptsPersistenceTestFactory(options);
        var mutations = new PromptGalleryMutationService(gallery, promptFactory, options, transactions);
        return new(changes, FromCanonical(canonicalFactory), validator, gallery, decorate is null ? mutations : decorate(mutations), transactions);
    }

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

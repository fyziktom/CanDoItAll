using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

internal static class PromptGalleryTestSupport
{
    public static (PromptGalleryMutationService Mutations, CoordinatedDatabaseTransaction Transactions) CreateMutations(
        IDbContextFactory<PromptsDbContext> factory, PromptsService service, string databaseName) {
        using var context = factory.CreateDbContext();
        var options = new DbContextOptions<PromptsDbContext>(context.GetService<IDbContextOptions>().Extensions.ToDictionary(item => item.GetType()));
        var transactions = CoordinatedDatabaseTransaction.ForProfile(new ResolvedDatabaseProfile(
            new DatabaseProfileRecord { ProviderKind = DatabaseProviderKind.InMemory }, DatabaseProfileResolutionSource.ExplicitOverride, databaseName));
        return (new(service, factory, options, transactions), transactions);
    }

    public static TestDbContextFactory CreateFactory(string testName)
    {
        var options = new DbContextOptionsBuilder<PromptsDbContext>()
            .UseInMemoryDatabase($"prompt-gallery-{testName}-{Guid.NewGuid():N}")
            .Options;
        return new TestDbContextFactory(options);
    }

    public static PromptGalleryProjectionCoordinator CreateDisabledProjectionCoordinator(
        IDbContextFactory<PromptsDbContext> factory)
        => new(factory, new DisabledPromptGalleryProjectionDriver());

    public static PromptsService CreateService(IDbContextFactory<PromptsDbContext> factory, PromptGalleryChanges? changes = null)
        => new(
            factory,
            new FixedClock(),
            new NullActivityStream(),
            new EfPromptGallerySearchDriver(factory),
            CreateDisabledProjectionCoordinator(factory),
            new PromptGalleryCompatibilityEvaluator(),
            NullLogger<PromptsService>.Instance,
            changes ?? PromptGalleryObservationTestSupport.Create());

    internal sealed class TestDbContextFactory(DbContextOptions<PromptsDbContext> options)
        : IDbContextFactory<PromptsDbContext>
    {
        public PromptsDbContext CreateDbContext() => new(options);

        public Task<PromptsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }

    internal sealed class FixedClock(DateTimeOffset? now = null) : IClock
    {
        public DateTimeOffset GetUtcNow() => now ?? DateTimeOffset.UnixEpoch;
    }
}

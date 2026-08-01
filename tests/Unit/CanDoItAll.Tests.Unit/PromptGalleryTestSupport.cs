using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

internal static class PromptGalleryTestSupport
{
    public static TestDbContextFactory CreateFactory(string testName)
    {
        AppDbContextModelRegistry.ConfigureAssemblies([typeof(PromptsModuleAssemblyMarker).Assembly]);
        var options = AppDbContextTestOptionsBuilder.Create()
            .UseInMemoryDatabase($"prompt-gallery-{testName}-{Guid.NewGuid():N}")
            .Options;
        return new TestDbContextFactory(options);
    }

    public static PromptGalleryProjectionCoordinator CreateDisabledProjectionCoordinator(
        IDbContextFactory<AppDbContext> factory)
        => new(factory, new DisabledPromptGalleryProjectionDriver());

    public static PromptsService CreateService(IDbContextFactory<AppDbContext> factory)
        => new(
            factory,
            new FixedClock(),
            new NullActivityStream(),
            new EfPromptGallerySearchDriver(factory),
            CreateDisabledProjectionCoordinator(factory),
            new PromptGalleryCompatibilityEvaluator(),
            NullLogger<PromptsService>.Instance);

    internal sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options)
        : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }

    internal sealed class FixedClock(DateTimeOffset? now = null) : IClock
    {
        public DateTimeOffset GetUtcNow() => now ?? DateTimeOffset.UnixEpoch;
    }
}

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
        var options = new DbContextOptionsBuilder<PromptsDbContext>()
            .UseInMemoryDatabase($"prompt-gallery-{testName}-{Guid.NewGuid():N}")
            .Options;
        return new TestDbContextFactory(options);
    }

    public static PromptGalleryProjectionCoordinator CreateDisabledProjectionCoordinator(
        IDbContextFactory<PromptsDbContext> factory)
        => new(factory, new DisabledPromptGalleryProjectionDriver());

    public static PromptsService CreateService(IDbContextFactory<PromptsDbContext> factory)
        => new(
            factory,
            new FixedClock(),
            new NullActivityStream(),
            new EfPromptGallerySearchDriver(factory),
            CreateDisabledProjectionCoordinator(factory),
            new PromptGalleryCompatibilityEvaluator(),
            NullLogger<PromptsService>.Instance);

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

using CanDoItAll.Modules.Prompts;

namespace CanDoItAll.Tests.Unit;

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
public sealed class PromptGalleryProjectionTests
{
    [Fact]
    public async Task Disabled_driver_reports_unavailable_without_touching_canonical_storage()
    {
        var coordinator = new PromptGalleryProjectionCoordinator(
            new ThrowingDbContextFactory(),
            new DisabledPromptGalleryProjectionDriver());

        var status = await coordinator.GetStatusAsync();
        var upsert = await coordinator.UpsertAsync(Guid.NewGuid());
        var rebuild = await coordinator.RebuildAsync();

        Assert.False(status.Enabled);
        Assert.Equal(PromptGalleryProjectionHealth.Disabled, status.Health);
        Assert.Equal(PromptGalleryProjectionOperationState.Disabled, upsert.State);
        Assert.Equal(PromptGalleryProjectionOperationState.Disabled, rebuild.State);
        Assert.Equal(0, rebuild.ProcessedCount);
    }

    [Fact]
    public async Task Coordinator_upserts_removes_and_rebuilds_from_canonical_items()
    {
        var factory = PromptGalleryTestSupport.CreateFactory(nameof(Coordinator_upserts_removes_and_rebuilds_from_canonical_items));
        var activeId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var archivedId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        await using (var dbContext = factory.CreateDbContext())
        {
            dbContext.AddRange(
                Artifact(activeId, "Active", archived: false),
                Artifact(archivedId, "Archived", archived: true));
            await dbContext.SaveChangesAsync();
        }

        var driver = new RecordingProjectionDriver();
        var coordinator = new PromptGalleryProjectionCoordinator(factory, driver);

        var upsert = await coordinator.UpsertAsync(activeId);
        var archiveProjection = await coordinator.UpsertAsync(archivedId);
        var explicitRemove = await coordinator.RemoveAsync(activeId);
        var rebuild = await coordinator.RebuildAsync();

        Assert.Equal(PromptGalleryProjectionOperationState.Applied, upsert.State);
        Assert.Equal([activeId], driver.Upserts.Select(document => document.PromptArtifactId));
        Assert.Equal([archivedId, activeId], driver.Removals);
        Assert.Equal(PromptGalleryProjectionOperationState.Applied, explicitRemove.State);
        Assert.Equal(1, rebuild.ProcessedCount);
        Assert.Equal([activeId], Assert.Single(driver.Rebuilds).Select(document => document.PromptArtifactId));
    }

    private static PromptArtifact Artifact(Guid id, string title, bool archived)
        => new()
        {
            Id = id,
            Title = title,
            Summary = $"{title} summary",
            Kind = PromptGalleryItemKind.FullPrompt,
            Status = PromptArtifactStatus.Final,
            CurrentDraftText = $"{title} content",
            CurrentVersionNumber = 1,
            IsArchived = archived,
            CreatedAtUtc = DateTimeOffset.UnixEpoch,
            UpdatedAtUtc = DateTimeOffset.UnixEpoch
        };

    private sealed class RecordingProjectionDriver : IPromptGalleryProjectionDriver
    {
        private static readonly PromptGalleryProjectionStatus Ready = new(
            "Recording",
            Enabled: true,
            PromptGalleryProjectionHealth.Ready,
            "Ready");

        public List<PromptGalleryProjectionDocument> Upserts { get; } = [];

        public List<Guid> Removals { get; } = [];

        public List<IReadOnlyList<PromptGalleryProjectionDocument>> Rebuilds { get; } = [];

        public string Name => Ready.DriverName;

        public bool Enabled => true;

        public Task<PromptGalleryProjectionStatus> GetStatusAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Ready);

        public Task UpsertAsync(
            PromptGalleryProjectionDocument document,
            CancellationToken cancellationToken = default)
        {
            Upserts.Add(document);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(Guid promptArtifactId, CancellationToken cancellationToken = default)
        {
            Removals.Add(promptArtifactId);
            return Task.CompletedTask;
        }

        public async Task<int> RebuildAsync(
            IAsyncEnumerable<PromptGalleryProjectionDocument> documents,
            CancellationToken cancellationToken = default)
        {
            var rebuilt = new List<PromptGalleryProjectionDocument>();
            await foreach (var document in documents.WithCancellation(cancellationToken))
            {
                rebuilt.Add(document);
            }

            Rebuilds.Add(rebuilt);
            return rebuilt.Count;
        }
    }

    private sealed class ThrowingDbContextFactory : Microsoft.EntityFrameworkCore.IDbContextFactory<CanDoItAll.Infrastructure.Persistence.AppDbContext>
    {
        public CanDoItAll.Infrastructure.Persistence.AppDbContext CreateDbContext()
            => throw new InvalidOperationException("Disabled projection must not access the database.");

        public Task<CanDoItAll.Infrastructure.Persistence.AppDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Disabled projection must not access the database.");
    }
}

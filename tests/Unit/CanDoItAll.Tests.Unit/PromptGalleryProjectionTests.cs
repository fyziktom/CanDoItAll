using CanDoItAll.Modules.Prompts;

namespace CanDoItAll.Tests.Unit.Infrastructure;

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
        var draftId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        await using (var dbContext = factory.CreateDbContext())
        {
            var active = Artifact(activeId, "Active", archived: false);
            active.CurrentDraftText = "Unpublished active draft content";
            var draft = Artifact(draftId, "Draft", archived: false);
            draft.Status = PromptArtifactStatus.Draft;
            dbContext.AddRange(
                active,
                Artifact(archivedId, "Archived", archived: true),
                draft,
                Version(activeId, "Published active content"));
            await dbContext.SaveChangesAsync();
        }

        var driver = new RecordingProjectionDriver();
        var coordinator = new PromptGalleryProjectionCoordinator(factory, driver);

        var upsert = await coordinator.UpsertAsync(activeId);
        var archiveProjection = await coordinator.UpsertAsync(archivedId);
        var draftProjection = await coordinator.UpsertAsync(draftId);
        var explicitRemove = await coordinator.RemoveAsync(activeId);
        var rebuild = await coordinator.RebuildAsync();

        Assert.Equal(PromptGalleryProjectionOperationState.Applied, upsert.State);
        Assert.Equal([activeId], driver.Upserts.Select(document => document.PromptArtifactId));
        Assert.Equal("Published active content", Assert.Single(driver.Upserts).Content);
        Assert.Equal([archivedId, draftId, activeId], driver.Removals.Select(removal => removal.PromptArtifactId));
        Assert.Equal(PromptGalleryProjectionOperationState.Applied, archiveProjection.State);
        Assert.Equal(PromptGalleryProjectionOperationState.Applied, draftProjection.State);
        Assert.Equal(PromptGalleryProjectionOperationState.Applied, explicitRemove.State);
        Assert.Equal(1, rebuild.ProcessedCount);
        var rebuilt = Assert.Single(Assert.Single(driver.Rebuilds));
        Assert.Equal(activeId, rebuilt.PromptArtifactId);
        Assert.Equal("Published active content", rebuilt.Content);
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

    private static PromptVersion Version(Guid promptArtifactId, string content)
        => new()
        {
            PromptArtifactId = promptArtifactId,
            VersionNumber = 1,
            Content = content,
            CreationReason = "Projection proof",
            TitleSnapshot = "Published title",
            SummarySnapshot = "Published summary",
            CreatedAtUtc = DateTimeOffset.UnixEpoch
        };

    private sealed class RecordingProjectionDriver : IPromptGalleryProjectionDriver
    {
        private static readonly PromptGalleryProjectionStatus Ready = new(
            "Recording",
            Enabled: true,
            PromptGalleryProjectionHealth.Ready,
            "Ready");

        public List<PromptGalleryProjectionDocument> Upserts { get; } = [];

        public List<(Guid PromptArtifactId, DateTimeOffset? ExpectedUpdatedAtUtc)> Removals { get; } = [];

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

        public Task RemoveAsync(
            Guid promptArtifactId,
            DateTimeOffset? expectedUpdatedAtUtc,
            CancellationToken cancellationToken = default)
        {
            Removals.Add((promptArtifactId, expectedUpdatedAtUtc));
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

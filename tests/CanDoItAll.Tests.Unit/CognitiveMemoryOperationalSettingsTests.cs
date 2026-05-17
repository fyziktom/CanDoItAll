using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class CognitiveMemoryOperationalSettingsTests
{
    [Fact]
    public async Task AutomationSettingsService_PersistsScheduleAndSourceOptions()
    {
        var fixture = CreateFixture();
        var service = new CognitiveMemoryAutomationSettingsService(fixture.Factory, fixture.Clock);

        var saved = await service.SaveAsync(new CognitiveMemoryAutomationSettingsUpdate(
            CognitiveMemoryAutomationScheduleMode.ScheduledMoments,
            "01:30",
            45,
            ["03:00", "16:15"],
            AutoIngestProjectStructure: true,
            AutoIngestProcessRuntime: false,
            AutoConsolidateAfterIngestion: true,
            UpdatedByActorId: "test:operator"));

        var loaded = await service.GetAsync();

        Assert.Equal(CognitiveMemoryAutomationScheduleMode.ScheduledMoments, saved.ScheduleMode);
        Assert.Equal("01:30", loaded.NightlyLocalTime);
        Assert.Equal(45, loaded.IdleMinutes);
        Assert.Equal(["03:00", "16:15"], loaded.ScheduledLocalTimes);
        Assert.True(loaded.AutoIngestProjectStructure);
        Assert.False(loaded.AutoIngestProcessRuntime);
        Assert.True(loaded.AutoConsolidateAfterIngestion);
        Assert.Equal("test:operator", loaded.UpdatedByActorId);
    }

    [Fact]
    public async Task ExternalSourceIngestionService_CreatesSourceItemEvidenceAndOperationStatus()
    {
        var fixture = CreateFixture();
        var service = new CognitiveMemoryExternalSourceIngestionService(
            fixture.Factory,
            fixture.Clock,
            NullLogger<CognitiveMemoryExternalSourceIngestionService>.Instance);
        var projectId = Guid.NewGuid();

        var result = await service.IngestAsync(new CognitiveMemoryExternalSourceIngestRequest(
            CognitiveMemoryExternalSourceKind.UploadedFile,
            projectId,
            "SaaS launch plan",
            "saas-launch-plan.md",
            "Detailed source content for a SaaS launch plan.",
            "text/markdown",
            47,
            "test:operator"));

        Assert.Equal(CognitiveMemoryExternalSourceIngestionStatus.Succeeded, result.Status);
        Assert.Equal(100, result.ProgressPercent);
        Assert.NotNull(result.SourceManifestId);
        Assert.NotNull(result.SourceItemId);
        Assert.NotNull(result.EvidenceAnchorId);

        await using var dbContext = fixture.Factory.CreateDbContext();
        Assert.Single(await dbContext.Set<CognitiveMemorySourceManifestRecord>().ToListAsync());
        Assert.Single(await dbContext.Set<CognitiveMemorySourceItemRecord>().ToListAsync());
        Assert.Single(await dbContext.Set<CognitiveMemoryEvidenceAnchorRecord>().ToListAsync());
        var operation = await dbContext.Set<CognitiveMemoryExternalSourceIngestionRecord>().SingleAsync();
        Assert.Equal(result.OperationId, operation.Id);
        Assert.Equal(CognitiveMemoryExternalSourceIngestionStatus.Succeeded, operation.Status);
    }

    [Fact]
    public async Task ExternalSourceIngestionService_SplitsMarkdownIntoReviewableSections()
    {
        var fixture = CreateFixture();
        var service = new CognitiveMemoryExternalSourceIngestionService(
            fixture.Factory,
            fixture.Clock,
            NullLogger<CognitiveMemoryExternalSourceIngestionService>.Instance);
        var projectId = Guid.NewGuid();
        var content = """
            # Follow-up source

            Intro text.

            ## FieldOps Mobile App

            Offline route planning, technician sync, photo evidence, and conflict review.

            ## ClinicFlow SaaS Business Plan

            Clinic scheduling, patient reminders, no-show reduction, and pilot risks.
            """;

        var result = await service.IngestAsync(new CognitiveMemoryExternalSourceIngestRequest(
            CognitiveMemoryExternalSourceKind.UploadedFile,
            projectId,
            "sample-projects.md",
            "sample-projects.md",
            content,
            "text/markdown",
            content.Length,
            "test:operator"));

        await using var dbContext = fixture.Factory.CreateDbContext();
        var sourceItems = await dbContext.Set<CognitiveMemorySourceItemRecord>()
            .OrderBy(item => item.Title)
            .ToListAsync();
        var anchors = await dbContext.Set<CognitiveMemoryEvidenceAnchorRecord>().ToListAsync();

        Assert.Equal(CognitiveMemoryExternalSourceIngestionStatus.Succeeded, result.Status);
        Assert.Equal(2, sourceItems.Count);
        Assert.Equal(2, anchors.Count);
        Assert.All(sourceItems, item => Assert.Equal("UploadedFileChunk", item.SourceItemType));
        Assert.Contains(sourceItems, item => item.Title.Contains("FieldOps Mobile App", StringComparison.Ordinal));
        Assert.Contains(sourceItems, item => item.ContentText.Contains("Offline route planning", StringComparison.Ordinal));
        Assert.DoesNotContain(sourceItems.Single(item => item.Title.Contains("FieldOps", StringComparison.Ordinal)).ContentText, "Clinic scheduling", StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExternalSourceIngestionService_SplitsMermaidMindmapIntoReviewableBranches()
    {
        var fixture = CreateFixture();
        var service = new CognitiveMemoryExternalSourceIngestionService(
            fixture.Factory,
            fixture.Clock,
            NullLogger<CognitiveMemoryExternalSourceIngestionService>.Instance);
        var projectId = Guid.NewGuid();
        var content = """
            mindmap
              root((Follow-up sources))
                FieldOps Mobile App
                  Offline work orders
                    Conflict resolution
                    Idempotent sync
                Regional Economy
                  Inflation inputs
                    CPI
                    Wages
                  Business responses
                    Hiring delay
            """;

        var result = await service.IngestAsync(new CognitiveMemoryExternalSourceIngestRequest(
            CognitiveMemoryExternalSourceKind.UploadedFile,
            projectId,
            "sample-projects.mmd",
            "sample-projects.mmd",
            content,
            "text/plain",
            content.Length,
            "test:operator"));

        await using var dbContext = fixture.Factory.CreateDbContext();
        var sourceItems = await dbContext.Set<CognitiveMemorySourceItemRecord>()
            .OrderBy(item => item.Title)
            .ToListAsync();

        Assert.Equal(CognitiveMemoryExternalSourceIngestionStatus.Succeeded, result.Status);
        Assert.Equal(2, sourceItems.Count);
        Assert.All(sourceItems, item => Assert.Equal("UploadedFileChunk", item.SourceItemType));
        Assert.Contains(sourceItems, item => item.Title.Contains("FieldOps Mobile App", StringComparison.Ordinal));
        Assert.Contains(sourceItems, item => item.Title.Contains("Regional Economy", StringComparison.Ordinal));
        Assert.Contains("Idempotent sync", sourceItems.Single(item => item.Title.Contains("FieldOps", StringComparison.Ordinal)).ContentText, StringComparison.Ordinal);
        Assert.DoesNotContain("Inflation inputs", sourceItems.Single(item => item.Title.Contains("FieldOps", StringComparison.Ordinal)).ContentText, StringComparison.Ordinal);
    }

    private static TestFixture CreateFixture()
    {
        AppDbContextModelRegistry.ConfigureAssemblies([typeof(CognitiveMemoryModuleAssemblyMarker).Assembly]);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"cognitive-memory-operational-{Guid.NewGuid():N}")
            .Options;
        return new TestFixture(new TestDbContextFactory(options), new FixedClock());
    }

    private sealed record TestFixture(
        TestDbContextFactory Factory,
        FixedClock Clock);

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
    }

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }
}

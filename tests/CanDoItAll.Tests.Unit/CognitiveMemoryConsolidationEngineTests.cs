using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.SharedKernel;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class CognitiveMemoryConsolidationEngineTests
{
    [Fact]
    public async Task RunAsync_SubmitsReviewRequiredMutationAndReviewItemWithoutCreatingCanonicalMemory()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SeedSourceAsync(fixture, projectId, "ProcessRuntime", "ProcessRun", "Docker deployment process completed.", withEvidence: true);
        var engine = CreateEngine(fixture);

        var result = await engine.RunAsync(Request(projectId, "consolidation-review"));

        await using var dbContext = fixture.Factory.CreateDbContext();
        var candidate = Assert.Single(await dbContext.Set<CognitiveMemoryConsolidationCandidateRecord>().ToListAsync());
        var mutation = Assert.Single(await dbContext.Set<CognitiveMemoryMutationCommandRecord>().ToListAsync());
        var review = Assert.Single(await dbContext.Set<CognitiveMemoryReviewItemRecord>().ToListAsync());
        var cursor = Assert.Single(await dbContext.Set<CognitiveMemoryConsolidationCursorRecord>().ToListAsync());
        var scoreComponents = await dbContext.Set<CognitiveMemoryScoreComponentRecord>().ToListAsync();

        Assert.Equal(CognitiveMemoryRunStatus.Succeeded, result.Status);
        Assert.Equal(1, result.SourceItemsScanned);
        Assert.Equal(1, result.CandidatesCreated);
        Assert.Equal(1, result.MutationCommandsSubmitted);
        Assert.Equal(1, result.ReviewItemsCreated);
        Assert.Equal(CognitiveMemoryConsolidationCandidateStatus.ReviewRequired, candidate.Status);
        Assert.Equal(CognitiveMemoryMutationCommandStatus.ReviewRequired, mutation.Status);
        Assert.Equal(mutation.Id, candidate.MutationCommandId);
        Assert.Equal(review.Id, candidate.ReviewItemId);
        Assert.Equal(CognitiveMemoryReviewSubjectKind.Run, review.SubjectKind);
        Assert.Equal(result.RunId.Value, review.SubjectId);
        Assert.False(string.IsNullOrWhiteSpace(cursor.Cursor));
        Assert.Contains(scoreComponents, component => component.SpaceKind == CognitiveMemoryScoreSpaceKind.ConsolidationCandidate);
        Assert.Equal(0, await dbContext.Set<CognitiveMemoryRecord>().CountAsync());
    }

    [Fact]
    public async Task RunAsync_ReplaysExistingRunForDuplicateIdempotencyKey()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SeedSourceAsync(fixture, projectId, "WorkflowRuntime", "WorkflowRun", "Workflow decision approved a deployment plan.", withEvidence: true);
        var engine = CreateEngine(fixture);
        var request = Request(projectId, "consolidation-idempotent");

        var first = await engine.RunAsync(request);
        var second = await engine.RunAsync(request);

        await using var dbContext = fixture.Factory.CreateDbContext();
        Assert.Equal(first.RunId, second.RunId);
        Assert.Equal(CognitiveMemoryRunStatus.Succeeded, second.Status);
        Assert.Contains(second.Warnings, warning => warning.Contains("Idempotent replay", StringComparison.Ordinal));
        Assert.Equal(1, await dbContext.Set<CognitiveMemoryConsolidationCandidateRecord>().CountAsync());
        Assert.Equal(1, await dbContext.Set<CognitiveMemoryMutationCommandRecord>().CountAsync());
        Assert.Equal(1, await dbContext.Set<CognitiveMemoryReviewItemRecord>().CountAsync());
    }

    [Fact]
    public async Task RunAsync_BlocksAndDoesNotAdvanceCursorWhenGeneratedCandidateHasNoEvidence()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SeedSourceAsync(fixture, projectId, "ProcessRuntime", "ProcessRun", "Process run lacks evidence anchor.", withEvidence: false);
        var engine = CreateEngine(fixture);

        var result = await engine.RunAsync(Request(projectId, "consolidation-no-evidence"));

        await using var dbContext = fixture.Factory.CreateDbContext();
        var candidate = Assert.Single(await dbContext.Set<CognitiveMemoryConsolidationCandidateRecord>().ToListAsync());
        var mutation = Assert.Single(await dbContext.Set<CognitiveMemoryMutationCommandRecord>().ToListAsync());

        Assert.Equal(CognitiveMemoryRunStatus.Blocked, result.Status);
        Assert.Equal(CognitiveMemoryConsolidationCandidateStatus.Rejected, candidate.Status);
        Assert.Equal(CognitiveMemoryMutationCommandStatus.Rejected, mutation.Status);
        Assert.Equal(0, await dbContext.Set<CognitiveMemoryConsolidationCursorRecord>().CountAsync());
        Assert.Equal(0, await dbContext.Set<CognitiveMemoryReviewItemRecord>().CountAsync());
        Assert.Contains(result.Warnings, warning => warning.Contains("rejected", StringComparison.OrdinalIgnoreCase));
    }

    private static ICognitiveMemoryConsolidationEngine CreateEngine(ConsolidationFixture fixture)
    {
        var registry = new CognitiveMemoryScoreSpaceRegistry();
        var driver = new CognitiveMemoryScoreGeometryDriver(registry);
        return new CognitiveMemoryConsolidationEngine(
            fixture.Factory,
            new CognitiveMemoryMutationAuthority(fixture.Factory, fixture.Clock),
            driver,
            fixture.Clock,
            NullLogger<CognitiveMemoryConsolidationEngine>.Instance);
    }

    private static CognitiveMemoryConsolidationRunRequest Request(Guid projectId, string idempotencyKey)
        => new(
            projectId,
            CognitiveMemoryConsolidationMode.IncrementalRecent,
            CognitiveMemoryConsolidationTriggerKind.Manual,
            CognitiveMemoryConsolidationProfile.IncrementalRecent,
            Policy(projectId),
            new CognitiveMemoryIdempotencyKey(idempotencyKey),
            new CognitiveMemoryConsolidationBudget(10, 10, 10, 4096, TimeSpan.FromMinutes(5)));

    private static CognitiveMemoryPolicyContext Policy(Guid projectId)
        => new(
            projectId,
            "agent:test",
            CognitiveMemoryAccessLevel.Project,
            new CognitiveMemoryPolicyProfileId("policy:test"),
            CognitiveMemoryRiskLevel.Low,
            AllowRestrictedContent: false);

    private static async Task SeedSourceAsync(
        ConsolidationFixture fixture,
        Guid projectId,
        string sourceSystem,
        string sourceItemType,
        string content,
        bool withEvidence)
    {
        var manifest = new CognitiveMemorySourceManifestRecord
        {
            ProjectId = projectId,
            SourceSystem = sourceSystem,
            SourceScopeKey = projectId.ToString("D"),
            SourceSnapshotId = $"snapshot-{sourceSystem}",
            SnapshotHash = CognitiveMemoryHash.FromUtf8($"snapshot-{sourceSystem}").Value,
            ProviderVersion = "test-provider-v1",
            ScanStatus = CognitiveMemoryRunStatus.Succeeded,
            ObservedAtUtc = fixture.Clock.GetUtcNow(),
            CreatedAtUtc = fixture.Clock.GetUtcNow(),
            UpdatedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        };
        var sourceItem = new CognitiveMemorySourceItemRecord
        {
            ProjectId = projectId,
            SourceManifestId = manifest.Id,
            SourceSystem = sourceSystem,
            SourceItemKey = $"{sourceSystem}-{sourceItemType}",
            SourceItemType = sourceItemType,
            Title = sourceItemType,
            ContentText = content,
            Locator = $"/{sourceSystem}/{sourceItemType}",
            ContentHash = CognitiveMemoryHash.FromUtf8(content).Value,
            RedactionState = CognitiveMemoryRedactionState.Safe,
            AccessLevel = CognitiveMemoryAccessLevel.Project,
            AccessScope = "test",
            ObservedAtUtc = fixture.Clock.GetUtcNow(),
            CreatedAtUtc = fixture.Clock.GetUtcNow(),
            UpdatedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        };
        fixture.DbContext.AddRange(manifest, sourceItem);
        if (withEvidence)
        {
            fixture.DbContext.Add(new CognitiveMemoryEvidenceAnchorRecord
            {
                ProjectId = projectId,
                AnchorKind = CognitiveMemoryEvidenceAnchorKind.TextSpan,
                SourceManifestId = manifest.Id,
                SourceItemId = sourceItem.Id,
                SourceSystem = sourceSystem,
                Locator = sourceItem.Locator,
                StructuredPath = "$.content",
                TextStart = 0,
                TextEnd = content.Length,
                QuoteHash = CognitiveMemoryHash.FromUtf8($"{sourceItem.Id:D}:quote").Value,
                TrustLevel = CognitiveMemorySourceTrustLevel.RuntimeSource,
                RedactionState = CognitiveMemoryRedactionState.Safe,
                SourceHash = sourceItem.ContentHash,
                ObservedAtUtc = fixture.Clock.GetUtcNow(),
                CreatedAtUtc = fixture.Clock.GetUtcNow(),
                ConcurrencyToken = Guid.NewGuid()
            });
        }

        await fixture.DbContext.SaveChangesAsync();
    }

    private static async Task<ConsolidationFixture> CreateFixtureAsync()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        return new ConsolidationFixture(connection, new TestDbContextFactory(options), dbContext, new FixedClock());
    }

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

    private sealed class ConsolidationFixture(
        SqliteConnection connection,
        TestDbContextFactory factory,
        AppDbContext dbContext,
        FixedClock clock) : IAsyncDisposable
    {
        public TestDbContextFactory Factory { get; } = factory;

        public AppDbContext DbContext { get; } = dbContext;

        public FixedClock Clock { get; } = clock;

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}

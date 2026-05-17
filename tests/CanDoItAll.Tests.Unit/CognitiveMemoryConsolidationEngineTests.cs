using System.Text.Json;
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

        var reviewUiService = new CognitiveMemoryReviewUiService(
            fixture.Factory,
            fixture.Clock,
            new CognitiveMemoryConsolidationCandidateApplicator(new CognitiveMemoryRecordValidator()));
        var snapshot = await reviewUiService.GetSnapshotAsync(new CognitiveMemoryReviewUiQuery(projectId));
        var reviewQueueItem = Assert.Single(snapshot.ReviewItems);
        Assert.NotNull(reviewQueueItem.CandidatePreview);
        Assert.Equal(CognitiveMemoryConsolidationCandidateKind.Episode, reviewQueueItem.CandidatePreview.CandidateKind);
        Assert.Contains("Docker deployment process completed", reviewQueueItem.CandidatePreview.ProposedMemoryText, StringComparison.Ordinal);
        Assert.Contains("Docker deployment process completed", reviewQueueItem.CandidatePreview.SourceExcerpt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_AppliesAcceptedCandidateToCanonicalMemoryWhenReviewIsDisabled()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SeedSourceAsync(fixture, projectId, "ProjectStructure", "CapabilityNode", "FieldOps Planner uses offline-first route planning for dispatchers.", withEvidence: true);
        var profile = CognitiveMemoryConsolidationProfile.IncrementalRecent with
        {
            Name = "direct-canonicalization",
            CreateHumanReviewItems = false
        };
        var engine = CreateEngine(fixture);

        var result = await engine.RunAsync(Request(projectId, "consolidation-direct", profile));

        await using var dbContext = fixture.Factory.CreateDbContext();
        var candidate = Assert.Single(await dbContext.Set<CognitiveMemoryConsolidationCandidateRecord>().ToListAsync());
        var mutation = Assert.Single(await dbContext.Set<CognitiveMemoryMutationCommandRecord>().ToListAsync());
        var memoryRecord = Assert.Single(await dbContext.Set<CognitiveMemoryRecord>().ToListAsync());
        var claim = Assert.Single(await dbContext.Set<CognitiveMemoryClaimRecord>().ToListAsync());
        var sourceLink = Assert.Single(await dbContext.Set<CognitiveMemorySourceLinkRecord>().ToListAsync());
        var recordEvidence = Assert.Single(await dbContext.Set<CognitiveMemoryRecordEvidenceAnchorRecord>().ToListAsync());
        var claimEvidence = Assert.Single(await dbContext.Set<CognitiveMemoryClaimEvidenceLinkRecord>().ToListAsync());
        var affectedMemoryIds = JsonSerializer.Deserialize<Guid[]>(mutation.AffectedMemoryRecordIdsJson);
        var affectedClaimIds = JsonSerializer.Deserialize<Guid[]>(mutation.AffectedClaimIdsJson);

        Assert.Equal(CognitiveMemoryRunStatus.Succeeded, result.Status);
        Assert.Equal(1, result.CandidatesCreated);
        Assert.Equal(0, result.ReviewItemsCreated);
        Assert.Equal(CognitiveMemoryConsolidationCandidateStatus.MutationSubmitted, candidate.Status);
        Assert.Equal(memoryRecord.Id, candidate.MemoryRecordId);
        Assert.Equal(CognitiveMemoryMutationCommandStatus.Accepted, mutation.Status);
        Assert.False(mutation.RequiresHumanReview);
        Assert.Equal(CognitiveMemoryValidationState.MachineGenerated, memoryRecord.ValidationState);
        Assert.Equal(CognitiveMemoryStabilityState.Experimental, memoryRecord.StabilityState);
        Assert.Equal(memoryRecord.Id, claim.MemoryRecordId);
        Assert.Equal(memoryRecord.Id, sourceLink.MemoryRecordId);
        Assert.Equal(memoryRecord.Id, recordEvidence.MemoryRecordId);
        Assert.Equal(claim.Id, claimEvidence.ClaimId);
        Assert.Contains(memoryRecord.Id, affectedMemoryIds ?? []);
        Assert.Contains(claim.Id, affectedClaimIds ?? []);
    }

    [Fact]
    public async Task RunAsync_PrefersProjectNodesAndSkipsNonMemoryProjectStructureRows()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SeedSourceAsync(fixture, projectId, "ExternalFile", "UploadedFileChunk", "External markdown section about offline sync.", withEvidence: true);
        await SeedSourceAsync(fixture, projectId, "WorkbenchProjectStructure", "ProjectLink", "project contains custom node", withEvidence: true);
        await SeedSourceAsync(fixture, projectId, "WorkbenchProjectStructure", "ProjectNode", "Title: Follow-up sample source document\nObject type: File\nRoute: /storage/objects/preview?ref=abc", withEvidence: true);
        await SeedSourceAsync(fixture, projectId, "WorkbenchProjectStructure", "ProjectNode", "Title: Offline sync architecture\nObject type: ProjectBlock\nNotes: Project node: offline sync architecture and queue conflict review.", withEvidence: true);
        var profile = CognitiveMemoryConsolidationProfile.IncrementalRecent with
        {
            MaxItems = 1
        };
        var engine = CreateEngine(fixture);

        var result = await engine.RunAsync(Request(projectId, "consolidation-source-priority", profile));

        await using var dbContext = fixture.Factory.CreateDbContext();
        var candidate = Assert.Single(await dbContext.Set<CognitiveMemoryConsolidationCandidateRecord>().ToListAsync());
        var sourceItem = await dbContext.Set<CognitiveMemorySourceItemRecord>().SingleAsync(item => item.Id == candidate.SourceItemId);

        Assert.Equal(CognitiveMemoryRunStatus.Succeeded, result.Status);
        Assert.Equal(1, result.SourceItemsScanned);
        Assert.Equal("WorkbenchProjectStructure", sourceItem.SourceSystem);
        Assert.Equal("ProjectNode", sourceItem.SourceItemType);
        Assert.DoesNotContain("Object type: File", sourceItem.ContentText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DecideReviewItemAsync_ApproveConsolidationCandidateAppliesCanonicalMemory()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SeedSourceAsync(fixture, projectId, "ProcessRuntime", "ProcessRun", "Docker deployment process completed.", withEvidence: true);
        var engine = CreateEngine(fixture);
        await engine.RunAsync(Request(projectId, "consolidation-review-approval"));
        await using var readContext = fixture.Factory.CreateDbContext();
        var review = Assert.Single(await readContext.Set<CognitiveMemoryReviewItemRecord>().ToListAsync());
        var service = new CognitiveMemoryReviewUiService(
            fixture.Factory,
            fixture.Clock,
            new CognitiveMemoryConsolidationCandidateApplicator(new CognitiveMemoryRecordValidator()));

        var result = await service.DecideReviewItemAsync(new CognitiveMemoryReviewDecisionRequest(
            new CognitiveMemoryReviewItemId(review.Id),
            CognitiveMemoryReviewDecisionKind.Approve,
            "agent:reviewer",
            string.Empty,
            review.ConcurrencyToken));

        await using var dbContext = fixture.Factory.CreateDbContext();
        var candidate = Assert.Single(await dbContext.Set<CognitiveMemoryConsolidationCandidateRecord>().ToListAsync());
        var memoryRecord = Assert.Single(await dbContext.Set<CognitiveMemoryRecord>().ToListAsync());
        var mutation = Assert.Single(await dbContext.Set<CognitiveMemoryMutationCommandRecord>().ToListAsync());

        Assert.Equal(CognitiveMemoryReviewStatus.Approved, result.Status);
        Assert.Equal(CognitiveMemoryConsolidationCandidateStatus.MutationSubmitted, candidate.Status);
        Assert.Equal(memoryRecord.Id, candidate.MemoryRecordId);
        Assert.Equal(CognitiveMemoryValidationState.Approved, memoryRecord.ValidationState);
        Assert.Equal(CognitiveMemoryStabilityState.Active, memoryRecord.StabilityState);
        Assert.Equal(CognitiveMemoryMutationCommandStatus.Accepted, mutation.Status);
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
            new CognitiveMemoryConsolidationCandidateApplicator(new CognitiveMemoryRecordValidator()),
            driver,
            fixture.Clock,
            NullLogger<CognitiveMemoryConsolidationEngine>.Instance);
    }

    private static CognitiveMemoryConsolidationRunRequest Request(Guid projectId, string idempotencyKey)
        => Request(projectId, idempotencyKey, CognitiveMemoryConsolidationProfile.IncrementalRecent);

    private static CognitiveMemoryConsolidationRunRequest Request(
        Guid projectId,
        string idempotencyKey,
        CognitiveMemoryConsolidationProfile profile)
        => new(
            projectId,
            CognitiveMemoryConsolidationMode.IncrementalRecent,
            CognitiveMemoryConsolidationTriggerKind.Manual,
            profile,
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
        var contentHash = CognitiveMemoryHash.FromUtf8(content).Value;
        var sourceKeySuffix = contentHash[..12];
        var manifest = new CognitiveMemorySourceManifestRecord
        {
            ProjectId = projectId,
            SourceSystem = sourceSystem,
            SourceScopeKey = projectId.ToString("D"),
            SourceSnapshotId = $"snapshot-{sourceSystem}-{sourceItemType}-{sourceKeySuffix}",
            SnapshotHash = contentHash,
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
            SourceItemKey = $"{sourceSystem}-{sourceItemType}-{sourceKeySuffix}",
            SourceItemType = sourceItemType,
            Title = sourceItemType,
            ContentText = content,
            Locator = $"/{sourceSystem}/{sourceItemType}",
            ContentHash = contentHash,
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

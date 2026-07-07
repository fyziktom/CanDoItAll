using System.Text.Json;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using CanDoItAll.Tests.Support;
namespace CanDoItAll.Tests.Unit;

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
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
        var contextFrame = Assert.Single(await dbContext.Set<CognitiveMemoryContextFrameRecord>().ToListAsync());
        var entity = Assert.Single(await dbContext.Set<CognitiveMemoryEntityRecord>().ToListAsync());
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
        Assert.Equal(contextFrame.Id, memoryRecord.PrimaryContextFrameId);
        Assert.Equal(contextFrame.Id, claim.PrimaryContextFrameId);
        Assert.Equal(contextFrame.Id, entity.PrimaryContextFrameId);
        Assert.Equal(memoryRecord.Id, claim.MemoryRecordId);
        Assert.Equal(memoryRecord.Id, sourceLink.MemoryRecordId);
        Assert.Equal(memoryRecord.Id, recordEvidence.MemoryRecordId);
        Assert.Equal(claim.Id, claimEvidence.ClaimId);
        Assert.Contains(memoryRecord.Id, affectedMemoryIds ?? []);
        Assert.Contains(claim.Id, affectedClaimIds ?? []);
    }

    [Fact]
    public async Task RunAsync_ExtractsBusinessPlanPlanningFactsIntoCandidatePayload()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var content = """
            # LB4U business plan
            The product section defines the LB4U service and the pilot validation assumptions.
            Marketing activities include customer interviews, launch campaign, and sales channel testing.
            Expense planning covers salary costs, payroll reserve, licenses, supplier procurement, and equipment.
            Staffing plan describes phased recruitment of the core team and employee onboarding.
            Risk analysis tracks market adoption, compliance, and cash flow assumptions.
            """;
        await SeedSourceAsync(fixture, projectId, "ExternalFile", "UploadedFileChunk", content, withEvidence: true);
        var engine = CreateEngine(fixture);

        var result = await engine.RunAsync(Request(projectId, "consolidation-lb4u-business-plan"));

        await using var dbContext = fixture.Factory.CreateDbContext();
        var candidate = Assert.Single(await dbContext.Set<CognitiveMemoryConsolidationCandidateRecord>().ToListAsync());
        var payload = JsonSerializer.Deserialize(
            candidate.PayloadJson,
            CognitiveMemoryJsonSerializerContext.Default.CognitiveMemoryConsolidationCandidatePayload);

        Assert.Equal(CognitiveMemoryRunStatus.Succeeded, result.Status);
        Assert.NotNull(payload);
        Assert.Contains("business-plan", payload.Summary, StringComparison.Ordinal);
        Assert.Contains("market-and-marketing", payload.Summary, StringComparison.Ordinal);
        Assert.Contains("finance-and-expenses", payload.Summary, StringComparison.Ordinal);
        Assert.Contains("staffing", payload.Summary, StringComparison.Ordinal);
        Assert.Contains("salary costs", payload.Summary, StringComparison.Ordinal);
        Assert.Contains("detected dimensions", payload.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ExtractsLocalizedPlanningFactsWithDiacritics()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var content = """
            Podnikatelský plán popisuje produktové tlačítko pro nemocnice a domovy seniorů.
            Marketing řeší vstup na trh, zákazníky, konkurenci a prodejní kampaň.
            Finanční rozpočet obsahuje náklady, mzdy, faktury, obrat a cenu certifikace.
            Personál a tým budou růst po fázích podle kapacit a náboru pracovníků.
            Nákup, dodavatel, objednávka, montáž a instalace patří do provozního plánu.
            Rizika pokrývají testování, ověření pilotu a schválení certifikace.
            """;
        await SeedSourceAsync(fixture, projectId, "ExternalFile", "UploadedFileChunk", content, withEvidence: true);
        var engine = CreateEngine(fixture);

        var result = await engine.RunAsync(Request(projectId, "consolidation-localized-lb4u-business-plan"));

        await using var dbContext = fixture.Factory.CreateDbContext();
        var candidate = Assert.Single(await dbContext.Set<CognitiveMemoryConsolidationCandidateRecord>().ToListAsync());
        var payload = JsonSerializer.Deserialize(
            candidate.PayloadJson,
            CognitiveMemoryJsonSerializerContext.Default.CognitiveMemoryConsolidationCandidatePayload);

        Assert.Equal(CognitiveMemoryRunStatus.Succeeded, result.Status);
        Assert.NotNull(payload);
        Assert.Contains("business-plan", payload.Summary, StringComparison.Ordinal);
        Assert.Contains("finance-and-expenses", payload.Summary, StringComparison.Ordinal);
        Assert.Contains("staffing", payload.Summary, StringComparison.Ordinal);
        Assert.Contains("operations-and-procurement", payload.Summary, StringComparison.Ordinal);
        Assert.Contains("risk-and-validation", payload.Summary, StringComparison.Ordinal);
        Assert.Contains("mzdy", payload.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_SkipsContactOnlySourceItems()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var content = """
            Kontakt
            Lucie Example
            Tel: +420 732 936 929
            lucie.example@example.com
            Zuzana Example
            Tel: +420 603 426 004
            zuzana.example@example.com
            """;
        await SeedSourceAsync(fixture, projectId, "ExternalFile", "UploadedFileChunk", content, withEvidence: true);
        var engine = CreateEngine(fixture);

        var result = await engine.RunAsync(Request(projectId, "consolidation-contact-only-skip"));

        await using var dbContext = fixture.Factory.CreateDbContext();
        Assert.Equal(CognitiveMemoryRunStatus.Succeeded, result.Status);
        Assert.Equal(1, result.SourceItemsScanned);
        Assert.Equal(0, result.CandidatesCreated);
        Assert.Empty(await dbContext.Set<CognitiveMemoryConsolidationCandidateRecord>().ToListAsync());
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
    public async Task RunAsync_SubsequentRunsProcessUnprocessedSourceItemsAfterFirstPage()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SeedSourceAsync(fixture, projectId, "WorkbenchProjectStructure", "ProjectNode", "Title: Alpha\nNotes: First project node source.", withEvidence: true);
        await SeedSourceAsync(fixture, projectId, "WorkbenchProjectStructure", "ProjectNode", "Title: Beta\nNotes: Second project node source has more text.", withEvidence: true);
        var profile = CognitiveMemoryConsolidationProfile.IncrementalRecent with
        {
            MaxItems = 1
        };
        var engine = CreateEngine(fixture);

        var first = await engine.RunAsync(Request(projectId, "consolidation-page-1", profile));
        var second = await engine.RunAsync(Request(projectId, "consolidation-page-2", profile));

        await using var dbContext = fixture.Factory.CreateDbContext();
        var candidates = await dbContext.Set<CognitiveMemoryConsolidationCandidateRecord>()
            .ToListAsync();

        Assert.Equal(CognitiveMemoryRunStatus.Succeeded, first.Status);
        Assert.Equal(CognitiveMemoryRunStatus.Succeeded, second.Status);
        Assert.Equal(1, first.SourceItemsScanned);
        Assert.Equal(1, second.SourceItemsScanned);
        Assert.Equal(2, candidates.Count);
        Assert.Equal(2, candidates.Select(candidate => candidate.SourceItemId).Distinct().Count());
    }

    [Fact]
    public async Task RunAsync_BackfillsReviewItemsForReviewRequiredCandidatesWithoutReviewItem()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SeedSourceAsync(fixture, projectId, "WorkbenchProjectStructure", "ProjectNode", "Title: Staffing\nNotes: Core factory staffing model is 30 FTE.", withEvidence: true);
        var engine = CreateEngine(fixture);
        var noReviewBudget = new CognitiveMemoryConsolidationBudget(10, 10, 0, 4096, TimeSpan.FromMinutes(5));

        var first = await engine.RunAsync(Request(projectId, "consolidation-review-budget-exhausted", CognitiveMemoryConsolidationProfile.IncrementalRecent, noReviewBudget));
        var second = await engine.RunAsync(Request(projectId, "consolidation-review-backfill", CognitiveMemoryConsolidationProfile.IncrementalRecent));

        await using var dbContext = fixture.Factory.CreateDbContext();
        var candidate = Assert.Single(await dbContext.Set<CognitiveMemoryConsolidationCandidateRecord>().ToListAsync());
        var review = Assert.Single(await dbContext.Set<CognitiveMemoryReviewItemRecord>().ToListAsync());

        Assert.Equal(CognitiveMemoryRunStatus.Succeeded, first.Status);
        Assert.Equal(0, first.ReviewItemsCreated);
        Assert.Equal(CognitiveMemoryRunStatus.Succeeded, second.Status);
        Assert.Equal(1, second.ReviewItemsCreated);
        Assert.Equal(CognitiveMemoryConsolidationCandidateStatus.ReviewRequired, candidate.Status);
        Assert.Equal(review.Id, candidate.ReviewItemId);
        Assert.Equal(CognitiveMemoryReviewStatus.Pending, review.Status);
        Assert.Equal(0, second.SourceItemsScanned);
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
        => Request(projectId, idempotencyKey, profile, new CognitiveMemoryConsolidationBudget(10, 10, 10, 4096, TimeSpan.FromMinutes(5)));

    private static CognitiveMemoryConsolidationRunRequest Request(
        Guid projectId,
        string idempotencyKey,
        CognitiveMemoryConsolidationProfile profile,
        CognitiveMemoryConsolidationBudget budget)
        => new(
            projectId,
            CognitiveMemoryConsolidationMode.IncrementalRecent,
            CognitiveMemoryConsolidationTriggerKind.Manual,
            profile,
            Policy(projectId),
            new CognitiveMemoryIdempotencyKey(idempotencyKey),
            budget);

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
        var database = PostgresTestDatabaseLease.Create("cognitivememoryconsolidationenginetests");

        var options = database.CreateAppDbContextOptions();
        var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        return new ConsolidationFixture(database, new TestDbContextFactory(options), dbContext, new FixedClock());
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
        PostgresTestDatabaseLease database,
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
            await database.DisposeAsync();
        }
    }
}

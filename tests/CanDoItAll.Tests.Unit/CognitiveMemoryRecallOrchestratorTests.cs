using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support.CognitiveMemory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class CognitiveMemoryRecallOrchestratorTests
{
    [Fact]
    public async Task RecallAsync_InhibitsDockerTestContextWhenProductionContextIsActive()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var production = await SeedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            "Docker production deployment",
            "Use production deployment Docker files and deployment runbooks for production releases.",
            "Production Docker deployment evidence.");
        var test = await SeedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000002"),
            "Docker test simulation",
            "Use the local Docker simulation only for test validation.",
            "Test Docker simulation evidence.");
        fixture.DbContext.Add(new CognitiveMemoryRelationRecord
        {
            ProjectId = projectId,
            SourceMemoryRecordId = production.RecordId,
            TargetMemoryRecordId = test.RecordId,
            RelationKind = CognitiveMemoryRelationKind.SemanticallyRelatedButContextSeparated,
            EvidenceCount = 1,
            RelationBucket = CognitiveMemoryScoreProjectionBucket.Inhibit,
            DisplayStrengthProjection = 0.95,
            Reason = "Local/test Docker simulation is related but not substitutable for production deployment.",
            AlgorithmVersion = "taxonomy-v1",
            CreatedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        });
        await fixture.DbContext.SaveChangesAsync();

        var adapter = new RecordingProjectionAdapter(
            [
                new CognitiveMemoryProjectionSearchHit(
                    new CognitiveMemoryProjectionPointId("prod"),
                    new CognitiveMemoryRecordId(production.RecordId),
                    CognitiveMemoryHash.FromUtf8("prod-payload"),
                    0.96,
                    new Dictionary<string, object?>()),
                new CognitiveMemoryProjectionSearchHit(
                    new CognitiveMemoryProjectionPointId("test"),
                    new CognitiveMemoryRecordId(test.RecordId),
                    CognitiveMemoryHash.FromUtf8("test-payload"),
                    0.95,
                    new Dictionary<string, object?>())
            ]);
        var orchestrator = CreateOrchestrator(fixture, adapter);

        var result = await orchestrator.RecallAsync(CreateRequest(projectId, "How should we use Docker for production deployment?"));

        Assert.Contains(result.Candidates, candidate =>
            candidate.MemoryRecordId.Value == production.RecordId &&
            candidate.DecisionKind == CognitiveMemoryRecallCandidateDecisionKind.Selected);
        var inhibited = Assert.Single(result.Candidates, candidate => candidate.MemoryRecordId.Value == test.RecordId);
        Assert.Equal(CognitiveMemoryRecallCandidateDecisionKind.Inhibited, inhibited.DecisionKind);
        Assert.Equal(CognitiveMemoryRecallExclusionReasonKind.ContextBoundary, inhibited.ExclusionReasonKind);
        Assert.Contains(inhibited.ScoreTrace.InputVectors.Single().Components, component => component.DimensionKind == CognitiveMemoryScoreDimensionKind.ContextSeparation);
        Assert.Contains(result.ContextPack.Sections, section => section.SectionKind == CognitiveMemoryRecallContextSectionKind.DoNotConfuseWith);
        Assert.Single(adapter.SearchRequests);
        Assert.Equal(projectId, adapter.SearchRequests[0].Filter?.ProjectId);
        Assert.NotNull(adapter.SearchRequests[0].Filter);
    }

    [Fact]
    public async Task RecallAsync_RecordsProjectionUnavailableAndFallsBackToLexicalRecall()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var memory = await SeedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000003"),
            "Docker production deployment",
            "Production Docker deployment uses release runbooks.",
            "Production source evidence.");
        var adapter = new RecordingProjectionAdapter([], supportsFilters: false);
        var orchestrator = CreateOrchestrator(fixture, adapter);

        var result = await orchestrator.RecallAsync(CreateRequest(projectId, "Docker production deployment"));

        Assert.Contains(result.Candidates, candidate =>
            candidate.MemoryRecordId.Value == memory.RecordId &&
            candidate.DecisionKind == CognitiveMemoryRecallCandidateDecisionKind.Selected);
        Assert.Contains(result.Stages, stage =>
            stage.ChannelKind == CognitiveMemoryRecallChannelKind.VectorProjection &&
            stage.Status == CognitiveMemoryRecallStageStatus.Unavailable);
        Assert.Contains(result.Warnings, warning => warning.Contains("typed filters", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(adapter.SearchRequests);
    }

    [Fact]
    public async Task RecallAsync_RecordsBudgetExclusionsInsteadOfSilentTruncation()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SeedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000004"),
            "Docker production deployment",
            "Production Docker deployment memory.",
            "Production source evidence.");
        await SeedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000005"),
            "Docker deployment validation",
            "Docker deployment validation memory.",
            "Validation source evidence.");
        var orchestrator = CreateOrchestrator(fixture, new RecordingProjectionAdapter([]));

        var result = await orchestrator.RecallAsync(CreateRequest(
            projectId,
            "Docker deployment",
            new CognitiveMemoryRecallBudget(8, 0, 4, 1, 1, 4096, 4096)));

        Assert.Single(result.Candidates, candidate => candidate.DecisionKind == CognitiveMemoryRecallCandidateDecisionKind.Selected);
        Assert.Contains(result.Candidates, candidate =>
            candidate.DecisionKind == CognitiveMemoryRecallCandidateDecisionKind.Excluded &&
            candidate.ExclusionReasonKind == CognitiveMemoryRecallExclusionReasonKind.BudgetLimit);
        Assert.Contains(result.Stages, stage =>
            stage.StageKind == CognitiveMemoryRecallTraceStageKind.FocusSelection &&
            stage.LimitingBudget == CognitiveMemoryBudgetLimit.ItemCount);
    }

    [Fact]
    public async Task RecallAsync_DoesNotInjectRestrictedSourceContentIntoContextPack()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var memory = await SeedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000006"),
            "Docker deployment secret handling",
            "Deployment memory must not leak restricted source text.",
            "SECRET_TOKEN=do-not-inject",
            sourceAccessLevel: CognitiveMemoryAccessLevel.Restricted,
            sourceRedactionState: CognitiveMemoryRedactionState.Restricted);
        var orchestrator = CreateOrchestrator(fixture, new RecordingProjectionAdapter([]));

        var result = await orchestrator.RecallAsync(CreateRequest(projectId, "Docker deployment secret handling"));

        var context = string.Join("\n", result.ContextPack.Sections.Select(section => section.Content));
        Assert.DoesNotContain("SECRET_TOKEN", context, StringComparison.Ordinal);
        Assert.Contains(result.ContextPack.SourceRefs, sourceRef =>
            sourceRef.MemoryRecordId.Value == memory.RecordId &&
            !sourceRef.IncludedInContext &&
            sourceRef.ExclusionReasonKind == CognitiveMemoryRecallExclusionReasonKind.AccessPolicy);
        Assert.Equal(0, await fixture.DbContext.Set<CognitiveMemoryMutationCommandRecord>().CountAsync());
    }

    [Fact]
    public async Task RecallAsync_DeduplicatesRepeatedMemoryAndSourceTextInContextPack()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var repeated = "Offline sync uses a local queue, idempotency keys, explicit conflict review, retry visibility, supervisor approval, and audit-safe evidence retention.";
        await SeedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000007"),
            "Offline sync architecture",
            repeated,
            $"{repeated} Additional source-only detail should not duplicate the memory summary prefix.");
        var orchestrator = CreateOrchestrator(fixture, new RecordingProjectionAdapter([]));

        var result = await orchestrator.RecallAsync(CreateRequest(projectId, "offline sync queue conflict"));

        var section = Assert.Single(result.ContextPack.Sections);
        Assert.Equal(1, CountOccurrences(section.Content, repeated));
        Assert.DoesNotContain($"Source detail: {repeated}", section.Content, StringComparison.Ordinal);
    }

    private static CognitiveMemoryRecallOrchestrator CreateOrchestrator(
        TestFixture fixture,
        RecordingProjectionAdapter adapter)
    {
        var registry = new CognitiveMemoryScoreSpaceRegistry();
        var driver = new CognitiveMemoryScoreGeometryDriver(registry);
        var signalLedger = new CognitiveMemorySignalLedger(
            fixture.Factory,
            registry,
            driver,
            fixture.Clock);
        var workspace = new CognitiveMemoryWorkspaceService(fixture.Factory, fixture.Clock);
        return new CognitiveMemoryRecallOrchestrator(
            fixture.Factory,
            new FakeCognitiveMemoryEmbeddingProvider(dimensions: 3),
            adapter,
            driver,
            signalLedger,
            workspace,
            fixture.Clock,
            NullLogger<CognitiveMemoryRecallOrchestrator>.Instance);
    }

    private static CognitiveMemoryRecallRequest CreateRequest(
        Guid projectId,
        string query,
        CognitiveMemoryRecallBudget? budget = null)
        => new(
            projectId,
            query,
            CognitiveMemoryRecallIntentKind.Deployment,
            CognitiveMemoryRecallMode.FocusedTaskContext,
            Policy(projectId),
            budget ?? new CognitiveMemoryRecallBudget(8, 1, 8, 4, 4, 4096, 4096),
            ProjectionCollectionName: new CognitiveMemoryProjectionCollectionName("cm-test"),
            ProjectionProfileId: new CognitiveMemoryProjectionProfileId("projection-v1"),
            EmbeddingProfileId: new CognitiveMemoryEmbeddingProfileId("embedding-v1"));

    private static CognitiveMemoryPolicyContext Policy(Guid projectId)
        => new(
            projectId,
            "agent:test",
            CognitiveMemoryAccessLevel.Project,
            new CognitiveMemoryPolicyProfileId("policy:test"),
            CognitiveMemoryRiskLevel.Low,
            AllowRestrictedContent: false);

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while (true)
        {
            index = text.IndexOf(value, index, StringComparison.Ordinal);
            if (index < 0)
            {
                return count;
            }

            count++;
            index += value.Length;
        }
    }

    private static async Task<SeededMemory> SeedMemoryAsync(
        TestFixture fixture,
        Guid projectId,
        Guid recordId,
        string title,
        string canonicalText,
        string sourceText,
        CognitiveMemoryAccessLevel sourceAccessLevel = CognitiveMemoryAccessLevel.Project,
        CognitiveMemoryRedactionState sourceRedactionState = CognitiveMemoryRedactionState.Safe)
    {
        var manifest = new CognitiveMemorySourceManifestRecord
        {
            ProjectId = projectId,
            SourceSystem = "unit-test",
            SourceScopeKey = projectId.ToString("D"),
            SourceSnapshotId = $"snapshot-{recordId:D}",
            SnapshotHash = CognitiveMemoryHash.FromUtf8($"snapshot-{recordId:D}").Value,
            ProviderVersion = "unit-test-v1",
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
            SourceSystem = "unit-test",
            SourceItemKey = $"source-{recordId:D}",
            SourceItemType = "test-node",
            Title = title,
            ContentText = sourceText,
            Locator = $"/unit/{recordId:D}",
            ContentHash = CognitiveMemoryHash.FromUtf8(sourceText).Value,
            RedactionState = sourceRedactionState,
            AccessLevel = sourceAccessLevel,
            AccessScope = "unit",
            ObservedAtUtc = fixture.Clock.GetUtcNow(),
            CreatedAtUtc = fixture.Clock.GetUtcNow(),
            UpdatedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        };
        var anchor = new CognitiveMemoryEvidenceAnchorRecord
        {
            ProjectId = projectId,
            AnchorKind = CognitiveMemoryEvidenceAnchorKind.TextSpan,
            SourceManifestId = manifest.Id,
            SourceItemId = sourceItem.Id,
            SourceSystem = "unit-test",
            Locator = sourceItem.Locator,
            StructuredPath = "$.content",
            TextStart = 0,
            TextEnd = Math.Min(sourceText.Length, 64),
            QuoteHash = CognitiveMemoryHash.FromUtf8($"{recordId:D}:quote").Value,
            TrustLevel = CognitiveMemorySourceTrustLevel.RuntimeSource,
            RedactionState = sourceRedactionState,
            SourceHash = sourceItem.ContentHash,
            ObservedAtUtc = fixture.Clock.GetUtcNow(),
            CreatedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        };
        var record = new CognitiveMemoryRecord
        {
            Id = recordId,
            ProjectId = projectId,
            Kind = CognitiveMemoryRecordKind.Semantic,
            Origin = CognitiveMemoryRecordOrigin.SourceDerived,
            Title = title,
            CanonicalText = canonicalText,
            SummaryText = canonicalText,
            TopicKey = title.ToLowerInvariant().Replace(' ', '.'),
            ValidationState = CognitiveMemoryValidationState.Approved,
            StabilityState = CognitiveMemoryStabilityState.Active,
            CreatedInMode = CognitiveMemoryOperationMode.Observe,
            AlgorithmVersion = "taxonomy-v1",
            ContentHash = CognitiveMemoryHash.FromUtf8(canonicalText).Value,
            SourceEvidenceCount = 1,
            EvidenceAnchorCount = 1,
            AccessLevel = CognitiveMemoryAccessLevel.Project,
            RiskLevel = CognitiveMemoryRiskLevel.Low,
            CreatedAtUtc = fixture.Clock.GetUtcNow(),
            UpdatedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        };
        fixture.DbContext.AddRange(
            manifest,
            sourceItem,
            anchor,
            record,
            new CognitiveMemorySourceLinkRecord
            {
                MemoryRecordId = record.Id,
                SourceManifestId = manifest.Id,
                SourceItemId = sourceItem.Id,
                EvidenceRole = CognitiveMemoryEvidenceRole.PrimarySource,
                Locator = sourceItem.Locator,
                QuoteHash = anchor.QuoteHash,
                Summary = sourceText,
                CreatedAtUtc = fixture.Clock.GetUtcNow()
            },
            new CognitiveMemoryRecordEvidenceAnchorRecord
            {
                MemoryRecordId = record.Id,
                EvidenceAnchorId = anchor.Id,
                EvidenceRole = CognitiveMemoryEvidenceRole.PrimarySource,
                Summary = sourceText,
                CreatedAtUtc = fixture.Clock.GetUtcNow()
            });
        await fixture.DbContext.SaveChangesAsync();
        return new SeededMemory(record.Id, sourceItem.Id, anchor.Id);
    }

    private static TestFixture CreateFixture()
    {
        AppDbContextModelRegistry.ConfigureAssemblies([typeof(CognitiveMemoryModuleAssemblyMarker).Assembly]);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"cognitive-memory-recall-{Guid.NewGuid():N}")
            .Options;
        return new TestFixture(new TestDbContextFactory(options), new FixedClock());
    }

    private sealed record SeededMemory(
        Guid RecordId,
        Guid SourceItemId,
        Guid EvidenceAnchorId);

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
    }

    private sealed record TestFixture(
        TestDbContextFactory Factory,
        FixedClock Clock)
    {
        public AppDbContext DbContext { get; } = Factory.CreateDbContext();
    }

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }

    private sealed class RecordingProjectionAdapter(
        IReadOnlyList<CognitiveMemoryProjectionSearchHit> hits,
        bool supportsFilters = true) : ICognitiveMemoryProjectionAdapter
    {
        public CognitiveMemoryProjectionAdapterCapabilities Capabilities { get; } = new(
            "fake-rag",
            supportsFilters,
            SupportsPayloadIndexes: true,
            SupportsDeleteByFilter: true,
            SupportsNamedVectors: false);

        public List<CognitiveMemoryProjectionSearchRequest> SearchRequests { get; } = [];

        public ValueTask EnsureCollectionAsync(
            CognitiveMemoryProjectionCollectionRequest request,
            CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask<IReadOnlyList<CognitiveMemoryProjectionPayloadIndexResult>> EnsurePayloadIndexesAsync(
            CognitiveMemoryProjectionPayloadIndexRequest request,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyList<CognitiveMemoryProjectionPayloadIndexResult>>([]);

        public ValueTask ProjectAsync(
            CognitiveMemoryProjectionWriteRequest request,
            CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask<CognitiveMemoryProjectionSearchResult> SearchAsync(
            CognitiveMemoryProjectionSearchRequest request,
            CancellationToken cancellationToken = default)
        {
            SearchRequests.Add(request);
            return ValueTask.FromResult(new CognitiveMemoryProjectionSearchResult(
                request.ProjectionProfileId,
                hits.Take(request.Page.Take).ToArray(),
                $"fake-rag:search:{hits.Count}"));
        }

        public ValueTask<CognitiveMemoryProjectionDeleteResult> DeleteBySourceAsync(
            CognitiveMemoryProjectionDeleteBySourceRequest request,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new CognitiveMemoryProjectionDeleteResult("fake-rag:delete"));
    }
}

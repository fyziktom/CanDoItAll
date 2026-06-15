using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging.Abstractions;

using CanDoItAll.Tests.Support;
namespace CanDoItAll.Tests.Integration;

public sealed class CognitiveMemoryConsolidationPersistenceModelTests
{
    [Fact]
    public async Task ConsolidationPersistenceModel_IndexesRunCandidatesCursorReportAndTypedEnumState()
    {
        await using var fixture = await CreateFixtureAsync();
        var entityTypes = fixture.DbContext.Model.GetEntityTypes().ToList();

        AssertEntityTable<CognitiveMemoryConsolidationRunRecord>(entityTypes, "CognitiveMemory_ConsolidationRuns");
        AssertEntityTable<CognitiveMemoryConsolidationCandidateRecord>(entityTypes, "CognitiveMemory_ConsolidationCandidates");
        AssertEntityTable<CognitiveMemoryConsolidationCursorRecord>(entityTypes, "CognitiveMemory_ConsolidationCursors");
        AssertEntityTable<CognitiveMemoryConsolidationReportRecord>(entityTypes, "CognitiveMemory_ConsolidationReports");

        foreach (var expectation in CognitiveMemoryEfGuardrails.ConsolidationIndexExpectations)
        {
            var entityType = Assert.Single(entityTypes, entityType => entityType.ClrType == expectation.EntityType);
            Assert.True(
                CognitiveMemoryEfGuardrails.HasExpectedIndex(entityType, expectation),
                $"Missing expected consolidation index on {expectation.EntityType.Name}: {string.Join(", ", expectation.PropertyNames)}.");
        }

        var registry = new CognitiveMemoryScoreSpaceRegistry();
        var definition = await registry.GetDefinitionAsync(
            CognitiveMemoryScoreSpaceKind.ConsolidationCandidate,
            CognitiveMemoryScoreSpaceRegistry.CurrentSchemaVersion);

        Assert.Contains(definition.Dimensions, dimension => dimension.Kind == CognitiveMemoryScoreDimensionKind.SourceSufficiency && dimension.Required);
        Assert.Contains(definition.Dimensions, dimension => dimension.Kind == CognitiveMemoryScoreDimensionKind.EvidenceStrength && dimension.Required);
        AssertEnumStateFieldsAreNotPersistedAsStrings(entityTypes);
    }

    [Fact]
    public async Task ConsolidationEngine_PersistsRunCandidateReportCursorScoreTraceAndProjectionInvalidation()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var seeded = await SeedLinkedMemoryAsync(fixture, projectId);
        var engine = CreateEngine(fixture);

        var result = await engine.RunAsync(new CognitiveMemoryConsolidationRunRequest(
            projectId,
            CognitiveMemoryConsolidationMode.IncrementalRecent,
            CognitiveMemoryConsolidationTriggerKind.SourceChanged,
            CognitiveMemoryConsolidationProfile.IncrementalRecent,
            Policy(projectId),
            new CognitiveMemoryIdempotencyKey("consolidation-projection"),
            new CognitiveMemoryConsolidationBudget(10, 10, 10, 4096, TimeSpan.FromMinutes(5))));

        fixture.DbContext.ChangeTracker.Clear();
        var run = await fixture.DbContext.Set<CognitiveMemoryConsolidationRunRecord>().SingleAsync();
        var generalRun = await fixture.DbContext.Set<CognitiveMemoryRunRecord>().SingleAsync(run => run.RunKind == CognitiveMemoryRunKind.Consolidation);
        var candidate = await fixture.DbContext.Set<CognitiveMemoryConsolidationCandidateRecord>().SingleAsync();
        var report = await fixture.DbContext.Set<CognitiveMemoryConsolidationReportRecord>().SingleAsync();
        var cursor = await fixture.DbContext.Set<CognitiveMemoryConsolidationCursorRecord>().SingleAsync();
        var projection = await fixture.DbContext.Set<CognitiveMemoryProjectionRecord>().SingleAsync();
        var components = await fixture.DbContext.Set<CognitiveMemoryScoreComponentRecord>()
            .Where(component => component.SpaceKind == CognitiveMemoryScoreSpaceKind.ConsolidationCandidate)
            .ToListAsync();

        Assert.Equal(CognitiveMemoryRunStatus.Succeeded, result.Status);
        Assert.Equal(result.RunId.Value, run.Id);
        Assert.Equal(run.Id, generalRun.Id);
        Assert.Equal(CognitiveMemoryRunStatus.Succeeded, generalRun.Status);
        Assert.Equal(seeded.SourceItemId, candidate.SourceItemId);
        Assert.Equal(CognitiveMemoryConsolidationCandidateStatus.ReviewRequired, candidate.Status);
        Assert.NotNull(candidate.MutationCommandId);
        Assert.NotNull(candidate.ReviewItemId);
        Assert.Equal(run.Id, report.RunId);
        Assert.Equal(run.Id, cursor.LastRunId);
        Assert.False(string.IsNullOrWhiteSpace(report.ReportHash));
        Assert.False(string.IsNullOrWhiteSpace(cursor.Cursor));
        Assert.True(projection.RebuildRequired);
        Assert.Equal(CognitiveMemoryProjectionStatus.RebuildRequired, projection.Status);
        Assert.Equal(CognitiveMemoryProjectionStaleReason.SourceHashChanged, projection.StaleReason);
        Assert.Contains(components, component => component.DimensionKind == CognitiveMemoryScoreDimensionKind.SourceSufficiency);
        Assert.Contains(components, component => component.DimensionKind == CognitiveMemoryScoreDimensionKind.EvidenceStrength);
        Assert.Equal(1, await fixture.DbContext.Set<CognitiveMemoryMutationCommandRecord>().CountAsync());
        Assert.Equal(1, await fixture.DbContext.Set<CognitiveMemoryReviewItemRecord>().CountAsync());
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

    private static CognitiveMemoryPolicyContext Policy(Guid projectId)
        => new(
            projectId,
            "agent:test",
            CognitiveMemoryAccessLevel.Project,
            new CognitiveMemoryPolicyProfileId("policy:test"),
            CognitiveMemoryRiskLevel.Low,
            AllowRestrictedContent: false);

    private static async Task<SeededMemory> SeedLinkedMemoryAsync(
        ConsolidationFixture fixture,
        Guid projectId)
    {
        var recordId = Guid.Parse("10000000-0000-0000-0000-000000000201");
        var content = "Process run completed Docker production deployment and produced a source-backed review candidate.";
        var manifest = new CognitiveMemorySourceManifestRecord
        {
            ProjectId = projectId,
            SourceSystem = "ProcessRuntime",
            SourceScopeKey = projectId.ToString("D"),
            SourceSnapshotId = $"snapshot-{recordId:D}",
            SnapshotHash = CognitiveMemoryHash.FromUtf8($"snapshot-{recordId:D}").Value,
            ProviderVersion = "integration-test-v1",
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
            SourceSystem = "ProcessRuntime",
            SourceItemKey = $"source-{recordId:D}",
            SourceItemType = "ProcessRun",
            Title = "Docker production deployment process",
            ContentText = content,
            Locator = $"/processes/{recordId:D}",
            ContentHash = CognitiveMemoryHash.FromUtf8(content).Value,
            RedactionState = CognitiveMemoryRedactionState.Safe,
            AccessLevel = CognitiveMemoryAccessLevel.Project,
            AccessScope = "integration",
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
            SourceSystem = "ProcessRuntime",
            Locator = sourceItem.Locator,
            StructuredPath = "$.content",
            TextStart = 0,
            TextEnd = content.Length,
            QuoteHash = CognitiveMemoryHash.FromUtf8($"{recordId:D}:quote").Value,
            TrustLevel = CognitiveMemorySourceTrustLevel.RuntimeSource,
            RedactionState = CognitiveMemoryRedactionState.Safe,
            SourceHash = sourceItem.ContentHash,
            ObservedAtUtc = fixture.Clock.GetUtcNow(),
            CreatedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        };
        var memory = new CognitiveMemoryRecord
        {
            Id = recordId,
            ProjectId = projectId,
            Kind = CognitiveMemoryRecordKind.Episodic,
            Origin = CognitiveMemoryRecordOrigin.SourceDerived,
            Title = "Docker production deployment episode",
            CanonicalText = "Docker production deployment episode.",
            SummaryText = "Docker production deployment episode.",
            TopicKey = "docker.production",
            ValidationState = CognitiveMemoryValidationState.HumanReviewed,
            StabilityState = CognitiveMemoryStabilityState.Active,
            CreatedInMode = CognitiveMemoryOperationMode.Observe,
            AlgorithmVersion = "taxonomy-v1",
            ContentHash = CognitiveMemoryHash.FromUtf8("Docker production deployment episode.").Value,
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
            memory,
            new CognitiveMemorySourceLinkRecord
            {
                MemoryRecordId = memory.Id,
                SourceManifestId = manifest.Id,
                SourceItemId = sourceItem.Id,
                EvidenceRole = CognitiveMemoryEvidenceRole.PrimarySource,
                Locator = sourceItem.Locator,
                QuoteHash = anchor.QuoteHash,
                Summary = content,
                CreatedAtUtc = fixture.Clock.GetUtcNow()
            },
            new CognitiveMemoryProjectionRecord
            {
                ProjectId = projectId,
                MemoryRecordId = memory.Id,
                ProjectionStoreKind = CognitiveMemoryProjectionStoreKind.GenericRag,
                ProjectionKind = CognitiveMemoryProjectionKind.VectorCollection,
                TargetProviderName = "fake-rag",
                CollectionName = "cm-test",
                PointId = "point-1",
                ProjectionProfileId = "projection-v1",
                EmbeddingProfileId = "embedding-v1",
                ProjectionSchemaVersion = "projection-schema-v1",
                AlgorithmVersion = "projection-v1",
                VectorDimensions = 3,
                SourceHash = sourceItem.ContentHash,
                PayloadHash = CognitiveMemoryHash.FromUtf8("payload").Value,
                Status = CognitiveMemoryProjectionStatus.Projected,
                StaleReason = CognitiveMemoryProjectionStaleReason.None,
                RebuildRequired = false,
                CreatedAtUtc = fixture.Clock.GetUtcNow(),
                UpdatedAtUtc = fixture.Clock.GetUtcNow(),
                LastProjectedAtUtc = fixture.Clock.GetUtcNow(),
                ConcurrencyToken = Guid.NewGuid()
            });
        await fixture.DbContext.SaveChangesAsync();
        return new SeededMemory(memory.Id, sourceItem.Id, anchor.Id);
    }

    private static async Task<ConsolidationFixture> CreateFixtureAsync()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        var database = PostgresTestDatabaseLease.Create("cognitivememoryconsolidationpersistencemodeltests");

        var options = database.CreateAppDbContextOptions();
        var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        return new ConsolidationFixture(database, new TestDbContextFactory(options), dbContext, new FixedClock());
    }

    private static void AssertEntityTable<TEntity>(IReadOnlyList<IEntityType> entityTypes, string tableName)
    {
        var entityType = Assert.Single(entityTypes, entityType => entityType.ClrType == typeof(TEntity));
        Assert.Equal(tableName, entityType.GetTableName());
    }

    private static void AssertEnumStateFieldsAreNotPersistedAsStrings(IReadOnlyList<IEntityType> entityTypes)
    {
        var stateProperties = new Dictionary<Type, string[]>
        {
            [typeof(CognitiveMemoryConsolidationRunRecord)] =
            [
                nameof(CognitiveMemoryConsolidationRunRecord.Mode),
                nameof(CognitiveMemoryConsolidationRunRecord.TriggerKind),
                nameof(CognitiveMemoryConsolidationRunRecord.Status)
            ],
            [typeof(CognitiveMemoryConsolidationCandidateRecord)] =
            [
                nameof(CognitiveMemoryConsolidationCandidateRecord.CandidateKind),
                nameof(CognitiveMemoryConsolidationCandidateRecord.Status),
                nameof(CognitiveMemoryConsolidationCandidateRecord.ScoreBucket)
            ],
            [typeof(CognitiveMemoryConsolidationCursorRecord)] =
            [
                nameof(CognitiveMemoryConsolidationCursorRecord.Mode)
            ]
        };

        foreach (var (entityClrType, propertyNames) in stateProperties)
        {
            var entityType = Assert.Single(entityTypes, entityType => entityType.ClrType == entityClrType);
            foreach (var propertyName in propertyNames)
            {
                var property = Assert.IsAssignableFrom<IProperty>(entityType.FindProperty(propertyName));
                Assert.True(property.ClrType.IsEnum || Nullable.GetUnderlyingType(property.ClrType)?.IsEnum == true, $"{entityClrType.Name}.{propertyName} should remain a typed enum.");
                Assert.NotEqual(typeof(string), property.ClrType);
            }
        }
    }

    private sealed record SeededMemory(
        Guid RecordId,
        Guid SourceItemId,
        Guid EvidenceAnchorId);

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

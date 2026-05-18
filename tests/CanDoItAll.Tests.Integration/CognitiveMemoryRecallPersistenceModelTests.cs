using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support.CognitiveMemory;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration;

public sealed class CognitiveMemoryRecallPersistenceModelTests
{
    [Fact]
    public async Task RecallPersistenceModel_IndexesTraceCandidatesContextPackAndTypedEnumState()
    {
        await using var fixture = await CreateFixtureAsync();
        var entityTypes = fixture.DbContext.Model.GetEntityTypes().ToList();

        AssertEntityTable<CognitiveMemoryRecallTraceRecord>(entityTypes, "CognitiveMemory_RecallTraces");
        AssertEntityTable<CognitiveMemoryRecallTraceStageRecord>(entityTypes, "CognitiveMemory_RecallTraceStages");
        AssertEntityTable<CognitiveMemoryRecallCandidateRecord>(entityTypes, "CognitiveMemory_RecallCandidates");
        AssertEntityTable<CognitiveMemoryRecallContextPackRecord>(entityTypes, "CognitiveMemory_RecallContextPacks");
        AssertEntityTable<CognitiveMemoryRecallContextSectionRecord>(entityTypes, "CognitiveMemory_RecallContextSections");
        AssertEntityTable<CognitiveMemoryRecallSourceRefRecord>(entityTypes, "CognitiveMemory_RecallSourceRefs");

        foreach (var expectation in CognitiveMemoryEfGuardrails.RecallIndexExpectations)
        {
            var entityType = Assert.Single(entityTypes, entityType => entityType.ClrType == expectation.EntityType);
            Assert.True(
                CognitiveMemoryEfGuardrails.HasExpectedIndex(entityType, expectation),
                $"Missing expected recall index on {expectation.EntityType.Name}: {string.Join(", ", expectation.PropertyNames)}.");
        }

        var registry = new CognitiveMemoryScoreSpaceRegistry();
        var definition = await registry.GetDefinitionAsync(
            CognitiveMemoryScoreSpaceKind.RecallCandidate,
            CognitiveMemoryScoreSpaceRegistry.CurrentSchemaVersion);

        Assert.Contains(definition.Dimensions, dimension => dimension.Kind == CognitiveMemoryScoreDimensionKind.ContextSeparation);
        Assert.Contains(definition.Dimensions, dimension => dimension.Kind == CognitiveMemoryScoreDimensionKind.MetadataFit);
        AssertEnumStateFieldsAreNotPersistedAsStrings(entityTypes);
    }

    [Fact]
    public async Task RecallOrchestrator_PersistsTraceStagesCandidatesContextPackSourceRefsAndScoreComponents()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var memory = await SeedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000101"),
            "Docker production deployment",
            "Production Docker deployment memory uses deployment sources.",
            "Production Docker deployment source detail.");
        var adapter = new RecordingProjectionAdapter(
            [
                new CognitiveMemoryProjectionSearchHit(
                    new CognitiveMemoryProjectionPointId("prod"),
                    new CognitiveMemoryRecordId(memory.RecordId),
                    CognitiveMemoryHash.FromUtf8("prod-payload"),
                    0.94,
                    new Dictionary<string, object?>())
            ]);
        var orchestrator = CreateOrchestrator(fixture, adapter);

        var result = await orchestrator.RecallAsync(new CognitiveMemoryRecallRequest(
            projectId,
            "Docker production deployment",
            CognitiveMemoryRecallIntentKind.Deployment,
            CognitiveMemoryRecallMode.FocusedTaskContext,
            Policy(projectId),
            new CognitiveMemoryRecallBudget(8, 1, 8, 4, 4, 4096, 4096),
            ProjectionCollectionName: new CognitiveMemoryProjectionCollectionName("cm-test"),
            ProjectionProfileId: new CognitiveMemoryProjectionProfileId("projection-v1"),
            EmbeddingProfileId: new CognitiveMemoryEmbeddingProfileId("embedding-v1")));

        fixture.DbContext.ChangeTracker.Clear();
        var trace = await fixture.DbContext.Set<CognitiveMemoryRecallTraceRecord>().SingleAsync();
        var stages = await fixture.DbContext.Set<CognitiveMemoryRecallTraceStageRecord>().ToListAsync();
        var candidate = await fixture.DbContext.Set<CognitiveMemoryRecallCandidateRecord>().SingleAsync();
        var pack = await fixture.DbContext.Set<CognitiveMemoryRecallContextPackRecord>().SingleAsync();
        var section = await fixture.DbContext.Set<CognitiveMemoryRecallContextSectionRecord>().SingleAsync(item => item.SectionKind == CognitiveMemoryRecallContextSectionKind.SelectedMemory);
        var sourceRefs = await fixture.DbContext.Set<CognitiveMemoryRecallSourceRefRecord>().ToListAsync();
        var components = await fixture.DbContext.Set<CognitiveMemoryScoreComponentRecord>()
            .Where(component => component.SpaceKind == CognitiveMemoryScoreSpaceKind.RecallCandidate)
            .ToListAsync();

        Assert.Equal(result.TraceId, trace.Id);
        Assert.Equal(CognitiveMemoryRecallMode.FocusedTaskContext, trace.RecallMode);
        Assert.Equal(pack.Id, trace.ContextPackId);
        Assert.Contains(stages, stage => stage.ChannelKind == CognitiveMemoryRecallChannelKind.VectorProjection && stage.Status == CognitiveMemoryRecallStageStatus.Completed);
        Assert.Equal(CognitiveMemoryRecallCandidateDecisionKind.Selected, candidate.DecisionKind);
        Assert.Equal(memory.RecordId, candidate.MemoryRecordId);
        Assert.True(candidate.DisplayRankProjection > 0);
        Assert.Equal(pack.Id, section.ContextPackId);
        Assert.Contains(sourceRefs, sourceRef => sourceRef.IncludedInContext && sourceRef.SourceItemId == memory.SourceItemId);
        Assert.Contains(components, component => component.DimensionKind == CognitiveMemoryScoreDimensionKind.SemanticSimilarity);
        Assert.Contains(components, component => component.DimensionKind == CognitiveMemoryScoreDimensionKind.SourceSufficiency);
        Assert.Equal(0, await fixture.DbContext.Set<CognitiveMemoryMutationCommandRecord>().CountAsync());
        Assert.Equal(1, await fixture.DbContext.Set<CognitiveMemorySourceItemRecord>().CountAsync());
    }

    private static CognitiveMemoryRecallOrchestrator CreateOrchestrator(
        RecallFixture fixture,
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

    private static async Task<SeededMemory> SeedMemoryAsync(
        RecallFixture fixture,
        Guid projectId,
        Guid recordId,
        string title,
        string canonicalText,
        string sourceText)
    {
        var manifest = new CognitiveMemorySourceManifestRecord
        {
            ProjectId = projectId,
            SourceSystem = "integration-test",
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
            SourceSystem = "integration-test",
            SourceItemKey = $"source-{recordId:D}",
            SourceItemType = "test-node",
            Title = title,
            ContentText = sourceText,
            Locator = $"/integration/{recordId:D}",
            ContentHash = CognitiveMemoryHash.FromUtf8(sourceText).Value,
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
            SourceSystem = "integration-test",
            Locator = sourceItem.Locator,
            StructuredPath = "$.content",
            TextStart = 0,
            TextEnd = sourceText.Length,
            QuoteHash = CognitiveMemoryHash.FromUtf8($"{recordId:D}:quote").Value,
            TrustLevel = CognitiveMemorySourceTrustLevel.RuntimeSource,
            RedactionState = CognitiveMemoryRedactionState.Safe,
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
            TopicKey = "docker.production",
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

    private static CognitiveMemoryPolicyContext Policy(Guid projectId)
        => new(
            projectId,
            "agent:test",
            CognitiveMemoryAccessLevel.Project,
            new CognitiveMemoryPolicyProfileId("policy:test"),
            CognitiveMemoryRiskLevel.Low,
            AllowRestrictedContent: false);

    private static async Task<RecallFixture> CreateFixtureAsync()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        return new RecallFixture(connection, new TestDbContextFactory(options), dbContext, new FixedClock());
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
            [typeof(CognitiveMemoryRecallTraceRecord)] =
            [
                nameof(CognitiveMemoryRecallTraceRecord.RecallMode),
                nameof(CognitiveMemoryRecallTraceRecord.Outcome),
                nameof(CognitiveMemoryRecallTraceRecord.LimitingBudget)
            ],
            [typeof(CognitiveMemoryRecallTraceStageRecord)] =
            [
                nameof(CognitiveMemoryRecallTraceStageRecord.StageKind),
                nameof(CognitiveMemoryRecallTraceStageRecord.ChannelKind),
                nameof(CognitiveMemoryRecallTraceStageRecord.Status),
                nameof(CognitiveMemoryRecallTraceStageRecord.LimitingBudget)
            ],
            [typeof(CognitiveMemoryRecallCandidateRecord)] =
            [
                nameof(CognitiveMemoryRecallCandidateRecord.PrimaryChannelKind),
                nameof(CognitiveMemoryRecallCandidateRecord.DecisionKind),
                nameof(CognitiveMemoryRecallCandidateRecord.ExclusionReasonKind),
                nameof(CognitiveMemoryRecallCandidateRecord.ScoreBucket)
            ],
            [typeof(CognitiveMemoryRecallContextSectionRecord)] =
            [
                nameof(CognitiveMemoryRecallContextSectionRecord.SectionKind),
                nameof(CognitiveMemoryRecallContextSectionRecord.AccessLevel),
                nameof(CognitiveMemoryRecallContextSectionRecord.RedactionState)
            ],
            [typeof(CognitiveMemoryRecallSourceRefRecord)] =
            [
                nameof(CognitiveMemoryRecallSourceRefRecord.AccessLevel),
                nameof(CognitiveMemoryRecallSourceRefRecord.RedactionState),
                nameof(CognitiveMemoryRecallSourceRefRecord.ExclusionReasonKind)
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

    private sealed class RecallFixture(
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

    private sealed class RecordingProjectionAdapter(IReadOnlyList<CognitiveMemoryProjectionSearchHit> hits) : ICognitiveMemoryProjectionAdapter
    {
        public CognitiveMemoryProjectionAdapterCapabilities Capabilities { get; } = new(
            "fake-rag",
            SupportsFilters: true,
            SupportsPayloadIndexes: true,
            SupportsDeleteByFilter: true,
            SupportsNamedVectors: false);

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
            => ValueTask.FromResult(new CognitiveMemoryProjectionSearchResult(
                request.ProjectionProfileId,
                hits.Take(request.Page.Take).ToArray(),
                $"fake-rag:search:{hits.Count}"));

        public ValueTask<CognitiveMemoryProjectionDeleteResult> DeleteBySourceAsync(
            CognitiveMemoryProjectionDeleteBySourceRequest request,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new CognitiveMemoryProjectionDeleteResult("fake-rag:delete"));
    }
}

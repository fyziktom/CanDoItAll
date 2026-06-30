using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging.Abstractions;

using CanDoItAll.Tests.Support;
namespace CanDoItAll.Tests.Integration;

public sealed class CognitiveMemoryTemporalReplayPersistenceModelTests
{
    [Fact]
    public async Task TemporalReplayPersistenceModel_IndexesEntitiesAndTypedEnumState()
    {
        await using var fixture = await CreateFixtureAsync();
        var entityTypes = fixture.DbContext.Model.GetEntityTypes().ToList();

        AssertEntityTable<CognitiveMemoryTemporalEpisodeRecord>(entityTypes, "CognitiveMemory_TemporalEpisodes");
        AssertEntityTable<CognitiveMemoryEpisodeStepRecord>(entityTypes, "CognitiveMemory_EpisodeSteps");
        AssertEntityTable<CognitiveMemoryTemporalEpisodeLinkRecord>(entityTypes, "CognitiveMemory_TemporalEpisodeLinks");
        AssertEntityTable<CognitiveMemoryEpisodeStepEvidenceRecord>(entityTypes, "CognitiveMemory_EpisodeStepEvidence");
        AssertEntityTable<CognitiveMemoryEpisodeCausalLinkRecord>(entityTypes, "CognitiveMemory_EpisodeCausalLinks");
        AssertEntityTable<CognitiveMemoryReplayJobRecord>(entityTypes, "CognitiveMemory_ReplayJobs");
        AssertEntityTable<CognitiveMemoryReplayJobTargetRecord>(entityTypes, "CognitiveMemory_ReplayJobTargets");
        AssertEntityTable<CognitiveMemoryReplayJobSignalRecord>(entityTypes, "CognitiveMemory_ReplayJobSignals");
        AssertEntityTable<CognitiveMemoryReplayJobPredictionErrorRecord>(entityTypes, "CognitiveMemory_ReplayJobPredictionErrors");
        AssertEntityTable<CognitiveMemoryReplayOutputRecord>(entityTypes, "CognitiveMemory_ReplayOutputs");
        AssertEntityTable<CognitiveMemoryReplayWorkerResultRecord>(entityTypes, "CognitiveMemory_ReplayWorkerResults");

        foreach (var expectation in CognitiveMemoryEfGuardrails.TemporalReplayIndexExpectations)
        {
            var entityType = Assert.Single(entityTypes, entityType => entityType.ClrType == expectation.EntityType);
            Assert.True(
                CognitiveMemoryEfGuardrails.HasExpectedIndex(entityType, expectation),
                $"Missing expected temporal replay index on {expectation.EntityType.Name}: {string.Join(", ", expectation.PropertyNames)}.");
        }

        var registry = new CognitiveMemoryScoreSpaceRegistry();
        var definition = await registry.GetDefinitionAsync(
            CognitiveMemoryScoreSpaceKind.ReplayPriority,
            CognitiveMemoryScoreSpaceRegistry.CurrentSchemaVersion);
        Assert.Contains(definition.Dimensions, dimension => dimension.Kind == CognitiveMemoryScoreDimensionKind.PredictionErrorMagnitude && dimension.Required);
        Assert.Contains(definition.Dimensions, dimension => dimension.Kind == CognitiveMemoryScoreDimensionKind.RiskImpact && dimension.Required);
        Assert.Contains(definition.Dimensions, dimension => dimension.Kind == CognitiveMemoryScoreDimensionKind.WrongScopePressure && !dimension.Required);
        Assert.Contains(definition.Dimensions, dimension => dimension.Kind == CognitiveMemoryScoreDimensionKind.ContradictionPressure && !dimension.Required);
        AssertEnumStateFieldsAreNotPersistedAsStrings(entityTypes);
    }

    [Fact]
    public async Task TemporalReplayServices_PersistEpisodeReplayOutputAndWorkerRejectionWithoutTruthMutation()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var evidenceAnchorId = await SeedEvidenceAnchorAsync(fixture, projectId);
        var service = CreateService(fixture);

        var episode = await service.CreateEpisodeAsync(new CognitiveMemoryTemporalEpisodeCreateRequest(
            projectId,
            CognitiveMemoryTemporalEpisodeKind.Debugging,
            "Debug wrong Docker context recall.",
            "Replay catches wrong-scope context.",
            "Replay job was queued for context drill.",
            fixture.Clock.GetUtcNow()));
        var step = await service.AppendStepAsync(new CognitiveMemoryEpisodeStepAppendRequest(
            new CognitiveMemoryTemporalEpisodeId(episode.Id),
            null,
            fixture.Clock.GetUtcNow(),
            CognitiveMemoryActorKind.Agent,
            "agent:test",
            CognitiveMemoryEpisodeStepActionKind.ErrorObserved,
            "Observed production/test Docker context confusion.",
            OutputEvidenceAnchorIds: [evidenceAnchorId]));
        await service.AddCausalLinkAsync(new CognitiveMemoryEpisodeCausalLinkRequest(
            new CognitiveMemoryTemporalEpisodeId(episode.Id),
            CognitiveMemoryEpisodeCausalLinkKind.StepCausedStep,
            new CognitiveMemoryEpisodeStepId(step.Id),
            null,
            "Error observation caused replay scheduling.",
            evidenceAnchorId));
        var job = await service.EnqueueAsync(new CognitiveMemoryReplayEnqueueRequest(
            projectId,
            CognitiveMemoryReplayJobKind.ContextBoundaryDrill,
            "Replay wrong-scope Docker context boundary.",
            Policy(projectId),
            Targets:
            [
                new CognitiveMemoryReplayJobTargetDraft(
                    CognitiveMemoryReplayJobTargetKind.TemporalEpisode,
                    episode.Id,
                    string.Empty,
                    "episode-input",
                    "Wrong-scope debugging episode.")
            ]));
        var output = await service.RecordOutputAsync(new CognitiveMemoryReplayOutputRequest(
            new CognitiveMemoryReplayJobId(job.Id),
            CognitiveMemoryReplayOutputKind.ProjectionInvalidationRequest,
            "Projection invalidation candidate from replay.",
            """{"projection":"candidate"}"""));
        var validation = await service.SubmitWorkerResultAsync(new CognitiveMemoryReplayWorkerResultSubmission(
            new CognitiveMemoryReplayJobId(job.Id),
            "worker:test",
            "wrong-input-hash",
            "output-hash",
            job.AlgorithmVersion,
            job.SourceScopeKey,
            job.PolicyProfileId,
            job.ExpectedOutputSchema,
            "storage://worker-result"));

        fixture.DbContext.ChangeTracker.Clear();
        var persistedEpisode = await fixture.DbContext.Set<CognitiveMemoryTemporalEpisodeRecord>().SingleAsync();
        var persistedJob = await fixture.DbContext.Set<CognitiveMemoryReplayJobRecord>().SingleAsync();
        var persistedOutput = await fixture.DbContext.Set<CognitiveMemoryReplayOutputRecord>().SingleAsync();
        var workerResult = await fixture.DbContext.Set<CognitiveMemoryReplayWorkerResultRecord>().SingleAsync();
        var scoreComponents = await fixture.DbContext.Set<CognitiveMemoryScoreComponentRecord>()
            .Where(component => component.SpaceKind == CognitiveMemoryScoreSpaceKind.ReplayPriority)
            .ToListAsync();

        Assert.Equal(1, persistedEpisode.StepCount);
        Assert.Equal(CognitiveMemoryReplayJobKind.ContextBoundaryDrill, persistedJob.JobKind);
        Assert.Equal(CognitiveMemoryReplayOutputStatus.NeedsReview, persistedOutput.Status);
        Assert.Equal(output.Id, persistedOutput.Id);
        Assert.False(validation.Accepted);
        Assert.Equal(CognitiveMemoryReplayWorkerResultStatus.Rejected, workerResult.Status);
        Assert.Equal("InputHashMismatch", workerResult.RejectionReason);
        Assert.Contains(scoreComponents, component => component.DimensionKind == CognitiveMemoryScoreDimensionKind.WrongScopePressure);
        Assert.Equal(0, await fixture.DbContext.Set<CognitiveMemoryMutationCommandRecord>().CountAsync());
        Assert.Equal(0, await fixture.DbContext.Set<CognitiveMemoryRecord>().CountAsync());
    }

    private static CognitiveMemoryTemporalReplayService CreateService(TemporalReplayFixture fixture)
        => new(
            fixture.Factory,
            new CognitiveMemoryScoreGeometryDriver(new CognitiveMemoryScoreSpaceRegistry()),
            fixture.Clock,
            NullLogger<CognitiveMemoryTemporalReplayService>.Instance);

    private static CognitiveMemoryPolicyContext Policy(Guid projectId)
        => new(
            projectId,
            "agent:test",
            CognitiveMemoryAccessLevel.Project,
            new CognitiveMemoryPolicyProfileId("policy:test"),
            CognitiveMemoryRiskLevel.Low,
            AllowRestrictedContent: false);

    private static async Task<CognitiveMemoryEvidenceAnchorId> SeedEvidenceAnchorAsync(
        TemporalReplayFixture fixture,
        Guid projectId)
    {
        var anchor = new CognitiveMemoryEvidenceAnchorRecord
        {
            ProjectId = projectId,
            AnchorKind = CognitiveMemoryEvidenceAnchorKind.TextSpan,
            SourceSystem = "integration-test",
            Locator = "/integration/replay",
            StructuredPath = "$.replay.context",
            TextStart = 0,
            TextEnd = 12,
            QuoteHash = CognitiveMemoryHash.FromUtf8("replay quote").Value,
            TrustLevel = CognitiveMemorySourceTrustLevel.RuntimeSource,
            RedactionState = CognitiveMemoryRedactionState.Safe,
            SourceHashAlgorithm = CognitiveMemoryHashAlgorithm.Sha256,
            SourceHash = CognitiveMemoryHash.FromUtf8("source hash").Value,
            ObservedAtUtc = fixture.Clock.GetUtcNow(),
            CreatedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        };
        fixture.DbContext.Add(anchor);
        await fixture.DbContext.SaveChangesAsync();
        return new CognitiveMemoryEvidenceAnchorId(anchor.Id);
    }

    private static async Task<TemporalReplayFixture> CreateFixtureAsync()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        var database = PostgresTestDatabaseLease.Create("cognitivememorytemporalreplaypersistencemodeltests");

        var options = database.CreateAppDbContextOptions();
        var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        return new TemporalReplayFixture(database, new TestDbContextFactory(options), dbContext, new FixedClock());
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
            [typeof(CognitiveMemoryTemporalEpisodeRecord)] =
            [
                nameof(CognitiveMemoryTemporalEpisodeRecord.EpisodeKind)
            ],
            [typeof(CognitiveMemoryEpisodeStepRecord)] =
            [
                nameof(CognitiveMemoryEpisodeStepRecord.ActorKind),
                nameof(CognitiveMemoryEpisodeStepRecord.ActionKind)
            ],
            [typeof(CognitiveMemoryTemporalEpisodeLinkRecord)] =
            [
                nameof(CognitiveMemoryTemporalEpisodeLinkRecord.LinkKind)
            ],
            [typeof(CognitiveMemoryReplayJobRecord)] =
            [
                nameof(CognitiveMemoryReplayJobRecord.JobKind),
                nameof(CognitiveMemoryReplayJobRecord.State),
                nameof(CognitiveMemoryReplayJobRecord.PriorityBucket)
            ],
            [typeof(CognitiveMemoryReplayOutputRecord)] =
            [
                nameof(CognitiveMemoryReplayOutputRecord.OutputKind),
                nameof(CognitiveMemoryReplayOutputRecord.Status)
            ],
            [typeof(CognitiveMemoryReplayWorkerResultRecord)] =
            [
                nameof(CognitiveMemoryReplayWorkerResultRecord.Status)
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

    private sealed class TemporalReplayFixture(
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

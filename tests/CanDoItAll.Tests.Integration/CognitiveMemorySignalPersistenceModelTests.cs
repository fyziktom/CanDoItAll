using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.SharedKernel;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CanDoItAll.Tests.Integration;

public sealed class CognitiveMemorySignalPersistenceModelTests
{
    [Fact]
    public async Task SignalPersistenceModel_IndexesPredictionErrorsSignalsAndTypedEnumState()
    {
        await using var fixture = await CreateFixtureAsync();
        var entityTypes = fixture.DbContext.Model.GetEntityTypes().ToList();

        AssertEntityTable<CognitiveMemoryPredictionExpectationRecord>(entityTypes, "CognitiveMemory_PredictionExpectations");
        AssertEntityTable<CognitiveMemoryPredictionExpectationEvidenceAnchorRecord>(entityTypes, "CognitiveMemory_PredictionExpectationEvidenceAnchors");
        AssertEntityTable<CognitiveMemoryPredictionErrorRecord>(entityTypes, "CognitiveMemory_PredictionErrors");
        AssertEntityTable<CognitiveMemoryPredictionErrorEvidenceAnchorRecord>(entityTypes, "CognitiveMemory_PredictionErrorEvidenceAnchors");
        AssertEntityTable<CognitiveMemoryPredictionErrorSignalRecord>(entityTypes, "CognitiveMemory_PredictionErrorSignals");
        AssertEntityTable<CognitiveMemorySignalRecord>(entityTypes, "CognitiveMemory_Signals");
        AssertEntityTable<CognitiveMemorySignalEvidenceAnchorRecord>(entityTypes, "CognitiveMemory_SignalEvidenceAnchors");
        AssertEntityTable<CognitiveMemorySignalConsumerPolicyRecord>(entityTypes, "CognitiveMemory_SignalConsumerPolicies");

        foreach (var expectation in CognitiveMemoryEfGuardrails.SignalIndexExpectations)
        {
            var entityType = Assert.Single(entityTypes, entityType => entityType.ClrType == expectation.EntityType);
            Assert.True(
                CognitiveMemoryEfGuardrails.HasExpectedIndex(entityType, expectation),
                $"Missing expected signal index on {expectation.EntityType.Name}: {string.Join(", ", expectation.PropertyNames)}.");
        }

        var registry = new CognitiveMemoryScoreSpaceRegistry();
        var predictionErrorDefinition = await registry.GetDefinitionAsync(
            CognitiveMemoryScoreSpaceKind.PredictionErrorSeverity,
            CognitiveMemoryScoreSpaceRegistry.CurrentSchemaVersion);
        var salienceDefinition = await registry.GetDefinitionAsync(
            CognitiveMemoryScoreSpaceKind.SalienceSignal,
            CognitiveMemoryScoreSpaceRegistry.CurrentSchemaVersion);

        Assert.Contains(predictionErrorDefinition.Dimensions, dimension => dimension.Kind == CognitiveMemoryScoreDimensionKind.PredictionErrorMagnitude && dimension.Required);
        Assert.Contains(salienceDefinition.Dimensions, dimension => dimension.Kind == CognitiveMemoryScoreDimensionKind.ContextSeparation);
        AssertEnumStateFieldsAreNotPersistedAsStrings(entityTypes);
    }

    [Fact]
    public async Task SignalLedger_PersistsWorkspaceAttentionTraceLinksWithoutCreatingSourceTruth()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var evidenceAnchorId = await SeedEvidenceAnchorAsync(fixture, projectId);
        var workspaceService = new CognitiveMemoryWorkspaceService(fixture.Factory, fixture.Clock);
        var workspace = await workspaceService.GetOrCreateAsync(new CognitiveMemoryWorkspaceOpenRequest(
            new CognitiveMemoryWorkspaceScope(projectId, CognitiveMemoryWorkspaceFrameKind.ProbeSession, probeSessionId: Guid.Parse("10000000-0000-0000-0000-000000000001")),
            new CognitiveMemoryWorkspaceContextBudget(2048, 4, 8),
            ExpiresAtUtc: fixture.Clock.GetUtcNow().AddHours(1)));
        var router = new CognitiveMemoryAttentionRouter(
            fixture.Factory,
            new CognitiveMemoryScoreGeometryDriver(new CognitiveMemoryScoreSpaceRegistry()),
            fixture.Clock);
        var attention = await router.RouteAsync(new CognitiveMemoryAttentionRoutingRequest(
            projectId,
            new CognitiveMemoryWorkspaceFrameId(workspace.Frame.Id),
            "Probe Docker context boundary before answering.",
            new CognitiveMemoryAttentionSignalSet(0.5, 0.4, RiskImpact: 0.3, AvailableWorkspaceEvidence: 0.2, MissingKnowledgePressure: 0.8, ExpectedValue: 0.8)));
        var ledger = new CognitiveMemorySignalLedger(
            fixture.Factory,
            new CognitiveMemoryScoreSpaceRegistry(),
            new CognitiveMemoryScoreGeometryDriver(new CognitiveMemoryScoreSpaceRegistry()),
            fixture.Clock);

        var error = await ledger.ObserveAsync(new CognitiveMemoryPredictionErrorObservationRequest(
            projectId,
            CognitiveMemoryPredictionErrorKind.WrongScope,
            CognitiveMemoryActorKind.Agent,
            "agent:test",
            Policy(projectId),
            "Probe found that production and test Docker contexts were substituted.",
            "Production context evidence.",
            "Test simulation evidence.",
            "Context boundary was treated as semantic similarity.",
            CognitiveMemoryPredictionSuggestedActionKind.Probe,
            "Keep probing and replaying context-boundary cases.",
            [
                new CognitiveMemorySignalComponentDraft(CognitiveMemoryScoreDimensionKind.PredictionErrorMagnitude, 0.88),
                new CognitiveMemorySignalComponentDraft(CognitiveMemoryScoreDimensionKind.ContextSeparation, 0.91),
                new CognitiveMemorySignalComponentDraft(CognitiveMemoryScoreDimensionKind.CalibrationRisk, 0.8)
            ],
            [evidenceAnchorId],
            WorkspaceFrameId: new CognitiveMemoryWorkspaceFrameId(workspace.Frame.Id),
            AttentionDecisionId: attention.Id,
            SignalsToPublish:
            [
                new CognitiveMemorySignalPublicationDraft(
                    CognitiveMemorySignalKind.CalibrationRisk,
                    CognitiveMemorySignalSourceKind.ProbeFeedback,
                    "Wrong-scope probe raises calibration and replay pressure.",
                    [
                        new CognitiveMemorySignalComponentDraft(CognitiveMemoryScoreDimensionKind.OutcomeMismatch, 0.88),
                        new CognitiveMemorySignalComponentDraft(CognitiveMemoryScoreDimensionKind.ContextSeparation, 0.91),
                        new CognitiveMemorySignalComponentDraft(CognitiveMemoryScoreDimensionKind.CalibrationRisk, 0.8)
                    ],
                    [
                        CognitiveMemorySignalConsumerKind.ReplayScheduler,
                        CognitiveMemorySignalConsumerKind.EpistemicDrive,
                        CognitiveMemorySignalConsumerKind.AnswerGate
                    ])
            ]));

        var query = await ledger.QueryAsync(new CognitiveMemorySignalQuery(
            projectId,
            Policy(projectId),
            new CognitiveMemoryPageRequest(take: 10),
            ConsumerKinds: [CognitiveMemorySignalConsumerKind.ReplayScheduler]));

        var persistedError = await fixture.DbContext.Set<CognitiveMemoryPredictionErrorRecord>()
            .SingleAsync(item => item.Id == error.PredictionError.Id);
        var persistedSignal = Assert.Single(query.Signals);
        var signalTrace = await fixture.DbContext.Set<CognitiveMemoryScoreEvaluationTraceRecord>()
            .SingleAsync(item => item.Id == persistedSignal.SignalScoreEvaluationTraceId);
        var consumerPolicies = await fixture.DbContext.Set<CognitiveMemorySignalConsumerPolicyRecord>()
            .Where(item => item.CognitiveSignalId == persistedSignal.Id)
            .ToListAsync();

        Assert.Equal(workspace.Frame.Id, persistedError.WorkspaceFrameId);
        Assert.Equal(attention.Id.Value, persistedError.AttentionDecisionId);
        Assert.Equal(CognitiveMemoryScoreSpaceKind.SalienceSignal, signalTrace.SpaceKind);
        Assert.All(consumerPolicies, policy => Assert.False(policy.CanCreateTruthDirectly));
        Assert.Equal(0, await fixture.DbContext.Set<CognitiveMemoryMutationCommandRecord>().CountAsync());
        Assert.Equal(0, await fixture.DbContext.Set<CognitiveMemorySourceItemRecord>().CountAsync());
    }

    private static async Task<CognitiveMemoryEvidenceAnchorId> SeedEvidenceAnchorAsync(
        SignalFixture fixture,
        Guid projectId)
    {
        var anchor = new CognitiveMemoryEvidenceAnchorRecord
        {
            ProjectId = projectId,
            AnchorKind = CognitiveMemoryEvidenceAnchorKind.TextSpan,
            SourceSystem = "integration-test",
            Locator = "/integration/docker",
            StructuredPath = "$.docker.context",
            TextStart = 0,
            TextEnd = 42,
            QuoteHash = CognitiveMemoryHash.FromUtf8("docker integration quote").Value,
            TrustLevel = CognitiveMemorySourceTrustLevel.RuntimeSource,
            RedactionState = CognitiveMemoryRedactionState.Safe,
            SourceHashAlgorithm = CognitiveMemoryHashAlgorithm.Sha256,
            SourceHash = CognitiveMemoryHash.FromUtf8("integration source hash").Value,
            ObservedAtUtc = fixture.Clock.GetUtcNow(),
            CreatedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        };
        fixture.DbContext.Add(anchor);
        await fixture.DbContext.SaveChangesAsync();
        return new CognitiveMemoryEvidenceAnchorId(anchor.Id);
    }

    private static CognitiveMemoryPolicyContext Policy(Guid projectId)
        => new(
            projectId,
            "agent:test",
            CognitiveMemoryAccessLevel.Project,
            new CognitiveMemoryPolicyProfileId("policy:test"),
            CognitiveMemoryRiskLevel.Low,
            AllowRestrictedContent: false);

    private static async Task<SignalFixture> CreateFixtureAsync()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        return new SignalFixture(connection, new TestDbContextFactory(options), dbContext, new FixedClock());
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
            [typeof(CognitiveMemoryPredictionExpectationRecord)] =
            [
                nameof(CognitiveMemoryPredictionExpectationRecord.ExpectationKind),
                nameof(CognitiveMemoryPredictionExpectationRecord.ActorKind),
                nameof(CognitiveMemoryPredictionExpectationRecord.ExpectedSourceSufficiency)
            ],
            [typeof(CognitiveMemoryPredictionErrorRecord)] =
            [
                nameof(CognitiveMemoryPredictionErrorRecord.ErrorKind),
                nameof(CognitiveMemoryPredictionErrorRecord.ActorKind),
                nameof(CognitiveMemoryPredictionErrorRecord.SeverityBucket),
                nameof(CognitiveMemoryPredictionErrorRecord.SuggestedActionKind)
            ],
            [typeof(CognitiveMemorySignalRecord)] =
            [
                nameof(CognitiveMemorySignalRecord.SignalKind),
                nameof(CognitiveMemorySignalRecord.SourceKind),
                nameof(CognitiveMemorySignalRecord.ActorKind),
                nameof(CognitiveMemorySignalRecord.AccessLevel),
                nameof(CognitiveMemorySignalRecord.RedactionState),
                nameof(CognitiveMemorySignalRecord.RiskLevel)
            ],
            [typeof(CognitiveMemorySignalConsumerPolicyRecord)] =
            [
                nameof(CognitiveMemorySignalConsumerPolicyRecord.ConsumerKind),
                nameof(CognitiveMemorySignalConsumerPolicyRecord.MaximumAccessLevel)
            ]
        };

        foreach (var (entityClrType, propertyNames) in stateProperties)
        {
            var entityType = Assert.Single(entityTypes, entityType => entityType.ClrType == entityClrType);
            foreach (var propertyName in propertyNames)
            {
                var property = Assert.IsAssignableFrom<IProperty>(entityType.FindProperty(propertyName));
                Assert.True(property.ClrType.IsEnum, $"{entityClrType.Name}.{propertyName} should remain a typed enum.");
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

    private sealed class SignalFixture(
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

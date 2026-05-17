using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit;

public sealed class CognitiveMemorySignalLedgerTests
{
    [Fact]
    public async Task PublishAsync_PersistsDimensionalSignalConsumersAndTraceWithoutCreatingTruth()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var evidenceAnchorId = await SeedEvidenceAnchorAsync(fixture, projectId);
        var ledger = CreateLedger(fixture);

        var result = await ledger.PublishAsync(new CognitiveMemorySignalPublicationRequest(
            projectId,
            CognitiveMemorySignalKind.Surprise,
            CognitiveMemorySignalSourceKind.ProbeFeedback,
            CognitiveMemoryActorKind.Agent,
            "agent:test",
            Policy(projectId),
            "Docker production answer used the test simulation context.",
            [
                new CognitiveMemorySignalComponentDraft(CognitiveMemoryScoreDimensionKind.OutcomeMismatch, 0.9),
                new CognitiveMemorySignalComponentDraft(CognitiveMemoryScoreDimensionKind.ContextSeparation, 0.95),
                new CognitiveMemorySignalComponentDraft(CognitiveMemoryScoreDimensionKind.CalibrationRisk, 0.7)
            ],
            [
                CognitiveMemorySignalConsumerKind.AttentionRouter,
                CognitiveMemorySignalConsumerKind.ReplayScheduler,
                CognitiveMemorySignalConsumerKind.EpistemicDrive
            ],
            [evidenceAnchorId],
            RiskLevel: CognitiveMemoryRiskLevel.Medium));

        await using var dbContext = fixture.Factory.CreateDbContext();
        var signal = Assert.Single(await dbContext.Set<CognitiveMemorySignalRecord>().ToListAsync());
        var consumers = await dbContext.Set<CognitiveMemorySignalConsumerPolicyRecord>().ToListAsync();
        var components = await dbContext.Set<CognitiveMemoryScoreComponentRecord>().ToListAsync();

        Assert.Equal(result.Signal.Id, signal.Id);
        Assert.Equal(CognitiveMemorySignalKind.Surprise, signal.SignalKind);
        Assert.Equal(3, signal.ComponentCount);
        Assert.True(signal.MatchedShapeCount > 0);
        Assert.Null(signal.DisplayMagnitudeProjection);
        Assert.Equal(3, consumers.Count);
        Assert.All(consumers, consumer => Assert.False(consumer.CanCreateTruthDirectly));
        Assert.Contains(components, component => component.DimensionKind == CognitiveMemoryScoreDimensionKind.ContextSeparation);
        Assert.Equal(0, await dbContext.Set<CognitiveMemoryMutationCommandRecord>().CountAsync());
        Assert.Equal(0, await dbContext.Set<CognitiveMemorySourceItemRecord>().CountAsync());
    }

    [Fact]
    public async Task ObserveAsync_RecordsExpectationErrorAndPublishedSignalsWithScoreDerivedSeverity()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var evidenceAnchorId = await SeedEvidenceAnchorAsync(fixture, projectId);
        var ledger = CreateLedger(fixture);
        var expectation = await ledger.RecordExpectationAsync(new CognitiveMemoryPredictionExpectationRequest(
            projectId,
            CognitiveMemoryPredictionExpectationKind.ContextBoundary,
            CognitiveMemoryActorKind.Agent,
            "agent:test",
            Policy(projectId),
            "Production Docker deployment should use production evidence.",
            "Answer cites production deployment source, not local simulation source.",
            [evidenceAnchorId],
            ExpectedContextKey: "docker:production",
            ExpectedSourceSufficiency: CognitiveMemoryWorkspaceSourceSufficiency.Sufficient,
            MinimumExpectedConfidence: 0.7,
            MaximumExpectedConfidence: 0.95));

        var result = await ledger.ObserveAsync(new CognitiveMemoryPredictionErrorObservationRequest(
            projectId,
            CognitiveMemoryPredictionErrorKind.WrongScope,
            CognitiveMemoryActorKind.Agent,
            "agent:test",
            Policy(projectId),
            "A wrong-scope Docker answer was observed.",
            "Production Docker deployment context.",
            "Local test simulation context.",
            "Semantic similarity overpowered context separation.",
            CognitiveMemoryPredictionSuggestedActionKind.Replay,
            "Create a context-boundary replay drill before recall consumes this topic.",
            [
                new CognitiveMemorySignalComponentDraft(CognitiveMemoryScoreDimensionKind.PredictionErrorMagnitude, 0.92),
                new CognitiveMemorySignalComponentDraft(CognitiveMemoryScoreDimensionKind.ContextSeparation, 0.95),
                new CognitiveMemorySignalComponentDraft(CognitiveMemoryScoreDimensionKind.ReworkCost, 0.7)
            ],
            [evidenceAnchorId],
            new CognitiveMemoryPredictionExpectationId(expectation.Id),
            SignalsToPublish:
            [
                new CognitiveMemorySignalPublicationDraft(
                    CognitiveMemorySignalKind.CalibrationRisk,
                    CognitiveMemorySignalSourceKind.ProbeFeedback,
                    "Wrong-scope answer raises calibration risk for Docker context separation.",
                    [
                        new CognitiveMemorySignalComponentDraft(CognitiveMemoryScoreDimensionKind.OutcomeMismatch, 0.92),
                        new CognitiveMemorySignalComponentDraft(CognitiveMemoryScoreDimensionKind.ContextSeparation, 0.95),
                        new CognitiveMemorySignalComponentDraft(CognitiveMemoryScoreDimensionKind.CalibrationRisk, 0.85)
                    ],
                    [
                        CognitiveMemorySignalConsumerKind.ConfidenceCalibration,
                        CognitiveMemorySignalConsumerKind.SelfRegulationAssessment
                    ],
                    CognitiveMemoryRiskLevel.High)
            ]));

        await using var dbContext = fixture.Factory.CreateDbContext();
        var error = Assert.Single(await dbContext.Set<CognitiveMemoryPredictionErrorRecord>().ToListAsync());
        var signal = Assert.Single(await dbContext.Set<CognitiveMemorySignalRecord>().ToListAsync());
        var link = Assert.Single(await dbContext.Set<CognitiveMemoryPredictionErrorSignalRecord>().ToListAsync());
        var trace = await dbContext.Set<CognitiveMemoryScoreEvaluationTraceRecord>()
            .SingleAsync(item => item.Id == error.SeverityScoreEvaluationTraceId);

        Assert.Equal(CognitiveMemoryPredictionErrorKind.WrongScope, error.ErrorKind);
        Assert.Equal(expectation.Id, error.PredictionExpectationId);
        Assert.True(error.DisplaySeverityProjection > 0);
        Assert.True(error.RequiresReview);
        Assert.Equal(CognitiveMemoryScoreSpaceKind.PredictionErrorSeverity, trace.SpaceKind);
        Assert.Equal(error.Id, link.PredictionErrorId);
        Assert.Equal(signal.Id, link.CognitiveSignalId);
        Assert.True(signal.RequiresReview);
        Assert.Equal(result.PublishedSignals[0].Signal.Id, signal.Id);
    }

    [Fact]
    public async Task PublishAsync_RestrictedSignalRequiresPolicyAccess()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var evidenceAnchorId = await SeedEvidenceAnchorAsync(fixture, projectId);
        var ledger = CreateLedger(fixture);
        var restrictedRequest = new CognitiveMemorySignalPublicationRequest(
            projectId,
            CognitiveMemorySignalKind.Risk,
            CognitiveMemorySignalSourceKind.WorkflowRun,
            CognitiveMemoryActorKind.Agent,
            "agent:test",
            Policy(projectId),
            "Restricted workflow failure signal.",
            [new CognitiveMemorySignalComponentDraft(CognitiveMemoryScoreDimensionKind.RiskImpact, 0.9)],
            [CognitiveMemorySignalConsumerKind.ReviewQueuePriority],
            [evidenceAnchorId],
            AccessLevel: CognitiveMemoryAccessLevel.Restricted);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await ledger.PublishAsync(restrictedRequest));

        Assert.Contains("cannot publish", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PublishAsync_RejectsAnonymousOrScalarOnlySignals()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var evidenceAnchorId = await SeedEvidenceAnchorAsync(fixture, projectId);
        var ledger = CreateLedger(fixture);

        await Assert.ThrowsAsync<ArgumentException>(async () => await ledger.PublishAsync(new CognitiveMemorySignalPublicationRequest(
            projectId,
            CognitiveMemorySignalKind.ReworkCost,
            CognitiveMemorySignalSourceKind.WorkflowRun,
            CognitiveMemoryActorKind.Agent,
            "agent:test",
            Policy(projectId),
            "Scalar-only signal attempt.",
            [],
            [CognitiveMemorySignalConsumerKind.ReplayScheduler],
            [evidenceAnchorId])));
    }

    private static CognitiveMemorySignalLedger CreateLedger(TestFixture fixture)
        => new(
            fixture.Factory,
            new CognitiveMemoryScoreSpaceRegistry(),
            new CognitiveMemoryScoreGeometryDriver(new CognitiveMemoryScoreSpaceRegistry()),
            fixture.Clock);

    private static async Task<CognitiveMemoryEvidenceAnchorId> SeedEvidenceAnchorAsync(
        TestFixture fixture,
        Guid projectId)
    {
        await using var dbContext = fixture.Factory.CreateDbContext();
        var anchor = new CognitiveMemoryEvidenceAnchorRecord
        {
            ProjectId = projectId,
            AnchorKind = CognitiveMemoryEvidenceAnchorKind.TextSpan,
            SourceSystem = "unit-test",
            Locator = "/unit/docker",
            StructuredPath = "$.docker.context",
            TextStart = 0,
            TextEnd = 12,
            QuoteHash = CognitiveMemoryHash.FromUtf8("docker quote").Value,
            TrustLevel = CognitiveMemorySourceTrustLevel.RuntimeSource,
            RedactionState = CognitiveMemoryRedactionState.Safe,
            SourceHashAlgorithm = CognitiveMemoryHashAlgorithm.Sha256,
            SourceHash = CognitiveMemoryHash.FromUtf8("source hash").Value,
            ObservedAtUtc = fixture.Clock.GetUtcNow(),
            CreatedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        };
        dbContext.Add(anchor);
        await dbContext.SaveChangesAsync();
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

    private static TestFixture CreateFixture()
    {
        AppDbContextModelRegistry.ConfigureAssemblies([typeof(CognitiveMemoryModuleAssemblyMarker).Assembly]);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"cognitive-memory-signals-{Guid.NewGuid():N}")
            .Options;
        return new TestFixture(new TestDbContextFactory(options), new FixedClock());
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
    }

    private sealed record TestFixture(
        TestDbContextFactory Factory,
        FixedClock Clock);

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }
}

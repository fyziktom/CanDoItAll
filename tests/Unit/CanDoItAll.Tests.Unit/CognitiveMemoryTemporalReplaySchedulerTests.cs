using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
public sealed class CognitiveMemoryTemporalReplaySchedulerTests
{
    [Fact]
    public async Task AppendStepAsync_PreservesSequenceActorEvidenceAndCausalLinks()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var evidenceAnchorId = await SeedEvidenceAnchorAsync(fixture, projectId);
        var service = CreateService(fixture);

        var episode = await service.CreateEpisodeAsync(new CognitiveMemoryTemporalEpisodeCreateRequest(
            projectId,
            CognitiveMemoryTemporalEpisodeKind.Deployment,
            "Deploy Docker production service.",
            "Production deployment succeeds.",
            "Deployment succeeded after validation.",
            fixture.Clock.GetUtcNow(),
            Links:
            [
                new CognitiveMemoryTemporalEpisodeLinkDraft(
                    CognitiveMemoryTemporalEpisodeLinkKind.Artifact,
                    null,
                    "artifact:deployment-log",
                    "Deployment log artifact.")
            ]));
        var first = await service.AppendStepAsync(new CognitiveMemoryEpisodeStepAppendRequest(
            new CognitiveMemoryTemporalEpisodeId(episode.Id),
            null,
            fixture.Clock.GetUtcNow(),
            CognitiveMemoryActorKind.Agent,
            "agent:deploy",
            CognitiveMemoryEpisodeStepActionKind.ToolCalled,
            "Ran production deployment command.",
            OutputEvidenceAnchorIds: [evidenceAnchorId],
            ToolOrPluginKey: "docker"));
        var second = await service.AppendStepAsync(new CognitiveMemoryEpisodeStepAppendRequest(
            new CognitiveMemoryTemporalEpisodeId(episode.Id),
            null,
            fixture.Clock.GetUtcNow().AddMinutes(1),
            CognitiveMemoryActorKind.WorkflowExecutor,
            "workflow:deploy",
            CognitiveMemoryEpisodeStepActionKind.ValidationRun,
            "Validated deployment health checks.",
            InputEvidenceAnchorIds: [evidenceAnchorId]));

        var causalLink = await service.AddCausalLinkAsync(new CognitiveMemoryEpisodeCausalLinkRequest(
            new CognitiveMemoryTemporalEpisodeId(episode.Id),
            CognitiveMemoryEpisodeCausalLinkKind.StepCausedStep,
            new CognitiveMemoryEpisodeStepId(first.Id),
            new CognitiveMemoryEpisodeStepId(second.Id),
            "Deployment command caused health validation step.",
            evidenceAnchorId));
        var outOfOrder = new CognitiveMemoryEpisodeStepAppendRequest(
            new CognitiveMemoryTemporalEpisodeId(episode.Id),
            4,
            fixture.Clock.GetUtcNow().AddMinutes(2),
            CognitiveMemoryActorKind.Agent,
            "agent:deploy",
            CognitiveMemoryEpisodeStepActionKind.Completed,
            "Out-of-order step.");

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.AppendStepAsync(outOfOrder));

        await using var dbContext = fixture.Factory.CreateDbContext();
        var steps = await dbContext.Set<CognitiveMemoryEpisodeStepRecord>()
            .OrderBy(step => step.SequenceIndex)
            .ToListAsync();
        var evidence = await dbContext.Set<CognitiveMemoryEpisodeStepEvidenceRecord>().ToListAsync();
        var episodeLinks = await dbContext.Set<CognitiveMemoryTemporalEpisodeLinkRecord>().ToListAsync();

        Assert.Equal([1, 2], steps.Select(step => step.SequenceIndex));
        Assert.Equal("agent:deploy", steps[0].ActorId);
        Assert.Equal(CognitiveMemoryActorKind.WorkflowExecutor, steps[1].ActorKind);
        Assert.Contains(evidence, item => item.StepId == first.Id && item.EvidenceRole == CognitiveMemoryEpisodeStepEvidenceRole.Output);
        Assert.Contains(evidence, item => item.StepId == second.Id && item.EvidenceRole == CognitiveMemoryEpisodeStepEvidenceRole.Input);
        Assert.Single(episodeLinks);
        Assert.Equal(first.Id, causalLink.FromStepId);
        Assert.Equal(second.Id, causalLink.ToStepId);
    }

    [Fact]
    public async Task PlanReplayJobsAsync_SchedulesWrongScopeReplayWithScoreTraceAndTypedTargets()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var evidenceAnchorId = await SeedEvidenceAnchorAsync(fixture, projectId);
        var ledger = CreateSignalLedger(fixture);
        var errorResult = await ledger.ObserveAsync(new CognitiveMemoryPredictionErrorObservationRequest(
            projectId,
            CognitiveMemoryPredictionErrorKind.WrongScope,
            CognitiveMemoryActorKind.Agent,
            "agent:test",
            Policy(projectId),
            "Docker production recall used local simulation context.",
            "Production Docker source.",
            "Local simulation source.",
            "Context separation failed.",
            CognitiveMemoryPredictionSuggestedActionKind.Replay,
            "Schedule context-boundary drill.",
            [
                new CognitiveMemorySignalComponentDraft(CognitiveMemoryScoreDimensionKind.PredictionErrorMagnitude, 0.9),
                new CognitiveMemorySignalComponentDraft(CognitiveMemoryScoreDimensionKind.ContextSeparation, 0.9)
            ],
            [evidenceAnchorId]));
        var service = CreateService(fixture);

        var result = await service.PlanReplayJobsAsync(new CognitiveMemoryReplayPlanRequest(
            projectId,
            Policy(projectId),
            new CognitiveMemoryPageRequest(take: 10)));

        await using var dbContext = fixture.Factory.CreateDbContext();
        var job = Assert.Single(result.Jobs);
        var persistedJob = Assert.Single(await dbContext.Set<CognitiveMemoryReplayJobRecord>().ToListAsync());
        var target = Assert.Single(await dbContext.Set<CognitiveMemoryReplayJobTargetRecord>().ToListAsync());
        var errorLink = Assert.Single(await dbContext.Set<CognitiveMemoryReplayJobPredictionErrorRecord>().ToListAsync());
        var components = await dbContext.Set<CognitiveMemoryScoreComponentRecord>()
            .Where(component => component.SpaceKind == CognitiveMemoryScoreSpaceKind.ReplayPriority)
            .ToListAsync();

        Assert.Equal(CognitiveMemoryReplayJobKind.ContextBoundaryDrill, job.JobKind);
        Assert.Equal(CognitiveMemoryReplayJobState.Ready, persistedJob.State);
        Assert.True(persistedJob.QueuePriority > 0);
        Assert.Equal(CognitiveMemoryReplayJobTargetKind.PredictionError, target.TargetKind);
        Assert.Equal(errorResult.PredictionError.Id, target.TargetId);
        Assert.Equal(errorResult.PredictionError.Id, errorLink.PredictionErrorId);
        Assert.Contains(components, component => component.DimensionKind == CognitiveMemoryScoreDimensionKind.WrongScopePressure);
        Assert.Contains(components, component => component.DimensionKind == CognitiveMemoryScoreDimensionKind.PredictionErrorMagnitude);
    }

    [Fact]
    public async Task SubmitWorkerResultAsync_RejectsWrongInputHashWithoutMutatingTruth()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var service = CreateService(fixture);
        var job = await service.EnqueueAsync(new CognitiveMemoryReplayEnqueueRequest(
            projectId,
            CognitiveMemoryReplayJobKind.ReplayProbeRegression,
            "Replay failed Docker context-boundary probe.",
            Policy(projectId),
            Targets:
            [
                new CognitiveMemoryReplayJobTargetDraft(
                    CognitiveMemoryReplayJobTargetKind.ProbeRegression,
                    null,
                    "probe:docker-context-boundary",
                    "expected-input",
                    "Docker context-boundary regression.")
            ]));

        var validation = await service.SubmitWorkerResultAsync(new CognitiveMemoryReplayWorkerResultSubmission(
            new CognitiveMemoryReplayJobId(job.Id),
            "worker:test",
            "wrong-input-hash",
            "output-hash",
            job.AlgorithmVersion,
            job.SourceScopeKey,
            job.PolicyProfileId,
            job.ExpectedOutputSchema,
            "storage://result"));

        await using var dbContext = fixture.Factory.CreateDbContext();
        var workerResult = Assert.Single(await dbContext.Set<CognitiveMemoryReplayWorkerResultRecord>().ToListAsync());
        Assert.False(validation.Accepted);
        Assert.Equal(CognitiveMemoryReplayWorkerResultStatus.Rejected, workerResult.Status);
        Assert.Equal("InputHashMismatch", workerResult.RejectionReason);
        Assert.Equal(0, await dbContext.Set<CognitiveMemoryMutationCommandRecord>().CountAsync());
        Assert.Equal(0, await dbContext.Set<CognitiveMemoryRecord>().CountAsync());
        Assert.Equal(0, await dbContext.Set<CognitiveMemoryReplayOutputRecord>().CountAsync());
    }

    [Fact]
    public async Task RecordOutputAsync_PersistsDraftOrReviewOnly()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var service = CreateService(fixture);
        var job = await service.EnqueueAsync(new CognitiveMemoryReplayEnqueueRequest(
            projectId,
            CognitiveMemoryReplayJobKind.ResolveContradiction,
            "Replay contradiction before review.",
            Policy(projectId),
            Targets:
            [
                new CognitiveMemoryReplayJobTargetDraft(
                    CognitiveMemoryReplayJobTargetKind.Claim,
                    Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    string.Empty,
                    "claim-hash",
                    "Potentially contradicted claim.")
            ]));

        var output = await service.RecordOutputAsync(new CognitiveMemoryReplayOutputRequest(
            new CognitiveMemoryReplayJobId(job.Id),
            CognitiveMemoryReplayOutputKind.DraftClaimUpdate,
            "Draft replay result for review.",
            """{"kind":"draft"}"""));

        await using var dbContext = fixture.Factory.CreateDbContext();
        Assert.Equal(CognitiveMemoryReplayOutputStatus.NeedsReview, output.Status);
        Assert.Equal(1, await dbContext.Set<CognitiveMemoryReplayOutputRecord>().CountAsync());
        Assert.Equal(0, await dbContext.Set<CognitiveMemoryMutationCommandRecord>().CountAsync());
        Assert.Equal(0, await dbContext.Set<CognitiveMemoryRecord>().CountAsync());
    }

    private static CognitiveMemoryTemporalReplayService CreateService(TestFixture fixture)
        => new(
            fixture.Factory,
            new CognitiveMemoryScoreGeometryDriver(new CognitiveMemoryScoreSpaceRegistry()),
            fixture.Clock,
            NullLogger<CognitiveMemoryTemporalReplayService>.Instance);

    private static CognitiveMemorySignalLedger CreateSignalLedger(TestFixture fixture)
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
            Locator = "/unit/replay",
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
        var options = AppDbContextTestOptionsBuilder.Create()
            .UseInMemoryDatabase($"cognitive-memory-temporal-replay-{Guid.NewGuid():N}")
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

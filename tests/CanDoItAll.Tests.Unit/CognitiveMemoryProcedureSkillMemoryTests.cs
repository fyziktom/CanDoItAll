using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class CognitiveMemoryProcedureSkillMemoryTests
{
    [Fact]
    public async Task ProposeSkillAsync_PersistsStepsFailureModesEvidenceAndMaturityTrace()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var evidenceAnchorId = await SeedEvidenceAnchorAsync(fixture, projectId);
        var episodeId = await SeedEpisodeAsync(fixture, projectId);
        var service = CreateService(fixture);

        var skill = await service.ProposeSkillAsync(CreateSkillRequest(
            projectId,
            evidenceAnchorId,
            episodeId,
            initialMaturity: CognitiveMemoryProcedureSkillMaturity.Observed));

        await using var dbContext = fixture.Factory.CreateDbContext();
        var steps = await dbContext.Set<CognitiveMemoryProcedureStepRecord>()
            .Where(step => step.ProcedureSkillId == skill.Id)
            .OrderBy(step => step.SequenceIndex)
            .ToListAsync();
        var failureMode = Assert.Single(await dbContext.Set<CognitiveMemoryProcedureFailureModeRecord>().ToListAsync());
        var failureEpisode = Assert.Single(await dbContext.Set<CognitiveMemoryProcedureFailureModeEpisodeRecord>().ToListAsync());
        var validationEvidence = Assert.Single(await dbContext.Set<CognitiveMemoryProcedureValidationEvidenceRecord>().ToListAsync());
        var scoreComponents = await dbContext.Set<CognitiveMemoryScoreComponentRecord>()
            .Where(component => component.OwnerKind == CognitiveMemoryScoreOwnerKind.ProcedureSkill && component.OwnerId == skill.Id)
            .ToListAsync();

        Assert.Equal(CognitiveMemoryProcedureSkillMaturity.Observed, skill.Maturity);
        Assert.Equal([1, 2], steps.Select(step => step.SequenceIndex));
        Assert.Equal("docker-build", steps[0].ToolBindingKey);
        Assert.Equal("health-check", failureMode.FailureKey);
        Assert.Equal(episodeId.Value, failureEpisode.EpisodeId);
        Assert.Equal(CognitiveMemoryProcedureValidationEvidenceRole.RuntimeObservation, validationEvidence.EvidenceRole);
        Assert.Contains(scoreComponents, component => component.DimensionKind == CognitiveMemoryScoreDimensionKind.ProcedureMaturity);
        Assert.Contains(scoreComponents, component => component.DimensionKind == CognitiveMemoryScoreDimensionKind.EvidenceStrength);
    }

    [Fact]
    public async Task RequestAutomationBindingAsync_RejectsImmatureAndRequiresReviewForHighRisk()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var evidenceAnchorId = await SeedEvidenceAnchorAsync(fixture, projectId);
        var service = CreateService(fixture);
        var draftSkill = await service.ProposeSkillAsync(CreateSkillRequest(projectId, evidenceAnchorId, null));

        var rejected = await service.RequestAutomationBindingAsync(new CognitiveMemoryProcedureAutomationBindingRequest(
            new CognitiveMemoryProcedureSkillId(draftSkill.Id),
            CognitiveMemoryProcedureAutomationBindingKind.WorkflowTemplate,
            "workflow:docker-deploy",
            Policy(projectId)));

        var highRiskSkill = await service.ProposeSkillAsync(CreateSkillRequest(
            projectId,
            evidenceAnchorId,
            null,
            initialMaturity: CognitiveMemoryProcedureSkillMaturity.Automatable,
            riskLevel: CognitiveMemoryRiskLevel.High,
            validationState: CognitiveMemoryValidationState.Approved));
        var needsReview = await service.RequestAutomationBindingAsync(new CognitiveMemoryProcedureAutomationBindingRequest(
            new CognitiveMemoryProcedureSkillId(highRiskSkill.Id),
            CognitiveMemoryProcedureAutomationBindingKind.MafProcedureGuidance,
            "maf:docker-deploy",
            Policy(projectId)));

        Assert.Equal(CognitiveMemoryProcedureAutomationBindingState.Rejected, rejected.State);
        Assert.Equal("ImmatureProcedure", rejected.RejectionCode);
        Assert.Equal(CognitiveMemoryProcedureAutomationBindingState.NeedsReview, needsReview.State);
        Assert.Equal("HighRiskRequiresReview", needsReview.RejectionCode);
    }

    [Fact]
    public async Task UpdateMaturityAsync_BlocksAutomatableWithoutReviewOrEvidence()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var evidenceAnchorId = await SeedEvidenceAnchorAsync(fixture, projectId);
        var service = CreateService(fixture);
        var skill = await service.ProposeSkillAsync(CreateSkillRequest(
            projectId,
            evidenceAnchorId,
            null,
            validationEvidence: []));

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.UpdateMaturityAsync(new CognitiveMemoryProcedureMaturityUpdateRequest(
            new CognitiveMemoryProcedureSkillId(skill.Id),
            CognitiveMemoryProcedureSkillMaturity.Automatable,
            Policy(projectId),
            AdditionalValidationEvidence: [])));
    }

    [Fact]
    public async Task SimulateAsync_PersistsSpeculativeOutputAndBlocksRestrictedCrossProjectAnalogy()
    {
        var fixture = CreateFixture();
        var sourceProjectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var targetProjectId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var sourceEvidenceAnchorId = await SeedEvidenceAnchorAsync(fixture, sourceProjectId);
        var targetEvidenceAnchorId = await SeedEvidenceAnchorAsync(fixture, targetProjectId);
        var service = CreateService(fixture);
        var publicSkill = await service.ProposeSkillAsync(CreateSkillRequest(
            sourceProjectId,
            sourceEvidenceAnchorId,
            null,
            accessLevel: CognitiveMemoryAccessLevel.Public));
        var restrictedSkill = await service.ProposeSkillAsync(CreateSkillRequest(
            sourceProjectId,
            sourceEvidenceAnchorId,
            null,
            accessLevel: CognitiveMemoryAccessLevel.Restricted,
            title: "Restricted Docker deployment"));

        var simulation = await service.SimulateAsync(new CognitiveMemoryProcedureSimulationRequest(
            targetProjectId,
            CognitiveMemoryProcedureSimulationOutputKind.RiskAnalysis,
            "Compare Docker deployment procedure against another project.",
            Policy(targetProjectId),
            [new CognitiveMemoryProcedureSkillId(publicSkill.Id)],
            [targetEvidenceAnchorId],
            ["Review source project permissions.", "Run regression probe."],
            CognitiveMemoryRiskLevel.Medium,
            AllowCrossProjectAnalogies: true));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.SimulateAsync(new CognitiveMemoryProcedureSimulationRequest(
            targetProjectId,
            CognitiveMemoryProcedureSimulationOutputKind.CandidatePlan,
            "Use restricted procedure as analogy.",
            Policy(targetProjectId),
            [new CognitiveMemoryProcedureSkillId(restrictedSkill.Id)],
            [targetEvidenceAnchorId],
            ["Review restricted source."],
            AllowCrossProjectAnalogies: true)));

        await using var dbContext = fixture.Factory.CreateDbContext();
        var persistedSimulation = await dbContext.Set<CognitiveMemoryProcedureSimulationRecord>().SingleAsync();
        var skillLink = await dbContext.Set<CognitiveMemoryProcedureSimulationSkillRecord>().SingleAsync();
        var riskComponents = await dbContext.Set<CognitiveMemoryScoreComponentRecord>()
            .Where(component => component.OwnerKind == CognitiveMemoryScoreOwnerKind.ProcedureSimulation)
            .ToListAsync();

        Assert.Equal(simulation.Id, persistedSimulation.Id);
        Assert.True(persistedSimulation.IsSpeculative);
        Assert.Equal("speculative-hypothesis", persistedSimulation.SpeculationLabel);
        Assert.Equal(CognitiveMemoryProcedureSimulationStatus.NeedsReview, persistedSimulation.Status);
        Assert.Equal(publicSkill.Id, skillLink.ProcedureSkillId);
        Assert.Contains(riskComponents, component => component.DimensionKind == CognitiveMemoryScoreDimensionKind.ContextSeparation);
        Assert.Equal(0, await dbContext.Set<CognitiveMemoryMutationCommandRecord>().CountAsync());
    }

    private static CognitiveMemoryProcedureSkillProposalRequest CreateSkillRequest(
        Guid projectId,
        CognitiveMemoryEvidenceAnchorId evidenceAnchorId,
        CognitiveMemoryTemporalEpisodeId? episodeId,
        CognitiveMemoryProcedureSkillMaturity initialMaturity = CognitiveMemoryProcedureSkillMaturity.Draft,
        CognitiveMemoryRiskLevel riskLevel = CognitiveMemoryRiskLevel.Low,
        CognitiveMemoryValidationState validationState = CognitiveMemoryValidationState.MachineGenerated,
        CognitiveMemoryAccessLevel accessLevel = CognitiveMemoryAccessLevel.Project,
        IReadOnlyList<CognitiveMemoryProcedureValidationEvidenceDraft>? validationEvidence = null,
        string title = "Docker deployment procedure")
        => new(
            projectId,
            title,
            "Deploy a Docker-backed service with health validation.",
            Policy(projectId),
            Steps:
            [
                new CognitiveMemoryProcedureStepDraft(
                    "build",
                    1,
                    "Build the Docker image.",
                    "Dockerfile and project source.",
                    "Tagged image.",
                    "Image build exits successfully.",
                    "Stop before deploy.",
                    [evidenceAnchorId],
                    ToolBindingKey: "docker-build"),
                new CognitiveMemoryProcedureStepDraft(
                    "verify",
                    2,
                    "Run health checks.",
                    "Running container endpoint.",
                    "Healthy endpoint response.",
                    "Health check returns 200.",
                    "Rollback deployment.",
                    [evidenceAnchorId])
            ],
            FailureModes:
            [
                new CognitiveMemoryProcedureFailureModeDraft(
                    "health-check",
                    "Health endpoint fails after deployment.",
                    "Health check returns non-200.",
                    "Wrong environment or missing secret.",
                    "Inspect environment-specific configuration.",
                    "Roll back to previous image.",
                    RelatedEpisodeIds: episodeId is null ? [] : [episodeId.Value])
            ],
            ValidationEvidence: validationEvidence ?? [new CognitiveMemoryProcedureValidationEvidenceDraft(
                CognitiveMemoryProcedureValidationEvidenceRole.RuntimeObservation,
                evidenceAnchorId,
                "Observed in a successful deployment episode.",
                episodeId)],
            Preconditions: ["Dockerfile exists.", "Target environment is selected."],
            Postconditions: ["Deployment is healthy."],
            RiskLevel: riskLevel,
            InitialMaturity: initialMaturity,
            ValidationState: validationState,
            AccessLevel: accessLevel,
            RequiredToolKeys: ["docker-build"],
            InputSchemaJson: """{"type":"object"}""",
            OutputSchemaJson: """{"type":"object"}""");

    private static CognitiveMemoryProcedureSkillService CreateService(TestFixture fixture)
        => new(
            fixture.Factory,
            new CognitiveMemoryScoreGeometryDriver(new CognitiveMemoryScoreSpaceRegistry()),
            fixture.Clock,
            NullLogger<CognitiveMemoryProcedureSkillService>.Instance);

    private static async Task<CognitiveMemoryEvidenceAnchorId> SeedEvidenceAnchorAsync(TestFixture fixture, Guid projectId)
    {
        await using var dbContext = fixture.Factory.CreateDbContext();
        var anchor = new CognitiveMemoryEvidenceAnchorRecord
        {
            ProjectId = projectId,
            AnchorKind = CognitiveMemoryEvidenceAnchorKind.TextSpan,
            SourceSystem = "unit-test",
            Locator = "/unit/procedure",
            StructuredPath = "$.procedure",
            TextStart = 0,
            TextEnd = 12,
            QuoteHash = CognitiveMemoryHash.FromUtf8("procedure quote").Value,
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

    private static async Task<CognitiveMemoryTemporalEpisodeId> SeedEpisodeAsync(TestFixture fixture, Guid projectId)
    {
        await using var dbContext = fixture.Factory.CreateDbContext();
        var episode = new CognitiveMemoryTemporalEpisodeRecord
        {
            ProjectId = projectId,
            EpisodeKind = CognitiveMemoryTemporalEpisodeKind.Deployment,
            Goal = "Deploy Docker service.",
            ExpectedOutcome = "Deployment succeeds.",
            ActualOutcome = "Deployment succeeded.",
            OutcomeSummary = "Deployment succeeded.",
            StartedAtUtc = fixture.Clock.GetUtcNow(),
            AlgorithmVersion = "unit-test",
            CreatedAtUtc = fixture.Clock.GetUtcNow(),
            UpdatedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        };
        dbContext.Add(episode);
        await dbContext.SaveChangesAsync();
        return new CognitiveMemoryTemporalEpisodeId(episode.Id);
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
            .UseInMemoryDatabase($"cognitive-memory-procedure-{Guid.NewGuid():N}")
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

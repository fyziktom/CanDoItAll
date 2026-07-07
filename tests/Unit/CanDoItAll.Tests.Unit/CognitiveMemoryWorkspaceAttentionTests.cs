using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit;

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
public sealed class CognitiveMemoryWorkspaceAttentionTests
{
    [Fact]
    public async Task WorkspaceService_CreatesEverySupportedScopedFrameKind()
    {
        var fixture = CreateFixture();
        var service = new CognitiveMemoryWorkspaceService(fixture.Factory, fixture.Clock);
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var processRunId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        var scopes = new[]
        {
            new CognitiveMemoryWorkspaceScope(projectId, CognitiveMemoryWorkspaceFrameKind.UserConversation, ownerUserId: "user:lucy"),
            new CognitiveMemoryWorkspaceScope(projectId, CognitiveMemoryWorkspaceFrameKind.AgentRun, ownerAgentId: "agent:architect"),
            new CognitiveMemoryWorkspaceScope(projectId, CognitiveMemoryWorkspaceFrameKind.WorkflowRun, workflowRunId: Guid.Parse("10000000-0000-0000-0000-000000000001")),
            new CognitiveMemoryWorkspaceScope(projectId, CognitiveMemoryWorkspaceFrameKind.ProcessStep, processRunId: processRunId, processStepId: Guid.Parse("10000000-0000-0000-0000-000000000002")),
            new CognitiveMemoryWorkspaceScope(projectId, CognitiveMemoryWorkspaceFrameKind.ProbeSession, probeSessionId: Guid.Parse("10000000-0000-0000-0000-000000000003")),
            new CognitiveMemoryWorkspaceScope(projectId, CognitiveMemoryWorkspaceFrameKind.ReviewSession, reviewSessionId: Guid.Parse("10000000-0000-0000-0000-000000000004")),
            new CognitiveMemoryWorkspaceScope(projectId, CognitiveMemoryWorkspaceFrameKind.LearningTask, learningTaskId: Guid.Parse("10000000-0000-0000-0000-000000000005"))
        };

        foreach (var scope in scopes)
        {
            await service.GetOrCreateAsync(new CognitiveMemoryWorkspaceOpenRequest(
                scope,
                new CognitiveMemoryWorkspaceContextBudget(4096, 8, 16),
                ExpiresAtUtc: fixture.Clock.GetUtcNow().AddHours(1)));
        }

        await using var dbContext = fixture.Factory.CreateDbContext();
        var frameKinds = await dbContext.Set<CognitiveMemoryWorkspaceFrameRecord>()
            .Select(frame => frame.FrameKind)
            .OrderBy(kind => kind)
            .ToListAsync();

        Assert.Equal(7, frameKinds.Count);
        Assert.Contains(CognitiveMemoryWorkspaceFrameKind.UserConversation, frameKinds);
        Assert.Contains(CognitiveMemoryWorkspaceFrameKind.AgentRun, frameKinds);
        Assert.Contains(CognitiveMemoryWorkspaceFrameKind.WorkflowRun, frameKinds);
        Assert.Contains(CognitiveMemoryWorkspaceFrameKind.ProcessStep, frameKinds);
        Assert.Contains(CognitiveMemoryWorkspaceFrameKind.ProbeSession, frameKinds);
        Assert.Contains(CognitiveMemoryWorkspaceFrameKind.ReviewSession, frameKinds);
        Assert.Contains(CognitiveMemoryWorkspaceFrameKind.LearningTask, frameKinds);
    }

    [Fact]
    public async Task WorkspaceService_ExpiresOldFrameBeforeCreatingReplacement()
    {
        var fixture = CreateFixture();
        var service = new CognitiveMemoryWorkspaceService(fixture.Factory, fixture.Clock);
        var scope = new CognitiveMemoryWorkspaceScope(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            CognitiveMemoryWorkspaceFrameKind.UserConversation,
            ownerUserId: "user:lucy");

        var first = await service.GetOrCreateAsync(new CognitiveMemoryWorkspaceOpenRequest(
            scope,
            new CognitiveMemoryWorkspaceContextBudget(100, 2, 2),
            ExpiresAtUtc: fixture.Clock.GetUtcNow().AddMinutes(1)));

        fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddMinutes(2);
        var second = await service.GetOrCreateAsync(new CognitiveMemoryWorkspaceOpenRequest(
            scope,
            new CognitiveMemoryWorkspaceContextBudget(100, 2, 2),
            ExpiresAtUtc: fixture.Clock.GetUtcNow().AddMinutes(1)));

        await using var dbContext = fixture.Factory.CreateDbContext();
        var frames = await dbContext.Set<CognitiveMemoryWorkspaceFrameRecord>()
            .OrderBy(frame => frame.CreatedAtUtc)
            .ToListAsync();

        Assert.NotEqual(first.Frame.Id, second.Frame.Id);
        Assert.Equal(2, frames.Count);
        Assert.Equal(CognitiveMemoryWorkspaceFrameStatus.Expired, frames[0].Status);
        Assert.Equal(CognitiveMemoryWorkspaceFrameStatus.Active, frames[1].Status);
    }

    [Fact]
    public async Task WorkspaceService_UpdateFocus_PersistsStructuredReasonsAndBudgetInhibitionWithoutSourceTruth()
    {
        var fixture = CreateFixture();
        var service = new CognitiveMemoryWorkspaceService(fixture.Factory, fixture.Clock);
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var workspace = await service.GetOrCreateAsync(new CognitiveMemoryWorkspaceOpenRequest(
            new CognitiveMemoryWorkspaceScope(projectId, CognitiveMemoryWorkspaceFrameKind.ProbeSession, probeSessionId: Guid.Parse("10000000-0000-0000-0000-000000000001")),
            new CognitiveMemoryWorkspaceContextBudget(10, 2, 2),
            ExpiresAtUtc: fixture.Clock.GetUtcNow().AddHours(1),
            GoalStack:
            [
                new CognitiveMemoryWorkspaceGoalDraft(new CognitiveMemoryWorkspaceGoalKey("docker.production"), "Keep production Docker context separate from test simulation.", 0)
            ],
            OpenQuestions:
            [
                new CognitiveMemoryWorkspaceOpenQuestionDraft("Which Docker context is active?", "The request did not say production, CI, local, or test.")
            ]));

        var accepted = new CognitiveMemoryWorkingMemorySlotDraft(
            CognitiveMemoryWorkingMemorySlotKind.ExternalSourcePlaceholder,
            "Production Docker source needed",
            "Use production deployment evidence only.",
            estimatedTokenCount: 5,
            estimatedSectionCount: 1,
            estimatedDetailCount: 1,
            CognitiveMemoryFocusInclusionReasonKind.GoalMatch,
            "The active goal is production deployment.",
            externalPlaceholderKey: new CognitiveMemoryWorkspaceExternalKey("docker.production.source"),
            sourceSufficiency: CognitiveMemoryWorkspaceSourceSufficiency.Weak,
            relationToActiveGoal: "primary");
        var overBudget = new CognitiveMemoryWorkingMemorySlotDraft(
            CognitiveMemoryWorkingMemorySlotKind.ExternalSourcePlaceholder,
            "Local Docker simulation",
            "Related local simulation detail.",
            estimatedTokenCount: 6,
            estimatedSectionCount: 1,
            estimatedDetailCount: 1,
            CognitiveMemoryFocusInclusionReasonKind.RecallSelected,
            "Semantically related but not enough budget remains.",
            externalPlaceholderKey: new CognitiveMemoryWorkspaceExternalKey("docker.local.simulation"),
            sourceSufficiency: CognitiveMemoryWorkspaceSourceSufficiency.Weak,
            relationToActiveGoal: "related");
        var contextBoundary = new CognitiveMemoryInhibitedCandidateDraft(
            CognitiveMemoryWorkingMemorySlotKind.ExternalSourcePlaceholder,
            CognitiveMemoryInhibitionReasonKind.ContextBoundary,
            "Local/test Docker evidence is related but not substitutable for production deployment.",
            externalCandidateKey: new CognitiveMemoryWorkspaceExternalKey("docker.test.simulation"),
            displayRelevanceScore: 0.95,
            displayInhibitionStrength: 1);

        var updated = await service.UpdateAsync(new CognitiveMemoryWorkspaceUpdateRequest(
            new CognitiveMemoryWorkspaceFrameId(workspace.Frame.Id),
            workspace.Goals.Select(goal => new CognitiveMemoryWorkspaceGoalDraft(new CognitiveMemoryWorkspaceGoalKey(goal.GoalKey), goal.Description, goal.Sequence)).ToArray(),
            [accepted, overBudget],
            [contextBoundary],
            workspace.OpenQuestions.Select(question => new CognitiveMemoryWorkspaceOpenQuestionDraft(question.QuestionText, question.Reason)).ToArray()));

        await using var dbContext = fixture.Factory.CreateDbContext();
        Assert.Single(updated.FocusSlots);
        Assert.Equal(2, updated.InhibitedCandidates.Count);
        Assert.Equal(CognitiveMemoryBudgetLimit.TokenCount, updated.BudgetResult.LimitingBudget);
        Assert.Contains(updated.InhibitedCandidates, candidate => candidate.ReasonKind == CognitiveMemoryInhibitionReasonKind.BudgetLimit);
        Assert.Contains(updated.InhibitedCandidates, candidate => candidate.ReasonKind == CognitiveMemoryInhibitionReasonKind.ContextBoundary);
        Assert.Single(updated.Goals);
        Assert.Single(updated.OpenQuestions);
        Assert.Equal(0, await dbContext.Set<CognitiveMemorySourceItemRecord>().CountAsync());
        Assert.Equal(0, await dbContext.Set<CognitiveMemoryMutationCommandRecord>().CountAsync());
    }

    [Fact]
    public async Task AttentionRouter_RoutesAllDecisionShapesAndPersistsTrace()
    {
        var fixture = CreateFixture();
        var workspace = await CreateWorkspaceAsync(fixture);
        var router = new CognitiveMemoryAttentionRouter(
            fixture.Factory,
            new CognitiveMemoryScoreGeometryDriver(new CognitiveMemoryScoreSpaceRegistry()),
            fixture.Clock);

        var cases = new Dictionary<CognitiveMemoryAttentionDecisionKind, CognitiveMemoryAttentionSignalSet>
        {
            [CognitiveMemoryAttentionDecisionKind.AnswerFromWorkspace] = new(0.9, 0.1, RiskImpact: 0.1, AvailableWorkspaceEvidence: 0.9),
            [CognitiveMemoryAttentionDecisionKind.Recall] = new(0.8, 0.1, RiskImpact: 0.1, AvailableWorkspaceEvidence: 0.2),
            [CognitiveMemoryAttentionDecisionKind.AskClarification] = new(0.8, 0.8, RiskImpact: 0.1, AvailableWorkspaceEvidence: 0.2),
            [CognitiveMemoryAttentionDecisionKind.RunSourceAudit] = new(0.3, 0.2, RiskImpact: 0.2, AvailableWorkspaceEvidence: 0.2, MissingKnowledgePressure: 0.2, ExpectedValue: 0.2),
            [CognitiveMemoryAttentionDecisionKind.StartProbe] = new(0.6, 0.2, RiskImpact: 0.1, AvailableWorkspaceEvidence: 0.1, MissingKnowledgePressure: 0.8, ExpectedValue: 0.6),
            [CognitiveMemoryAttentionDecisionKind.CreateReviewItem] = new(0.8, 0.2, RiskImpact: 0.85, AvailableWorkspaceEvidence: 0.8),
            [CognitiveMemoryAttentionDecisionKind.RequestLearningProposal] = new(0.2, 0.2, RiskImpact: 0.3, AvailableWorkspaceEvidence: 0.1, MissingKnowledgePressure: 0.85, ExpectedValue: 0.8),
            [CognitiveMemoryAttentionDecisionKind.RunReplay] = new(0.6, 0.2, RiskImpact: 0.2, AvailableWorkspaceEvidence: 0.6, CalibrationRisk: 0.8, ExpectedValue: 0.7),
            [CognitiveMemoryAttentionDecisionKind.Abstain] = new(0.1, 0.2, RiskImpact: 0.8, AvailableWorkspaceEvidence: 0.1)
        };

        foreach (var (expected, signals) in cases)
        {
            var decision = await router.RouteAsync(new CognitiveMemoryAttentionRoutingRequest(
                workspace.Frame.ProjectId,
                new CognitiveMemoryWorkspaceFrameId(workspace.Frame.Id),
                $"route {expected}",
                signals));

            Assert.Equal(expected, decision.DecisionKind);
            Assert.NotEqual(CognitiveMemoryScoreProjectionBucket.Unknown, decision.RoutingTrace.ScalarProjection?.Bucket);
            Assert.True(decision.RoutingTrace.MatchedShapes.Count > 0);
            Assert.NotEmpty(decision.RequiredNextActions);
        }

        await using var dbContext = fixture.Factory.CreateDbContext();
        Assert.Equal(cases.Count, await dbContext.Set<CognitiveMemoryAttentionDecisionRecord>().CountAsync());
        Assert.Equal(cases.Count, await dbContext.Set<CognitiveMemoryScoreEvaluationTraceRecord>().CountAsync());
        Assert.True(await dbContext.Set<CognitiveMemoryScoreComponentRecord>().AnyAsync(component =>
            component.SpaceKind == CognitiveMemoryScoreSpaceKind.AttentionRouting &&
            component.DimensionKind == CognitiveMemoryScoreDimensionKind.SourceSufficiency));
    }

    [Fact]
    public async Task AttentionRouter_HonorsRequiredOperationAndDoesNotFallbackToWorkspaceAnswer()
    {
        var fixture = CreateFixture();
        var workspace = await CreateWorkspaceAsync(fixture);
        var router = new CognitiveMemoryAttentionRouter(
            fixture.Factory,
            new CognitiveMemoryScoreGeometryDriver(new CognitiveMemoryScoreSpaceRegistry()),
            fixture.Clock);

        var decision = await router.RouteAsync(new CognitiveMemoryAttentionRoutingRequest(
            workspace.Frame.ProjectId,
            new CognitiveMemoryWorkspaceFrameId(workspace.Frame.Id),
            "answerable text that still requires source audit",
            new CognitiveMemoryAttentionSignalSet(0.95, 0.05, RiskImpact: 0.1, AvailableWorkspaceEvidence: 0.95),
            RequiredDecisionKinds: [CognitiveMemoryAttentionDecisionKind.RunSourceAudit]));

        Assert.Equal(CognitiveMemoryAttentionDecisionKind.RunSourceAudit, decision.DecisionKind);
        Assert.Equal(CognitiveMemoryAttentionReasonKind.RequiredOperation, decision.ReasonKind);
        Assert.Contains("required", decision.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AttentionRouter_MissingRequiredDimensionsAbstainsWithStructuredMissingDimensionTrace()
    {
        var fixture = CreateFixture();
        var workspace = await CreateWorkspaceAsync(fixture);
        var router = new CognitiveMemoryAttentionRouter(
            fixture.Factory,
            new CognitiveMemoryScoreGeometryDriver(new CognitiveMemoryScoreSpaceRegistry()),
            fixture.Clock);

        var decision = await router.RouteAsync(new CognitiveMemoryAttentionRoutingRequest(
            workspace.Frame.ProjectId,
            new CognitiveMemoryWorkspaceFrameId(workspace.Frame.Id),
            "missing dimensions",
            new CognitiveMemoryAttentionSignalSet(SourceSufficiency: null, ContextAmbiguity: null, RiskImpact: 0.2)));

        Assert.Equal(CognitiveMemoryAttentionDecisionKind.Abstain, decision.DecisionKind);
        Assert.Equal(CognitiveMemoryAttentionReasonKind.MissingRequiredDimensions, decision.ReasonKind);
        Assert.Contains(decision.RoutingTrace.MissingRequiredDimensions, dimension => dimension.DimensionKind == CognitiveMemoryScoreDimensionKind.SourceSufficiency);
        Assert.Contains(decision.RoutingTrace.MissingRequiredDimensions, dimension => dimension.DimensionKind == CognitiveMemoryScoreDimensionKind.ContextAmbiguity);
        Assert.Contains(decision.RequiredNextActions, action => action == "provide:SourceSufficiency");
    }

    private static async Task<CognitiveMemoryWorkspaceSnapshot> CreateWorkspaceAsync(TestFixture fixture)
    {
        var service = new CognitiveMemoryWorkspaceService(fixture.Factory, fixture.Clock);
        return await service.GetOrCreateAsync(new CognitiveMemoryWorkspaceOpenRequest(
            new CognitiveMemoryWorkspaceScope(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                CognitiveMemoryWorkspaceFrameKind.AgentRun,
                ownerAgentId: "agent:test"),
            new CognitiveMemoryWorkspaceContextBudget(4096, 8, 16),
            ExpiresAtUtc: fixture.Clock.GetUtcNow().AddHours(1)));
    }

    private static TestFixture CreateFixture()
    {
        AppDbContextModelRegistry.ConfigureAssemblies([typeof(CognitiveMemoryModuleAssemblyMarker).Assembly]);
        var options = AppDbContextTestOptionsBuilder.Create()
            .UseInMemoryDatabase($"cognitive-memory-workspace-{Guid.NewGuid():N}")
            .Options;
        return new TestFixture(new TestDbContextFactory(options), new MutableClock(DateTimeOffset.UnixEpoch));
    }

    private sealed record TestFixture(
        TestDbContextFactory Factory,
        MutableClock Clock);

    private sealed class MutableClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public DateTimeOffset GetUtcNow() => UtcNow;
    }

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }
}

using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CanDoItAll.Tests.Unit;

public sealed class CognitiveMemoryAdvancedServicesTests
{
    [Fact]
    public async Task ProbeAsk_PersistsSourceAwareAnswerSummary()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var calibration = new CognitiveMemoryCalibrationHealthService(fixture.Factory, fixture.ScoreDriver, fixture.Clock);
        var service = new CognitiveMemoryProbeService(
            fixture.Factory,
            new FakeRecallOrchestrator(projectId),
            fixture.ScoreDriver,
            calibration,
            fixture.Clock);
        var session = await service.StartAsync(new CognitiveMemoryProbeStartRequest(
            projectId,
            "Docker source probe",
            Policy(projectId)));

        var result = await service.AskAsync(new CognitiveMemoryProbeAskRequest(
            session.Id,
            "What source supports production deployment?",
            CognitiveMemoryRecallIntentKind.Deployment,
            new CognitiveMemoryRecallBudget(10, 1, 5, 5, 5, 4000, 64000)));

        Assert.Contains("Question: What source supports production deployment?", result.Turn.AnswerSummary, StringComparison.Ordinal);
        Assert.Contains("Supported context:", result.Turn.AnswerSummary, StringComparison.Ordinal);
        Assert.Contains("production-runbook.md", result.Turn.AnswerSummary, StringComparison.Ordinal);
        Assert.Contains("Source refs:", result.Turn.AnswerSummary, StringComparison.Ordinal);
        Assert.Contains("[redacted-email]", result.Turn.AnswerSummary, StringComparison.Ordinal);
        Assert.Contains("[redacted-phone]", result.Turn.AnswerSummary, StringComparison.Ordinal);
        Assert.DoesNotContain("lucie@example.test", result.Turn.AnswerSummary, StringComparison.Ordinal);
        Assert.DoesNotContain("+420 732 936 929", result.Turn.AnswerSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProbeAsk_UsesStoredPolicyAndProjectionDefaultsWithAskOverrides()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var recall = new FakeRecallOrchestrator(projectId);
        var calibration = new CognitiveMemoryCalibrationHealthService(fixture.Factory, fixture.ScoreDriver, fixture.Clock);
        var service = new CognitiveMemoryProbeService(
            fixture.Factory,
            recall,
            fixture.ScoreDriver,
            calibration,
            fixture.Clock);
        var session = await service.StartAsync(new CognitiveMemoryProbeStartRequest(
            projectId,
            "Restricted vector probe",
            Policy(
                projectId,
                CognitiveMemoryRiskLevel.High,
                allowRestrictedContent: true,
                accessLevel: CognitiveMemoryAccessLevel.Restricted),
            ProjectionCollectionName: new CognitiveMemoryProjectionCollectionName("session-collection"),
            ProjectionProfileId: new CognitiveMemoryProjectionProfileId("session-projection"),
            EmbeddingProfileId: new CognitiveMemoryEmbeddingProfileId("session-embedding")));

        await service.AskAsync(new CognitiveMemoryProbeAskRequest(
            session.Id,
            "Which restricted source supports the deployment decision?",
            CognitiveMemoryRecallIntentKind.SourceLookup,
            new CognitiveMemoryRecallBudget(10, 1, 5, 5, 5, 4000, 64000),
            Metadata: new Dictionary<string, string> { ["probe"] = "session-defaults" }));
        await service.AskAsync(new CognitiveMemoryProbeAskRequest(
            session.Id,
            "Which override source supports the deployment decision?",
            CognitiveMemoryRecallIntentKind.SourceLookup,
            new CognitiveMemoryRecallBudget(10, 1, 5, 5, 5, 4000, 64000),
            ProjectionCollectionName: new CognitiveMemoryProjectionCollectionName("ask-collection"),
            ProjectionProfileId: new CognitiveMemoryProjectionProfileId("ask-projection"),
            EmbeddingProfileId: new CognitiveMemoryEmbeddingProfileId("ask-embedding")));

        Assert.Equal(2, recall.Requests.Count);
        var first = recall.Requests[0];
        Assert.Equal(CognitiveMemoryAccessLevel.Restricted, first.PolicyContext.AccessLevel);
        Assert.Equal(CognitiveMemoryRiskLevel.High, first.PolicyContext.RiskLevel);
        Assert.True(first.PolicyContext.AllowRestrictedContent);
        Assert.Equal("session-collection", first.ProjectionCollectionName?.Value);
        Assert.Equal("session-projection", first.ProjectionProfileId?.Value);
        Assert.Equal("session-embedding", first.EmbeddingProfileId?.Value);

        var second = recall.Requests[1];
        Assert.Equal(CognitiveMemoryAccessLevel.Restricted, second.PolicyContext.AccessLevel);
        Assert.Equal("ask-collection", second.ProjectionCollectionName?.Value);
        Assert.Equal("ask-projection", second.ProjectionProfileId?.Value);
        Assert.Equal("ask-embedding", second.EmbeddingProfileId?.Value);

        await using var dbContext = fixture.Factory.CreateDbContext();
        var persisted = await dbContext.Set<CognitiveMemoryProbeSessionRecord>().SingleAsync(item => item.Id == session.Id);
        Assert.Equal(CognitiveMemoryAccessLevel.Restricted, persisted.AccessLevel);
        Assert.Equal(CognitiveMemoryRiskLevel.High, persisted.RiskLevel);
        Assert.True(persisted.AllowRestrictedContent);
        Assert.Equal("session-collection", persisted.ProjectionCollectionName);
    }

    [Fact]
    public async Task ProbeFeedback_CreatesReviewRegressionAndCalibrationWithoutMutatingTruth()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var turnId = await SeedProbeTurnAsync(fixture, projectId);
        var calibration = new CognitiveMemoryCalibrationHealthService(fixture.Factory, fixture.ScoreDriver, fixture.Clock);
        var service = new CognitiveMemoryProbeService(
            fixture.Factory,
            new FakeRecallOrchestrator(projectId),
            fixture.ScoreDriver,
            calibration,
            fixture.Clock);

        var feedback = await service.RecordFeedbackAsync(new CognitiveMemoryProbeFeedbackRequest(
            turnId,
            CognitiveMemoryProbeFeedbackAction.AddCorrection,
            "The answer mixed production and local Docker contexts.",
            "Production deployment must cite the production runbook.",
            CognitiveMemoryRiskLevel.High,
            CreateRegressionTest: true,
            RequestHumanReview: true,
            CognitiveMemoryCalibrationOutcomeKind.IncorrectHighConfidence));

        await using var dbContext = fixture.Factory.CreateDbContext();
        Assert.NotNull(feedback.ReviewItemId);
        Assert.NotNull(feedback.RegressionTestCaseId);
        Assert.NotNull(feedback.CalibrationEventId);
        var candidate = Assert.Single(await dbContext.Set<CognitiveMemoryConsolidationCandidateRecord>().ToListAsync());
        var reviewItem = Assert.Single(await dbContext.Set<CognitiveMemoryReviewItemRecord>().ToListAsync());
        var mutation = Assert.Single(await dbContext.Set<CognitiveMemoryMutationCommandRecord>().ToListAsync());
        Assert.Equal(feedback.ReviewItemId, candidate.ReviewItemId);
        Assert.Equal(CognitiveMemoryConsolidationCandidateStatus.ReviewRequired, candidate.Status);
        Assert.Equal(CognitiveMemoryMutationCommandStatus.ReviewRequired, mutation.Status);
        Assert.Equal(candidate.MutationCommandId, mutation.Id);
        Assert.NotNull(candidate.SourceItemId);
        Assert.NotNull(candidate.EvidenceAnchorId);
        Assert.Equal(1, reviewItem.SourceEvidenceCount);
        Assert.Single(await dbContext.Set<CognitiveMemorySourceItemRecord>().ToListAsync());
        Assert.Single(await dbContext.Set<CognitiveMemoryEvidenceAnchorRecord>().ToListAsync());
        Assert.Equal(CognitiveMemoryProbeTurnStatus.FeedbackRecorded, await dbContext.Set<CognitiveMemoryProbeTurnRecord>()
            .Where(turn => turn.Id == turnId)
            .Select(turn => turn.Status)
            .SingleAsync());
        Assert.Single(await dbContext.Set<CognitiveMemoryProbeFindingRecord>().ToListAsync());
        Assert.Single(await dbContext.Set<CognitiveMemoryCalibrationAggregateRecord>().ToListAsync());
        Assert.Equal(0, await dbContext.Set<CognitiveMemoryRecord>().CountAsync());
    }

    [Fact]
    public async Task ProbeFeedbackActionSemantics_RequestReviewAndCreateRegressionAreHonored()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var reviewTurnId = await SeedProbeTurnAsync(fixture, projectId);
        var regressionTurnId = await SeedProbeTurnAsync(fixture, projectId);
        var calibration = new CognitiveMemoryCalibrationHealthService(fixture.Factory, fixture.ScoreDriver, fixture.Clock);
        var service = new CognitiveMemoryProbeService(
            fixture.Factory,
            new FakeRecallOrchestrator(projectId),
            fixture.ScoreDriver,
            calibration,
            fixture.Clock);

        var reviewFeedback = await service.RecordFeedbackAsync(new CognitiveMemoryProbeFeedbackRequest(
            reviewTurnId,
            CognitiveMemoryProbeFeedbackAction.RequestReview,
            "User wants this answer checked.",
            string.Empty,
            CognitiveMemoryRiskLevel.Low,
            CreateRegressionTest: false,
            RequestHumanReview: false,
            CognitiveMemoryCalibrationOutcomeKind.Unknown));
        var regressionFeedback = await service.RecordFeedbackAsync(new CognitiveMemoryProbeFeedbackRequest(
            regressionTurnId,
            CognitiveMemoryProbeFeedbackAction.CreateRegression,
            "Keep this question as a regression.",
            "Expected answer keeps the project scope.",
            CognitiveMemoryRiskLevel.Low,
            CreateRegressionTest: false,
            RequestHumanReview: false,
            CognitiveMemoryCalibrationOutcomeKind.Unknown));

        await using var dbContext = fixture.Factory.CreateDbContext();
        Assert.NotNull(reviewFeedback.ReviewItemId);
        Assert.Null(reviewFeedback.RegressionTestCaseId);
        Assert.Null(regressionFeedback.ReviewItemId);
        Assert.NotNull(regressionFeedback.RegressionTestCaseId);
        Assert.Single(await dbContext.Set<CognitiveMemoryReviewItemRecord>().ToListAsync());
        Assert.Single(await dbContext.Set<CognitiveMemoryProbeRegressionTestCaseRecord>().ToListAsync());
        Assert.Equal(0, await dbContext.Set<CognitiveMemoryConsolidationCandidateRecord>().CountAsync());
    }

    [Fact]
    public async Task ProbeFeedbackCorrection_ApprovalAppliesRepairCandidateMemory()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var turnId = await SeedProbeTurnAsync(fixture, projectId);
        var calibration = new CognitiveMemoryCalibrationHealthService(fixture.Factory, fixture.ScoreDriver, fixture.Clock);
        var probeService = new CognitiveMemoryProbeService(
            fixture.Factory,
            new FakeRecallOrchestrator(projectId),
            fixture.ScoreDriver,
            calibration,
            fixture.Clock);

        var feedback = await probeService.RecordFeedbackAsync(new CognitiveMemoryProbeFeedbackRequest(
            turnId,
            CognitiveMemoryProbeFeedbackAction.AddCorrection,
            "The answer mixed production and local Docker contexts.",
            "Production deployment must cite the production runbook.",
            CognitiveMemoryRiskLevel.Medium,
            CreateRegressionTest: false,
            RequestHumanReview: true,
            CognitiveMemoryCalibrationOutcomeKind.IncorrectHighConfidence));

        await using (var beforeApproval = fixture.Factory.CreateDbContext())
        {
            Assert.NotNull(feedback.ReviewItemId);
            Assert.Equal(0, await beforeApproval.Set<CognitiveMemoryRecord>().CountAsync());
            Assert.Equal(0, await beforeApproval.Set<CognitiveMemoryClaimRecord>().CountAsync());
        }

        await using var decisionContext = fixture.Factory.CreateDbContext();
        var pendingReview = await decisionContext.Set<CognitiveMemoryReviewItemRecord>().SingleAsync();
        var reviewService = new CognitiveMemoryReviewUiService(
            fixture.Factory,
            fixture.Clock,
            new CognitiveMemoryConsolidationCandidateApplicator(new CognitiveMemoryRecordValidator()));

        var decided = await reviewService.DecideReviewItemAsync(new CognitiveMemoryReviewDecisionRequest(
            new CognitiveMemoryReviewItemId(pendingReview.Id),
            CognitiveMemoryReviewDecisionKind.Approve,
            "operator:test",
            "Approved against source truth.",
            pendingReview.ConcurrencyToken));

        await using var dbContext = fixture.Factory.CreateDbContext();
        var memory = Assert.Single(await dbContext.Set<CognitiveMemoryRecord>().ToListAsync());
        var candidate = Assert.Single(await dbContext.Set<CognitiveMemoryConsolidationCandidateRecord>().ToListAsync());
        var mutation = Assert.Single(await dbContext.Set<CognitiveMemoryMutationCommandRecord>().ToListAsync());
        Assert.Equal(CognitiveMemoryReviewStatus.Approved, decided.Status);
        Assert.Equal("Production deployment must cite the production runbook.", memory.CanonicalText);
        Assert.Equal(CognitiveMemoryValidationState.Approved, memory.ValidationState);
        Assert.Equal(CognitiveMemoryConsolidationCandidateStatus.MutationSubmitted, candidate.Status);
        Assert.Equal(memory.Id, candidate.MemoryRecordId);
        Assert.Equal(CognitiveMemoryMutationCommandStatus.Accepted, mutation.Status);
        Assert.False(mutation.RequiresHumanReview);
        Assert.Single(await dbContext.Set<CognitiveMemorySourceLinkRecord>().ToListAsync());
        Assert.Single(await dbContext.Set<CognitiveMemoryClaimEvidenceLinkRecord>().ToListAsync());
    }

    [Fact]
    public async Task CuratorCapture_NewKnowledgeAppliesTrustedMemoryWithoutReview()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var service = CreateCuratorService(fixture);
        var session = await service.StartAsync(new CognitiveMemoryCuratorSessionStartRequest(
            projectId,
            "Curator chat",
            Policy(projectId),
            CognitiveMemoryCuratorRuntimeMode.DirectLlm));

        var result = await service.RecordTurnAsync(new CognitiveMemoryCuratorTurnCaptureRequest(
            session.Id,
            "Remember that LB4U payroll reserve must cover two months of salaries.",
            "I will retain that as trusted project knowledge.",
            CognitiveMemoryCuratorRuntimeMode.DirectLlm,
            ExplicitCaptureKind: CognitiveMemoryCuratorCaptureKind.NewKnowledge));

        await using var dbContext = fixture.Factory.CreateDbContext();
        var capture = Assert.Single(result.CapturedImprovements);
        var persistedCapture = await dbContext.Set<CognitiveMemoryCuratorCapturedImprovementRecord>().SingleAsync();
        var mutation = await dbContext.Set<CognitiveMemoryMutationCommandRecord>().SingleAsync();
        var candidate = await dbContext.Set<CognitiveMemoryConsolidationCandidateRecord>().SingleAsync();
        var evidence = await dbContext.Set<CognitiveMemoryEvidenceAnchorRecord>().SingleAsync();
        var memory = await dbContext.Set<CognitiveMemoryRecord>().SingleAsync();

        Assert.Equal(capture.Id, persistedCapture.Id);
        Assert.Equal(CognitiveMemoryCuratorCaptureStatus.Applied, persistedCapture.Status);
        Assert.Equal(CognitiveMemoryCuratorCaptureKind.NewKnowledge, persistedCapture.CaptureKind);
        Assert.Equal("agent:test", persistedCapture.ActorId);
        Assert.Equal(0.95, persistedCapture.ConfidenceScore);
        Assert.Equal(0.95, persistedCapture.PriorityScore);
        Assert.Equal(CognitiveMemoryMutationCommandStatus.Accepted, mutation.Status);
        Assert.Equal(CognitiveMemoryMutationCommandKind.ProposeClaim, mutation.CommandKind);
        Assert.False(mutation.RequiresHumanReview);
        Assert.Equal(CognitiveMemorySourceTrustLevel.HumanReview, evidence.TrustLevel);
        Assert.Equal(CognitiveMemoryConsolidationCandidateKind.Knowledge, candidate.CandidateKind);
        Assert.Equal(CognitiveMemoryConsolidationCandidateStatus.MutationSubmitted, candidate.Status);
        Assert.Equal(memory.Id, candidate.MemoryRecordId);
        Assert.Equal("LB4U payroll reserve must cover two months of salaries.", memory.CanonicalText);
        Assert.Equal(CognitiveMemoryValidationState.Approved, memory.ValidationState);
        Assert.Equal(CognitiveMemoryStabilityState.Active, memory.StabilityState);
        Assert.Equal(0, await dbContext.Set<CognitiveMemoryReviewItemRecord>().CountAsync());
    }

    [Fact]
    public async Task CuratorCapture_CorrectionTargetsIncludedRecallMemoryAndSupersedesIt()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var affectedMemoryId = await SeedMemoryRecordAsync(fixture, projectId, CognitiveMemoryAccessLevel.Project);
        var recallTraceId = await SeedRecallTraceWithIncludedMemoryAsync(fixture, projectId, affectedMemoryId);
        var service = CreateCuratorService(fixture);
        var session = await service.StartAsync(new CognitiveMemoryCuratorSessionStartRequest(
            projectId,
            "Curator correction chat",
            Policy(projectId),
            CognitiveMemoryCuratorRuntimeMode.DirectLlm));

        var result = await service.RecordTurnAsync(new CognitiveMemoryCuratorTurnCaptureRequest(
            session.Id,
            "That is not correct. Production deploys from the signed release branch, not from a local Docker context.",
            "Production deploys from a local Docker context.",
            CognitiveMemoryCuratorRuntimeMode.DirectLlm,
            RecallTraceId: recallTraceId,
            ExplicitCaptureKind: CognitiveMemoryCuratorCaptureKind.Correction));

        await using var dbContext = fixture.Factory.CreateDbContext();
        var capture = Assert.Single(result.CapturedImprovements);
        var persistedCapture = await dbContext.Set<CognitiveMemoryCuratorCapturedImprovementRecord>().SingleAsync();
        var mutation = await dbContext.Set<CognitiveMemoryMutationCommandRecord>().SingleAsync();
        var candidate = await dbContext.Set<CognitiveMemoryConsolidationCandidateRecord>().SingleAsync();
        var relation = await dbContext.Set<CognitiveMemoryRelationRecord>().SingleAsync();
        var affectedMemory = await dbContext.Set<CognitiveMemoryRecord>().SingleAsync(record => record.Id == affectedMemoryId);
        var appliedMemory = await dbContext.Set<CognitiveMemoryRecord>().SingleAsync(record => record.Id == capture.AppliedMemoryRecordId);
        var capturedAffectedIds = DeserializeGuidList(persistedCapture.AffectedMemoryRecordIdsJson);
        var mutationAffectedIds = DeserializeGuidList(mutation.AffectedMemoryRecordIdsJson);

        Assert.Equal(CognitiveMemoryCuratorCaptureStatus.Applied, persistedCapture.Status);
        Assert.Equal(CognitiveMemoryCuratorCaptureKind.Correction, persistedCapture.CaptureKind);
        Assert.Equal(recallTraceId, persistedCapture.RecallTraceId);
        Assert.Contains(affectedMemoryId, capturedAffectedIds);
        Assert.Contains(affectedMemoryId, mutationAffectedIds);
        Assert.Contains(appliedMemory.Id, mutationAffectedIds);
        Assert.Equal(CognitiveMemoryMutationCommandKind.SupersedeClaim, mutation.CommandKind);
        Assert.Equal(CognitiveMemoryMutationCommandStatus.Accepted, mutation.Status);
        Assert.False(mutation.RequiresHumanReview);
        Assert.Equal(CognitiveMemoryConsolidationCandidateKind.Contradiction, candidate.CandidateKind);
        Assert.Equal(appliedMemory.Id, candidate.MemoryRecordId);
        Assert.Equal(CognitiveMemoryValidationState.Superseded, affectedMemory.ValidationState);
        Assert.Equal(CognitiveMemoryStabilityState.Stale, affectedMemory.StabilityState);
        Assert.Equal(appliedMemory.Id, relation.SourceMemoryRecordId);
        Assert.Equal(affectedMemoryId, relation.TargetMemoryRecordId);
        Assert.Equal(CognitiveMemoryRelationKind.Supersedes, relation.RelationKind);
        Assert.Contains("signed release branch", appliedMemory.CanonicalText, StringComparison.Ordinal);
        Assert.Equal(0, await dbContext.Set<CognitiveMemoryReviewItemRecord>().CountAsync());
    }

    [Fact]
    public async Task CuratorSend_DirectLlmUsesConfiguredProviderAndSharedCapturePath()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var providerId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var recall = new FakeRecallOrchestrator(projectId);
        var workspace = new FakeAgentFrameworkWorkspaceService
        {
            DirectResponseText = "Direct curator response."
        };
        var service = CreateCuratorService(
            fixture,
            recall,
            CreateSettingsService(fixture, defaultProviderProfileId: providerId),
            workspace);
        var session = await service.StartAsync(new CognitiveMemoryCuratorSessionStartRequest(
            projectId,
            "Direct curator chat",
            Policy(projectId),
            CognitiveMemoryCuratorRuntimeMode.DirectLlm));

        var result = await service.SendAsync(new CognitiveMemoryCuratorSendRequest(
            session.Id,
            "Remember that production releases require signed artifacts.",
            ExplicitCaptureKind: CognitiveMemoryCuratorCaptureKind.NewKnowledge));

        await using var dbContext = fixture.Factory.CreateDbContext();
        Assert.Equal("Direct curator response.", result.ResponseText);
        Assert.Equal(CognitiveMemoryCuratorRuntimeMode.DirectLlm, result.RuntimeMode);
        Assert.Equal(providerId, result.ProviderProfileId);
        Assert.Equal(CognitiveMemoryModelExecutionProfileDefaults.OpenAiDefaultModelId, result.ModelId?.Value);
        Assert.Single(recall.Requests);
        var directRequest = Assert.Single(workspace.DirectRequests);
        Assert.Equal(providerId, directRequest.ProviderId);
        Assert.Contains("Memory context:", directRequest.Request.SystemPrompt, StringComparison.Ordinal);
        Assert.Equal("Remember that production releases require signed artifacts.", directRequest.Request.Prompt);
        Assert.Single(result.CapturedImprovements);
        Assert.Single(await dbContext.Set<CognitiveMemoryCuratorTurnRecord>().ToListAsync());
        Assert.Single(await dbContext.Set<CognitiveMemoryConsolidationCandidateRecord>().ToListAsync());
        Assert.Equal(0, await dbContext.Set<CognitiveMemoryReviewItemRecord>().CountAsync());
    }

    [Fact]
    public async Task CuratorSend_AgentModeUsesConfiguredAgentWithAutoApprovalAndSharedCapturePath()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var agentId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var recall = new FakeRecallOrchestrator(projectId);
        var workspace = new FakeAgentFrameworkWorkspaceService
        {
            AgentResponseText = "Agent curator response."
        };
        var service = CreateCuratorService(
            fixture,
            recall,
            CreateSettingsService(fixture, defaultAgentId: agentId),
            workspace);
        var session = await service.StartAsync(new CognitiveMemoryCuratorSessionStartRequest(
            projectId,
            "Agent curator chat",
            Policy(projectId),
            CognitiveMemoryCuratorRuntimeMode.Agent));

        var result = await service.SendAsync(new CognitiveMemoryCuratorSendRequest(
            session.Id,
            "Remember that support escalations need the on-call runbook.",
            ExplicitCaptureKind: CognitiveMemoryCuratorCaptureKind.NewKnowledge));

        await using var dbContext = fixture.Factory.CreateDbContext();
        var persistedSession = await dbContext.Set<CognitiveMemoryCuratorSessionRecord>().SingleAsync();
        var executionRequest = Assert.Single(workspace.ExecutionRequests);
        Assert.Equal("Agent curator response.", result.ResponseText);
        Assert.Equal(CognitiveMemoryCuratorRuntimeMode.Agent, result.RuntimeMode);
        Assert.Equal(agentId, result.AgentId);
        Assert.Equal(agentId, executionRequest.AgentId);
        Assert.True(executionRequest.AutoApprovePendingToolCalls);
        Assert.NotNull(executionRequest.ChatSessionId);
        Assert.Equal(executionRequest.ChatSessionId, persistedSession.AgentChatSessionId);
        Assert.Contains("Memory context:", executionRequest.Prompt, StringComparison.Ordinal);
        Assert.Single(result.CapturedImprovements);
        Assert.Single(await dbContext.Set<CognitiveMemoryCuratorTurnRecord>().ToListAsync());
        Assert.Single(await dbContext.Set<CognitiveMemoryConsolidationCandidateRecord>().ToListAsync());
        Assert.Equal(0, await dbContext.Set<CognitiveMemoryReviewItemRecord>().CountAsync());
    }

    [Fact]
    public async Task CuratorSend_MissingDirectProviderAndAgentConfigurationFailExplicitly()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var directService = CreateCuratorService(
            fixture,
            new FakeRecallOrchestrator(projectId),
            CreateSettingsService(fixture),
            new FakeAgentFrameworkWorkspaceService());
        var directSession = await directService.StartAsync(new CognitiveMemoryCuratorSessionStartRequest(
            projectId,
            "Direct curator chat",
            Policy(projectId),
            CognitiveMemoryCuratorRuntimeMode.DirectLlm));
        var directError = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await directService.SendAsync(new CognitiveMemoryCuratorSendRequest(directSession.Id, "What do you know?")));

        var agentService = CreateCuratorService(
            fixture,
            new FakeRecallOrchestrator(projectId),
            CreateSettingsService(fixture),
            new FakeAgentFrameworkWorkspaceService());
        var agentSession = await agentService.StartAsync(new CognitiveMemoryCuratorSessionStartRequest(
            projectId,
            "Agent curator chat",
            Policy(projectId),
            CognitiveMemoryCuratorRuntimeMode.Agent));
        var agentError = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await agentService.SendAsync(new CognitiveMemoryCuratorSendRequest(agentSession.Id, "What do you know?")));

        Assert.Contains("provider profile", directError.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("configured agent", agentError.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SelfRegulation_AssessesPostureAndUpdatesRecallTrace()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var recallTraceId = await SeedRecallTraceAsync(fixture, projectId);
        var calibration = new CognitiveMemoryCalibrationHealthService(fixture.Factory, fixture.ScoreDriver, fixture.Clock);
        var selfModelStore = new CognitiveMemorySelfModelStore(fixture.Factory, fixture.ScoreDriver, fixture.Clock);
        var service = new CognitiveMemorySelfRegulationOrchestrator(
            fixture.Factory,
            selfModelStore,
            calibration,
            fixture.ScoreDriver,
            fixture.Clock);

        var result = await service.AssessAsync(new CognitiveMemorySelfRegulationAssessmentRequest(
            projectId,
            "agent:test",
            new CognitiveMemoryModelProfileId("unit-model"),
            new CognitiveMemoryRoleKey("developer"),
            "architecture",
            "answering",
            CognitiveMemoryRiskLevel.High,
            Policy(projectId, CognitiveMemoryRiskLevel.High),
            SourceSufficiency: 0.7,
            EvidenceCoverage: 0.6,
            ContextFit: 0.7,
            ContradictionPressure: 0.8,
            RedactionPressure: 0.1,
            CognitiveLoad: 0.2,
            HighImpact: false,
            RecentCorrection: true,
            RecallTraceId: recallTraceId));

        await using var dbContext = fixture.Factory.CreateDbContext();
        var recallTrace = await dbContext.Set<CognitiveMemoryRecallTraceRecord>().SingleAsync(trace => trace.Id == recallTraceId);
        Assert.Equal(CognitiveMemorySelfRegulationStateKind.ProfessorReviewNeeded, result.Assessment.State);
        Assert.Equal(CognitiveMemoryAnswerPostureKind.ProfessorReviewRequired, result.Posture.Posture);
        Assert.Contains(result.HumilityTriggers, trigger => trigger.TriggerKind == CognitiveMemoryHumilityTriggerKind.ContradictionPressure);
        Assert.Equal(result.Assessment.Id, recallTrace.SelfRegulationAssessmentId);
        Assert.Equal(result.Posture.Id, recallTrace.AnswerPostureDecisionId);
        Assert.True(await dbContext.Set<CognitiveMemoryScoreComponentRecord>()
            .AnyAsync(component => component.SpaceKind == CognitiveMemoryScoreSpaceKind.SelfRegulationAssessment));
    }

    [Fact]
    public async Task AnswerGate_UsesPostureAndPersistsTraceToRecall()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var recallTraceId = await SeedRecallTraceAsync(fixture, projectId);
        var posture = await SeedPostureAsync(fixture, projectId, CognitiveMemoryAnswerPostureKind.ProfessorReviewRequired);
        var service = new CognitiveMemoryAnswerGateService(fixture.Factory, fixture.ScoreDriver, fixture.Clock);

        var decision = await service.DecideAsync(new CognitiveMemoryAnswerGateRequest(
            projectId,
            "agent:test",
            Policy(projectId, CognitiveMemoryRiskLevel.High),
            recallTraceId,
            posture.SelfRegulationAssessmentId,
            posture.Id,
            ProfessorReviewId: null,
            SourceSufficiency: 0.9,
            ContextFit: 0.9,
            EvidenceSupport: 0.9,
            ContradictionPressure: 0.1,
            StalenessPressure: 0.1,
            RedactionPressure: 0.1,
            CalibrationRisk: 0.1,
            CognitiveMemoryRiskLevel.High,
            ProcedureUnvalidated: false,
            ProfessorReviewRequired: false,
            "Draft answer waits for challenge review."));

        await using var dbContext = fixture.Factory.CreateDbContext();
        var recallTrace = await dbContext.Set<CognitiveMemoryRecallTraceRecord>().SingleAsync(trace => trace.Id == recallTraceId);
        Assert.Equal(CognitiveMemoryAnswerGateDecisionKind.ProfessorReview, decision.DecisionKind);
        Assert.Equal(decision.Id, recallTrace.AnswerGateDecisionId);
        Assert.Equal(posture.Id, recallTrace.AnswerPostureDecisionId);
        Assert.True(await dbContext.Set<CognitiveMemoryScoreComponentRecord>()
            .AnyAsync(component => component.SpaceKind == CognitiveMemoryScoreSpaceKind.AnswerGate));
    }

    [Fact]
    public async Task ProfessorReview_CompletionRoutesSuggestionsWithoutCreatingTruth()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var service = new CognitiveMemoryProfessorReviewService(fixture.Factory, fixture.ScoreDriver, fixture.Clock);

        var requested = await service.RequestReviewAsync(new CognitiveMemoryProfessorReviewRequest(
            projectId,
            CognitiveMemoryProfessorReviewMode.SourceSufficiencyReview,
            "agent:test",
            new CognitiveMemoryModelProfileId("unit-professor"),
            "professor-v1",
            Policy(projectId, CognitiveMemoryRiskLevel.Medium),
            SelfRegulationAssessmentId: null,
            AnswerPostureDecisionId: null,
            "Check whether the Docker rollback answer has enough source support.",
            "restricted source text should not be exposed",
            [CognitiveMemoryProfessorSuggestionKind.ReviewItem]));
        Assert.Equal("[redacted by cognitive-memory professor-review access policy]", requested.ContextSummary);

        var completed = await service.CompleteReviewAsync(
            requested.Id,
            "The answer needs a source audit before being treated as reliable.",
            "Missing production rollback runbook citation.",
            CognitiveMemoryAnswerPostureKind.SourceAuditRequired,
            [CognitiveMemoryProfessorSuggestionKind.ReviewItem, CognitiveMemoryProfessorSuggestionKind.LearningProposal]);

        await using var dbContext = fixture.Factory.CreateDbContext();
        Assert.Equal(CognitiveMemoryProfessorReviewStatus.Completed, completed.Status);
        Assert.Equal(1, await dbContext.Set<CognitiveMemoryReviewItemRecord>().CountAsync());
        Assert.Equal(1, await dbContext.Set<CognitiveMemoryLearningProposalRecord>().CountAsync());
        Assert.Equal(0, await dbContext.Set<CognitiveMemoryRecord>().CountAsync());
        Assert.Equal(0, await dbContext.Set<CognitiveMemoryMutationCommandRecord>().CountAsync());
    }

    [Fact]
    public async Task EpistemicDrive_CreatesApprovalGatedLearningTaskFromGaps()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await using (var dbContext = fixture.Factory.CreateDbContext())
        {
            dbContext.Add(new CognitiveMemoryAnswerGateDecisionRecord
            {
                ProjectId = projectId,
                DecisionKind = CognitiveMemoryAnswerGateDecisionKind.SourceAudit,
                ScoreEvaluationTraceId = Guid.NewGuid(),
                DecisionBucket = CognitiveMemoryScoreProjectionBucket.NeedsReview,
                WarningsJson = "[]",
                RequiredOperationsJson = "[\"SourceAudit\"]",
                Reason = "Answer gate could not cite enough source evidence.",
                CreatedAtUtc = fixture.Clock.GetUtcNow(),
                ConcurrencyToken = Guid.NewGuid()
            });
            dbContext.Add(new CognitiveMemoryCalibrationAggregateRecord
            {
                ProjectId = projectId,
                DomainKey = "docker",
                TaskTypeKey = "deployment",
                ModelProfileId = new CognitiveMemoryModelProfileId("unit-model"),
                RiskKey = new CognitiveMemoryRiskKey("high"),
                FeaturePatternKey = "general",
                ProfileVersion = "calibration-v1",
                ObservationCount = 4,
                OverconfidenceRate = 0.5,
                SourceInsufficientRate = 0.5,
                WrongScopeRate = 0.25,
                UpdatedAtUtc = fixture.Clock.GetUtcNow(),
                ConcurrencyToken = Guid.NewGuid()
            });
            await dbContext.SaveChangesAsync();
        }

        var service = new CognitiveMemoryEpistemicDriveService(fixture.Factory, fixture.ScoreDriver, fixture.Clock);
        var proposals = await service.ScanAsync(new CognitiveMemoryEpistemicScanRequest(
            projectId,
            Policy(projectId),
            "agent:test"));
        var approved = await service.DecideProposalAsync(
            proposals[0].Id,
            CognitiveMemoryLearningProposalStatus.Approved,
            "operator:test",
            "Approved for source-backed expansion.");

        await using var verification = fixture.Factory.CreateDbContext();
        Assert.True(proposals.Count >= 2);
        Assert.Equal(CognitiveMemoryLearningProposalStatus.Approved, approved.Status);
        Assert.Single(await verification.Set<CognitiveMemoryLearningTaskRecord>().ToListAsync());
        Assert.Equal(0, await verification.Set<CognitiveMemoryMutationCommandRecord>().CountAsync());
    }

    [Fact]
    public async Task EpistemicDrive_CreatesSourceBackedPlanningCoverageProposals()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SeedSourceItemAsync(
            fixture,
            projectId,
            "LB4U business plan",
            """
            The business plan explains the LB4U product, market launch, marketing campaign, sales channels,
            salary costs, payroll reserve, supplier procurement, equipment expenses, and phased staffing plan.
            """);
        var service = new CognitiveMemoryEpistemicDriveService(fixture.Factory, fixture.ScoreDriver, fixture.Clock);

        var proposals = await service.ScanAsync(new CognitiveMemoryEpistemicScanRequest(
            projectId,
            Policy(projectId),
            "agent:test"));

        await using var dbContext = fixture.Factory.CreateDbContext();
        Assert.Contains(proposals, proposal => proposal.Title.Contains("finance-and-expenses", StringComparison.Ordinal));
        Assert.Contains(proposals, proposal => proposal.Title.Contains("market-and-marketing", StringComparison.Ordinal));
        Assert.All(proposals, proposal => Assert.Equal(CognitiveMemoryLearningProposalStatus.PendingApproval, proposal.Status));
        Assert.Contains(proposals, proposal => proposal.EvidenceRefsJson.Contains("source-item:", StringComparison.Ordinal));
        Assert.True(await dbContext.Set<CognitiveMemoryCoverageMapRecord>().AnyAsync(map => map.SourceEvidenceCount > 0));
        Assert.Equal(0, await dbContext.Set<CognitiveMemoryMutationCommandRecord>().CountAsync());
    }

    [Fact]
    public async Task EpistemicDrive_DoesNotRecreateApprovedSourceCoverageProposal()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SeedSourceItemAsync(
            fixture,
            projectId,
            "Finance planning note",
            "Salary costs, payroll reserve, equipment expense budget, and revenue assumptions need planning.");
        var service = new CognitiveMemoryEpistemicDriveService(fixture.Factory, fixture.ScoreDriver, fixture.Clock);

        var proposals = await service.ScanAsync(new CognitiveMemoryEpistemicScanRequest(
            projectId,
            Policy(projectId),
            "agent:test"));
        var financeProposal = Assert.Single(proposals, proposal => proposal.Title.Contains("finance-and-expenses", StringComparison.Ordinal));
        await service.DecideProposalAsync(
            financeProposal.Id,
            CognitiveMemoryLearningProposalStatus.Approved,
            "operator:test",
            "Useful source-backed finance planning expansion.");

        var secondScan = await service.ScanAsync(new CognitiveMemoryEpistemicScanRequest(
            projectId,
            Policy(projectId),
            "agent:test"));

        Assert.DoesNotContain(secondScan, proposal => proposal.Title.Contains("finance-and-expenses", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CrossProjectPromotion_IsReviewGatedAndRejectsRestrictedSourceWithoutPolicy()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var recordId = await SeedMemoryRecordAsync(fixture, projectId, CognitiveMemoryAccessLevel.Restricted);
        var service = new CognitiveMemoryCrossProjectMemoryService(fixture.Factory, fixture.ScoreDriver, fixture.Clock);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.CreateCandidateAsync(new CognitiveMemoryCrossProjectPromotionRequest(
            recordId,
            projectId,
            "agent:test",
            Policy(projectId, allowRestrictedContent: false),
            SemanticSimilarity: 0.95,
            EntityEquivalence: 0.9,
            ContextSeparation: 0.2,
            SourceReusePermission: 0.4,
            PolicyCompatibility: 0.8,
            "Try unsafe promotion.")));

        var candidate = await service.CreateCandidateAsync(new CognitiveMemoryCrossProjectPromotionRequest(
            recordId,
            projectId,
            "agent:test",
            Policy(projectId, allowRestrictedContent: true),
            SemanticSimilarity: 0.95,
            EntityEquivalence: 0.9,
            ContextSeparation: 0.2,
            SourceReusePermission: 0.4,
            PolicyCompatibility: 0.8,
            "Promote only after review."));

        await using var dbContext = fixture.Factory.CreateDbContext();
        Assert.Equal(CognitiveMemoryCrossProjectPromotionStatus.PendingReview, candidate.Status);
        Assert.NotNull(candidate.ReviewItemId);
        Assert.Single(await dbContext.Set<CognitiveMemoryReviewItemRecord>().ToListAsync());
    }

    [Fact]
    public async Task DistributedCoordinator_RejectsLeaseOrSchemaMismatchWithoutMutatingMemory()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var service = new CognitiveMemoryDistributedComputeCoordinator(fixture.Factory, fixture.Clock);
        await service.RegisterWorkerAsync(
            "worker-1",
            "unit-machine",
            [CognitiveMemoryDistributedJobKind.ReplayAnalysis]);
        var job = await service.EnqueueAsync(new CognitiveMemoryDistributedJobEnqueueRequest(
            projectId,
            CognitiveMemoryDistributedJobKind.ReplayAnalysis,
            "project:docker",
            "{\"project\":\"docker\"}",
            "replay-analysis-v1",
            "replay-worker-v1",
            "policy:test"));
        var claim = await service.ClaimAsync(
            "worker-1",
            [CognitiveMemoryDistributedJobKind.ReplayAnalysis],
            TimeSpan.FromMinutes(5));

        var result = await service.SubmitResultAsync(
            job.Id,
            "worker-1",
            claim!.LeaseToken,
            claim.InputHash,
            "{\"summary\":\"done\"}",
            "replay-worker-v1",
            "wrong-schema");

        await using var dbContext = fixture.Factory.CreateDbContext();
        Assert.Equal(CognitiveMemoryDistributedResultStatus.Rejected, result.Status);
        Assert.Equal(CognitiveMemoryDistributedJobState.Rejected, await dbContext.Set<CognitiveMemoryDistributedJobRecord>()
            .Where(item => item.Id == job.Id)
            .Select(item => item.State)
            .SingleAsync());
        Assert.Equal(0, await dbContext.Set<CognitiveMemoryRecord>().CountAsync());
        Assert.Equal(0, await dbContext.Set<CognitiveMemoryMutationCommandRecord>().CountAsync());
    }

    private static async Task<Guid> SeedProbeTurnAsync(TestFixture fixture, Guid projectId)
    {
        await using var dbContext = fixture.Factory.CreateDbContext();
        var session = new CognitiveMemoryProbeSessionRecord
        {
            ProjectId = projectId,
            Status = CognitiveMemoryProbeSessionStatus.Active,
            RecallMode = CognitiveMemoryRecallMode.FocusedTaskContext,
            Title = "Docker context probe",
            ActorId = "agent:test",
            PolicyProfileId = "policy:test",
            AlgorithmVersion = "unit-test",
            TurnCount = 1,
            CreatedAtUtc = fixture.Clock.GetUtcNow(),
            UpdatedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        };
        var turn = new CognitiveMemoryProbeTurnRecord
        {
            ProbeSessionId = session.Id,
            ProjectId = projectId,
            Sequence = 1,
            Status = CognitiveMemoryProbeTurnStatus.Answered,
            Intent = CognitiveMemoryRecallIntentKind.Deployment,
            Question = "What Docker context should production deployment use?",
            AnswerSummary = "Use Docker production context.",
            RecallTraceId = Guid.NewGuid(),
            ProbeScoreEvaluationTraceId = Guid.NewGuid(),
            ProbeScoreBucket = CognitiveMemoryScoreProjectionBucket.NeedsReview,
            DisplayProbeScore = 0.9,
            WarningsJson = "[]",
            MetadataJson = "{}",
            CreatedAtUtc = fixture.Clock.GetUtcNow(),
            UpdatedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        };
        dbContext.AddRange(session, turn);
        await dbContext.SaveChangesAsync();
        return turn.Id;
    }

    private static async Task<Guid> SeedRecallTraceAsync(TestFixture fixture, Guid projectId)
    {
        await using var dbContext = fixture.Factory.CreateDbContext();
        var trace = new CognitiveMemoryRecallTraceRecord
        {
            ProjectId = projectId,
            RecallMode = CognitiveMemoryRecallMode.FocusedTaskContext,
            RequestedByActorId = "agent:test",
            PolicyProfileId = "policy:test",
            RequestHash = CognitiveMemoryHash.FromUtf8(Guid.NewGuid().ToString("D")).Value,
            AlgorithmVersion = "unit-test",
            Outcome = CognitiveMemoryRunStatus.Succeeded,
            StartedAtUtc = fixture.Clock.GetUtcNow(),
            CompletedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        };
        dbContext.Add(trace);
        await dbContext.SaveChangesAsync();
        return trace.Id;
    }

    private static async Task<Guid> SeedRecallTraceWithIncludedMemoryAsync(TestFixture fixture, Guid projectId, Guid memoryRecordId)
    {
        await using var dbContext = fixture.Factory.CreateDbContext();
        var trace = new CognitiveMemoryRecallTraceRecord
        {
            ProjectId = projectId,
            RecallMode = CognitiveMemoryRecallMode.FocusedTaskContext,
            RequestedByActorId = "agent:test",
            PolicyProfileId = "policy:test",
            RequestHash = CognitiveMemoryHash.FromUtf8(Guid.NewGuid().ToString("D")).Value,
            AlgorithmVersion = "unit-test",
            Outcome = CognitiveMemoryRunStatus.Succeeded,
            StartedAtUtc = fixture.Clock.GetUtcNow(),
            CompletedAtUtc = fixture.Clock.GetUtcNow(),
            IncludedRecordCount = 1,
            ConcurrencyToken = Guid.NewGuid()
        };
        var sourceRef = new CognitiveMemoryRecallSourceRefRecord
        {
            RecallTraceId = trace.Id,
            ProjectId = projectId,
            MemoryRecordId = memoryRecordId,
            SourceSystem = "MemoryRecord",
            Locator = $"memory:{memoryRecordId:D}",
            Summary = "Seeded memory included in answer context.",
            AccessLevel = CognitiveMemoryAccessLevel.Project,
            RedactionState = CognitiveMemoryRedactionState.Safe,
            IncludedInContext = true,
            CreatedAtUtc = fixture.Clock.GetUtcNow()
        };
        dbContext.AddRange(trace, sourceRef);
        await dbContext.SaveChangesAsync();
        return trace.Id;
    }

    private static async Task<CognitiveMemoryAnswerPostureDecisionRecord> SeedPostureAsync(
        TestFixture fixture,
        Guid projectId,
        CognitiveMemoryAnswerPostureKind postureKind)
    {
        await using var dbContext = fixture.Factory.CreateDbContext();
        var assessment = new CognitiveMemorySelfRegulationAssessmentRecord
        {
            ProjectId = projectId,
            ActorId = "agent:test",
            ModelProfileId = new CognitiveMemoryModelProfileId("unit-model"),
            DomainKey = "architecture",
            TaskTypeKey = "answering",
            State = CognitiveMemorySelfRegulationStateKind.ProfessorReviewNeeded,
            AssessmentScoreEvaluationTraceId = Guid.NewGuid(),
            AssessmentBucket = CognitiveMemoryScoreProjectionBucket.NeedsReview,
            WarningsJson = "[]",
            RequiredOperationsJson = "[]",
            AlgorithmVersion = "unit-test",
            CreatedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        };
        var posture = new CognitiveMemoryAnswerPostureDecisionRecord
        {
            ProjectId = projectId,
            SelfRegulationAssessmentId = assessment.Id,
            Posture = postureKind,
            PostureScoreEvaluationTraceId = Guid.NewGuid(),
            PostureBucket = CognitiveMemoryScoreProjectionBucket.NeedsReview,
            RequiredOperationsJson = "[\"ProfessorReview\"]",
            WarningsJson = "[]",
            Reason = "Seeded posture.",
            CreatedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        };
        dbContext.AddRange(assessment, posture);
        await dbContext.SaveChangesAsync();
        return posture;
    }

    private static async Task<Guid> SeedMemoryRecordAsync(
        TestFixture fixture,
        Guid projectId,
        CognitiveMemoryAccessLevel accessLevel)
    {
        await using var dbContext = fixture.Factory.CreateDbContext();
        var record = new CognitiveMemoryRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Kind = CognitiveMemoryRecordKind.Semantic,
            Origin = CognitiveMemoryRecordOrigin.SourceDerived,
            Title = "Reusable Docker deployment memory",
            CanonicalText = "Docker deployment memory with restricted evidence.",
            SummaryText = "Potentially reusable Docker deployment guidance.",
            TopicKey = "docker.deployment",
            ValidationState = CognitiveMemoryValidationState.Approved,
            StabilityState = CognitiveMemoryStabilityState.Active,
            AlgorithmVersion = "unit-test",
            ContentHash = CognitiveMemoryHash.FromUtf8("restricted docker").Value,
            SourceEvidenceCount = 1,
            AccessLevel = accessLevel,
            RiskLevel = CognitiveMemoryRiskLevel.Medium,
            CreatedAtUtc = fixture.Clock.GetUtcNow(),
            UpdatedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        };
        dbContext.Add(record);
        await dbContext.SaveChangesAsync();
        return record.Id;
    }

    private static async Task SeedSourceItemAsync(
        TestFixture fixture,
        Guid projectId,
        string title,
        string content)
    {
        await using var dbContext = fixture.Factory.CreateDbContext();
        var contentHash = CognitiveMemoryHash.FromUtf8(content).Value;
        var manifest = new CognitiveMemorySourceManifestRecord
        {
            ProjectId = projectId,
            SourceSystem = "ExternalFile",
            SourceScopeKey = projectId.ToString("D"),
            SourceSnapshotId = $"snapshot-{contentHash[..12]}",
            SnapshotHash = contentHash,
            ProviderVersion = "unit-test",
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
            SourceSystem = "ExternalFile",
            SourceItemKey = $"external:{contentHash[..12]}",
            SourceItemType = "UploadedFileChunk",
            Title = title,
            ContentText = content,
            Locator = title,
            ContentHash = contentHash,
            RedactionState = CognitiveMemoryRedactionState.Safe,
            AccessLevel = CognitiveMemoryAccessLevel.Project,
            AccessScope = projectId.ToString("D"),
            ObservedAtUtc = fixture.Clock.GetUtcNow(),
            CreatedAtUtc = fixture.Clock.GetUtcNow(),
            UpdatedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        };
        dbContext.AddRange(manifest, sourceItem);
        await dbContext.SaveChangesAsync();
    }

    private static CognitiveMemoryPolicyContext Policy(
        Guid? projectId,
        CognitiveMemoryRiskLevel riskLevel = CognitiveMemoryRiskLevel.Low,
        bool allowRestrictedContent = false,
        CognitiveMemoryAccessLevel accessLevel = CognitiveMemoryAccessLevel.Project)
        => new(
            projectId,
            "agent:test",
            accessLevel,
            new CognitiveMemoryPolicyProfileId("policy:test"),
            riskLevel,
            allowRestrictedContent);

    private static ICognitiveMemoryCuratorConversationService CreateCuratorService(
        TestFixture fixture,
        ICognitiveMemoryRecallOrchestrator? recallOrchestrator = null,
        ICognitiveMemoryAutomationSettingsService? settingsService = null,
        IAgentFrameworkWorkspaceService? workspaceService = null)
        => new CognitiveMemoryCuratorConversationService(
            fixture.Factory,
            recallOrchestrator ?? new FakeRecallOrchestrator(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")),
            settingsService ?? CreateSettingsService(fixture),
            workspaceService ?? new FakeAgentFrameworkWorkspaceService(),
            new CognitiveMemoryConsolidationCandidateApplicator(new CognitiveMemoryRecordValidator()),
            fixture.Clock);

    private static ICognitiveMemoryAutomationSettingsService CreateSettingsService(
        TestFixture fixture,
        Guid? defaultProviderProfileId = null,
        Guid? defaultAgentId = null)
        => new FakeAutomationSettingsService(CognitiveMemoryAutomationSettings.Defaults(fixture.Clock.GetUtcNow()) with
        {
            DefaultProviderProfileId = defaultProviderProfileId,
            DefaultAgentId = defaultAgentId
        });

    private static IReadOnlyList<Guid> DeserializeGuidList(string json)
        => JsonSerializer.Deserialize(
            json,
            CognitiveMemoryJsonSerializerContext.Default.GuidArray) ?? [];

    private static TestFixture CreateFixture()
    {
        AppDbContextModelRegistry.ConfigureAssemblies([typeof(CognitiveMemoryModuleAssemblyMarker).Assembly]);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"cognitive-memory-advanced-{Guid.NewGuid():N}")
            .Options;
        var factory = new TestDbContextFactory(options);
        var scoreDriver = new CognitiveMemoryScoreGeometryDriver(new CognitiveMemoryScoreSpaceRegistry());
        return new TestFixture(factory, new FixedClock(), scoreDriver);
    }

    private sealed record TestFixture(
        TestDbContextFactory Factory,
        FixedClock Clock,
        ICognitiveMemoryScoreGeometryDriver ScoreDriver);

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
    }

    private sealed class FakeAutomationSettingsService(CognitiveMemoryAutomationSettings settings) : ICognitiveMemoryAutomationSettingsService
    {
        public ValueTask<CognitiveMemoryAutomationSettings> GetAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(settings);

        public ValueTask<CognitiveMemoryAutomationSettings> SaveAsync(
            CognitiveMemoryAutomationSettingsUpdate update,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeAgentFrameworkWorkspaceService : IAgentFrameworkWorkspaceService
    {
        public event EventHandler<ExecutionLogEntry>? ExecutionUpdated
        {
            add { }
            remove { }
        }

        public string DirectResponseText { get; init; } = "Direct response.";

        public string AgentResponseText { get; init; } = "Agent response.";

        public List<(Guid ProviderId, ProviderTestChatRequest Request)> DirectRequests { get; } = [];

        public List<ExecutionRunRequest> ExecutionRequests { get; } = [];

        public Task<ProviderTestChatResult> RunProviderTestChatAsync(
            Guid providerId,
            ProviderTestChatRequest request,
            CancellationToken cancellationToken = default)
        {
            DirectRequests.Add((providerId, request));
            return Task.FromResult(new ProviderTestChatResult(
                request.Model,
                DirectResponseText,
                InputTokens: 12,
                OutputTokens: 6));
        }

        public Task<ChatSessionRecord> GetOrCreateChatSessionAsync(
            Guid agentId,
            Guid? chatSessionId = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ChatSessionRecord(
                chatSessionId ?? Guid.NewGuid(),
                agentId,
                "Curator test chat",
                DateTimeOffset.UnixEpoch,
                DateTimeOffset.UnixEpoch,
                Messages: []));
        }

        public Task<ExecutionRunResult> ExecuteRunAsync(
            ExecutionRunRequest request,
            CancellationToken cancellationToken = default)
        {
            ExecutionRequests.Add(request);
            var executionRunId = Guid.NewGuid();
            var metric = new AgentRunMetric(
                Guid.NewGuid(),
                request.AgentId,
                request.ChatSessionId,
                DateTimeOffset.UnixEpoch,
                RunOutcome.Succeeded,
                "fake-provider",
                CognitiveMemoryModelExecutionProfileDefaults.OpenAiDefaultModelId,
                DurationMs: 10,
                InputTokens: 20,
                OutputTokens: 8,
                ToolCalls: 0)
            {
                ExecutionRunId = executionRunId
            };
            return Task.FromResult(new ExecutionRunResult(
                executionRunId,
                request.ChatSessionId,
                AgentResponseText,
                new ChatMessageRecord(
                    Guid.NewGuid(),
                    ChatMessageRole.Assistant,
                    AgentResponseText,
                    DateTimeOffset.UnixEpoch,
                    TokenEstimate: 8),
                metric));
        }

        public Task<SandboxDashboardSnapshot> GetDashboardAsync(CancellationToken cancellationToken = default)
            => NotSupported<SandboxDashboardSnapshot>();

        public Task<IReadOnlyList<AgentDefinition>> ListAgentsAsync(bool includeTemplates = true, CancellationToken cancellationToken = default)
            => NotSupported<IReadOnlyList<AgentDefinition>>();

        public Task<AgentEditorModel> GetAgentEditorAsync(Guid? agentId = null, CancellationToken cancellationToken = default)
            => NotSupported<AgentEditorModel>();

        public Task<Guid> SaveAgentAsync(AgentEditorModel model, CancellationToken cancellationToken = default)
            => NotSupported<Guid>();

        public Task DeleteAgentAsync(Guid agentId, CancellationToken cancellationToken = default)
            => NotSupported();

        public Task<IReadOnlyList<AgentTeamDefinition>> ListAgentTeamsAsync(CancellationToken cancellationToken = default)
            => NotSupported<IReadOnlyList<AgentTeamDefinition>>();

        public Task<AgentTeamEditorModel> GetAgentTeamEditorAsync(Guid? teamId = null, CancellationToken cancellationToken = default)
            => NotSupported<AgentTeamEditorModel>();

        public Task<Guid> SaveAgentTeamAsync(AgentTeamEditorModel model, CancellationToken cancellationToken = default)
            => NotSupported<Guid>();

        public Task<AgentTeamDefinition> UpdateAgentTeamMembersAsync(Guid teamId, IReadOnlyList<Guid> agentIds, CancellationToken cancellationToken = default)
            => NotSupported<AgentTeamDefinition>();

        public Task DeleteAgentTeamAsync(Guid teamId, CancellationToken cancellationToken = default)
            => NotSupported();

        public Task<Guid> CloneAgentAsync(Guid agentId, string cloneName, CancellationToken cancellationToken = default)
            => NotSupported<Guid>();

        public Task<Guid> ConvertToTemplateAsync(Guid agentId, string templateKey, CancellationToken cancellationToken = default)
            => NotSupported<Guid>();

        public Task<AgentExportResult> ExportAgentAsync(Guid agentId, CancellationToken cancellationToken = default)
            => NotSupported<AgentExportResult>();

        public Task<Guid> ImportAgentAsync(string packagePath, CancellationToken cancellationToken = default)
            => NotSupported<Guid>();

        public Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(CancellationToken cancellationToken = default)
            => NotSupported<IReadOnlyList<ProviderProfile>>();

        public Task<ProviderProfileEditorModel> GetProviderEditorAsync(Guid? providerId = null, CancellationToken cancellationToken = default)
            => NotSupported<ProviderProfileEditorModel>();

        public Task<Guid> SaveProviderAsync(ProviderProfileEditorModel model, CancellationToken cancellationToken = default)
            => NotSupported<Guid>();

        public Task DeleteProviderAsync(Guid providerId, CancellationToken cancellationToken = default)
            => NotSupported();

        public Task<ProviderHealthResult> TestProviderAsync(Guid providerId, CancellationToken cancellationToken = default)
            => NotSupported<ProviderHealthResult>();

        public Task<OllamaModelfileResult> CreateOrUpdateOllamaModelAsync(Guid providerId, OllamaModelfileRequest request, CancellationToken cancellationToken = default)
            => NotSupported<OllamaModelfileResult>();

        public Task<IReadOnlyList<CapabilityCatalogItem>> ListCapabilitiesAsync(CancellationToken cancellationToken = default)
            => NotSupported<IReadOnlyList<CapabilityCatalogItem>>();

        public Task<CapabilityEditorModel> GetCapabilityEditorAsync(Guid? capabilityId = null, CancellationToken cancellationToken = default)
            => NotSupported<CapabilityEditorModel>();

        public Task<Guid> SaveCapabilityAsync(CapabilityEditorModel model, CancellationToken cancellationToken = default)
            => NotSupported<Guid>();

        public Task DeleteCapabilityAsync(Guid capabilityId, CancellationToken cancellationToken = default)
            => NotSupported();

        public Task VerifyCapabilityAsync(Guid agentId, Guid capabilityId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<ChatSessionRecord>> ListChatSessionsAsync(Guid agentId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ChatSessionRecord>>([]);

        public Task<ChatPageBootstrapSnapshot> GetChatPageBootstrapAsync(bool includeTemplates = false, CancellationToken cancellationToken = default)
            => NotSupported<ChatPageBootstrapSnapshot>();

        public Task<ChatAgentWorkspaceSnapshot> GetChatAgentWorkspaceAsync(Guid agentId, Guid? preferredSessionId = null, CancellationToken cancellationToken = default)
            => NotSupported<ChatAgentWorkspaceSnapshot>();

        public Task<ChatSessionRecord> RenameChatSessionAsync(Guid agentId, Guid chatSessionId, string title, CancellationToken cancellationToken = default)
            => NotSupported<ChatSessionRecord>();

        public Task<ExecutionRunResult> ContinueExecutionRunAsync(Guid executionRunId, bool approved, bool autoApprovePendingToolCalls = false, CancellationToken cancellationToken = default)
            => NotSupported<ExecutionRunResult>();

        public Task<AgentChatRunResult> SendMessageAsync(Guid agentId, Guid? chatSessionId, string prompt, CancellationToken cancellationToken = default)
            => NotSupported<AgentChatRunResult>();

        public Task<AgentChatRunResult> RespondToPendingApprovalsAsync(Guid agentId, Guid chatSessionId, bool approved, bool autoApprovePendingToolCalls = false, CancellationToken cancellationToken = default)
            => NotSupported<AgentChatRunResult>();

        public Task<IReadOnlyList<ExecutionLogEntry>> ListExecutionLogAsync(Guid agentId, Guid? chatSessionId = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ExecutionLogEntry>>([]);

        public Task<ChatRuntimeSnapshot> GetChatRuntimeSnapshotAsync(Guid agentId, Guid? chatSessionId = null, CancellationToken cancellationToken = default)
            => NotSupported<ChatRuntimeSnapshot>();

        public Task<IReadOnlyList<AgentRunMetric>> ListMetricsAsync(Guid agentId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AgentRunMetric>>([]);

        public Task<IReadOnlyList<AgentMemoryRecord>> ListMemoryAsync(Guid agentId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AgentMemoryRecord>>([]);

        public Task<Guid> SaveMemoryAsync(MemoryEditorModel model, CancellationToken cancellationToken = default)
            => NotSupported<Guid>();

        public Task DeleteMemoryAsync(Guid memoryId, CancellationToken cancellationToken = default)
            => NotSupported();

        public Task<IReadOnlyList<ExecutionRunRecord>> ListExecutionRunsAsync(ExecutionRunQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ExecutionRunRecord>>([]);

        public Task<ExecutionRunDetail> GetExecutionRunDetailAsync(Guid executionRunId, CancellationToken cancellationToken = default)
            => NotSupported<ExecutionRunDetail>();

        public Task<IReadOnlyList<ExecutionArtifactRecord>> ListExecutionArtifactsAsync(Guid executionRunId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ExecutionArtifactRecord>>([]);

        public Task<IReadOnlyList<ExecutionWorkflowCheckpointRecord>> ListExecutionWorkflowCheckpointsAsync(Guid executionRunId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ExecutionWorkflowCheckpointRecord>>([]);

        public Task<IReadOnlyList<ToolExecutionReceiptRecord>> ListToolExecutionReceiptsAsync(Guid executionRunId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ToolExecutionReceiptRecord>>([]);

        private static Task<T> NotSupported<T>()
            => Task.FromException<T>(new NotSupportedException());

        private static Task NotSupported()
            => Task.FromException(new NotSupportedException());
    }

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }

    private sealed class FakeRecallOrchestrator(Guid projectId) : ICognitiveMemoryRecallOrchestrator
    {
        public List<CognitiveMemoryRecallRequest> Requests { get; } = [];

        public ValueTask<CognitiveMemoryRecallResult> RecallAsync(
            CognitiveMemoryRecallRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            var sourceRef = new CognitiveMemoryRecallSourceRef(
                new CognitiveMemoryRecordId(Guid.NewGuid()),
                new CognitiveMemorySourceItemId(Guid.NewGuid()),
                new CognitiveMemoryEvidenceAnchorId(Guid.NewGuid()),
                "ExternalFile",
                "production-runbook.md",
                "Production deployment runbook source.",
                CognitiveMemoryAccessLevel.Project,
                CognitiveMemoryRedactionState.Safe,
                IncludedInContext: true,
                CognitiveMemoryRecallExclusionReasonKind.None);
            return ValueTask.FromResult(new CognitiveMemoryRecallResult(
                Guid.NewGuid(),
                new CognitiveMemoryRecallContextPack(
                    CognitiveMemoryRecallContextPackId.New(),
                    projectId,
                    null,
                    "Fake recall",
                    "Fake source-backed answer.",
                    [
                        new CognitiveMemoryRecallContextSection(
                            new CognitiveMemorySectionId("selected"),
                            CognitiveMemoryRecallContextSectionKind.SelectedMemory,
                            "Selected memory",
                            "Production deployment must cite the production runbook. Contact lucie@example.test or +420 732 936 929.",
                            [],
                            [],
                            [sourceRef])
                    ],
                    [sourceRef],
                    [],
                    new Dictionary<string, string>()),
                [],
                [],
                []));
        }
    }
}

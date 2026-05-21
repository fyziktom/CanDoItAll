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
        Assert.Equal(CognitiveMemoryProfessorAnchorState.NotProfessorAnchor, persistedCapture.AnchorState);
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
    public async Task CuratorCapture_NaturalProfessorGuidanceCreatesStructuredTemporaryAnchor()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var service = CreateCuratorService(fixture);
        var session = await service.StartAsync(new CognitiveMemoryCuratorSessionStartRequest(
            projectId,
            "Natural professor guidance",
            Policy(projectId),
            CognitiveMemoryCuratorRuntimeMode.DirectLlm));

        var result = await service.RecordTurnAsync(new CognitiveMemoryCuratorTurnCaptureRequest(
            session.Id,
            "In this project, production rollback approval is a release-owner gate because operators often confuse health-check recovery with traffic restoration.",
            "That distinction matters: approval is the gate, health checks are only recovery evidence.",
            CognitiveMemoryCuratorRuntimeMode.DirectLlm));

        var capture = Assert.Single(result.CapturedImprovements);
        Assert.Equal(CognitiveMemoryProfessorAnchorState.Active, capture.AnchorState);
        Assert.Equal(CognitiveMemoryCuratorCaptureKind.NewKnowledge, capture.CaptureKind);
        Assert.Contains("production rollback approval", capture.CaptureScope, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Claim:", capture.Summary, StringComparison.Ordinal);
        Assert.Contains("Target:", capture.Summary, StringComparison.Ordinal);
        Assert.Contains("Misconception:", capture.Summary, StringComparison.Ordinal);
        Assert.InRange(capture.ConfidenceScore, 0.6, 0.95);
    }

    [Fact]
    public async Task CuratorCapture_ExplicitProfessorExamplesAndCounterexamplesCreateAnchor()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var service = CreateCuratorService(fixture);
        var session = await service.StartAsync(new CognitiveMemoryCuratorSessionStartRequest(
            projectId,
            "Explicit professor examples",
            Policy(projectId),
            CognitiveMemoryCuratorRuntimeMode.DirectLlm));

        var result = await service.RecordTurnAsync(new CognitiveMemoryCuratorTurnCaptureRequest(
            session.Id,
            "Store this: Example: production rollback approval is the release-owner gate. Counterexample: health-check recovery is not approval.",
            "Captured as professor teaching because the example and counterexample define the scope.",
            CognitiveMemoryCuratorRuntimeMode.DirectLlm,
            ExplicitCaptureKind: CognitiveMemoryCuratorCaptureKind.NewKnowledge));

        await using var dbContext = fixture.Factory.CreateDbContext();
        var capture = Assert.Single(result.CapturedImprovements);
        var persistedCapture = await dbContext.Set<CognitiveMemoryCuratorCapturedImprovementRecord>().SingleAsync();
        Assert.Equal(CognitiveMemoryProfessorAnchorState.Active, capture.AnchorState);
        Assert.Equal(CognitiveMemoryProfessorAnchorState.Active, persistedCapture.AnchorState);
        Assert.Contains("Example", persistedCapture.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Counterexample", persistedCapture.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Source utterances:", persistedCapture.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SemanticInvariant_CuratorCaptureCzechProfessorTeachingWithoutEnglishKeywordsPreservesDiacritics()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var service = CreateCuratorService(fixture);
        var session = await service.StartAsync(new CognitiveMemoryCuratorSessionStartRequest(
            projectId,
            "Ceske uceni profesora",
            Policy(projectId),
            CognitiveMemoryCuratorRuntimeMode.DirectLlm));

        var result = await service.RecordTurnAsync(new CognitiveMemoryCuratorTurnCaptureRequest(
            session.Id,
            "U nasazení platí: schválení vlastníkem vydání je brána před návratem provozu. Příklad: vlastník vydání podepíše obnovení před provozem. Protipříklad: samotná zdravotní kontrola nestačí.",
            "Rozumím, zachovám to jako dočasné učení profesora.",
            CognitiveMemoryCuratorRuntimeMode.DirectLlm));

        var capture = Assert.Single(result.CapturedImprovements);
        Assert.Equal(CognitiveMemoryProfessorAnchorState.Active, capture.AnchorState);
        Assert.Contains("schválení", capture.Summary, StringComparison.Ordinal);
        Assert.Contains("Příklad", capture.Summary, StringComparison.Ordinal);
        Assert.Contains("Protipříklad", capture.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain("schvaleni", capture.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SemanticInvariant_CuratorCaptureNaturalProfessorQuestionAnswerAndShortCorrectionCreateAnchors()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var service = CreateCuratorService(fixture);
        var session = await service.StartAsync(new CognitiveMemoryCuratorSessionStartRequest(
            projectId,
            "Natural professor Q&A",
            Policy(projectId),
            CognitiveMemoryCuratorRuntimeMode.DirectLlm));
        await service.RecordTurnAsync(new CognitiveMemoryCuratorTurnCaptureRequest(
            session.Id,
            "Should rollback approval wait for health-check recovery?",
            "No. Approval is the release-owner gate; health-check recovery is only evidence.",
            CognitiveMemoryCuratorRuntimeMode.DirectLlm));

        var correction = await service.RecordTurnAsync(new CognitiveMemoryCuratorTurnCaptureRequest(
            session.Id,
            "No: release-owner gate, not health.",
            "Captured as temporary professor guidance.",
            CognitiveMemoryCuratorRuntimeMode.DirectLlm));

        var capture = Assert.Single(correction.CapturedImprovements);
        Assert.Equal(CognitiveMemoryProfessorAnchorState.Active, capture.AnchorState);
        Assert.Equal(CognitiveMemoryCuratorCaptureKind.NewKnowledge, capture.CaptureKind);
        Assert.Contains("release-owner", capture.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("health", capture.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SemanticInvariant_AcceptedUseSignalHasProductionOutcomeEventHandlerAndScheduledAssimilation()
    {
        var moduleSource = ReadRepositoryFiles("src", "CanDoItAll.Modules.CognitiveMemory");
        var scheduledAutomationSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.CognitiveMemory",
            "Operations",
            "CognitiveMemoryScheduledAutomationRunner.cs");

        Assert.Contains("ICognitiveMemoryProfessorAcceptedUseSignalEmitter", moduleSource, StringComparison.Ordinal);
        Assert.Contains("CognitiveMemoryProfessorAcceptedUseSignalRequest", moduleSource, StringComparison.Ordinal);
        Assert.Contains("ICognitiveMemoryRecallOutcomeAcceptedEventHandler", moduleSource, StringComparison.Ordinal);
        Assert.Contains("CognitiveMemoryRecallOutcomeAcceptedEvent", moduleSource, StringComparison.Ordinal);
        Assert.Contains("HandleAsync", moduleSource, StringComparison.Ordinal);
        Assert.Contains("SignalKind = CognitiveMemorySignalKind.ProfessorAnchorAcceptedUse", moduleSource, StringComparison.Ordinal);
        Assert.Contains("SourceKind = CognitiveMemorySignalSourceKind.RecallTrace", moduleSource, StringComparison.Ordinal);
        Assert.Contains("ICognitiveMemoryProfessorAnchorService", scheduledAutomationSource, StringComparison.Ordinal);
        Assert.Contains("ScanAssimilationAsync", scheduledAutomationSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AcceptedUseEmitter_PublishesRecallTraceSignalAndRejectsDirectCaptureMemory()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var service = CreateCuratorService(fixture);
        var session = await service.StartAsync(new CognitiveMemoryCuratorSessionStartRequest(
            projectId,
            "Accepted use emitter chat",
            Policy(projectId),
            CognitiveMemoryCuratorRuntimeMode.DirectLlm));
        var result = await service.RecordTurnAsync(new CognitiveMemoryCuratorTurnCaptureRequest(
            session.Id,
            "Remember that rollback restore needs signed release-owner approval.",
            "I will retain that as temporary professor guidance.",
            CognitiveMemoryCuratorRuntimeMode.DirectLlm,
            ExplicitCaptureKind: CognitiveMemoryCuratorCaptureKind.NewKnowledge));
        var capture = Assert.Single(result.CapturedImprovements);
        var derivedMemoryId = await SeedDerivedProfessorMemoryAsync(
            fixture,
            projectId,
            capture,
            "Rollback restore requires signed release-owner approval before traffic returns.");
        var recallUse = await SeedSynthesizedRecallUseAsync(fixture, projectId, derivedMemoryId, capture.EvidenceAnchorId!.Value);
        var signalLedger = CreateSignalLedger(fixture);
        var anchorService = new CognitiveMemoryProfessorAnchorService(fixture.Factory, fixture.Clock);
        var emitter = new CognitiveMemoryProfessorAcceptedUseSignalEmitter(fixture.Factory, signalLedger, anchorService);

        var emitted = await emitter.EmitAsync(new CognitiveMemoryProfessorAcceptedUseSignalRequest(
            projectId,
            Policy(projectId).ActorId,
            Policy(projectId),
            recallUse.RecallTraceId,
            recallUse.SynthesisId,
            recallUse.StatementId,
            new CognitiveMemoryRecordId(derivedMemoryId),
            recallUse.AcceptedOutcomeId,
            "Workflow answer was accepted by the operator."));

        var directError = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await emitter.EmitAsync(new CognitiveMemoryProfessorAcceptedUseSignalRequest(
                projectId,
                Policy(projectId).ActorId,
                Policy(projectId),
                recallUse.RecallTraceId,
                recallUse.SynthesisId,
                recallUse.StatementId,
                new CognitiveMemoryRecordId(capture.AppliedMemoryRecordId!.Value),
                Guid.NewGuid(),
                "Direct capture memory cannot be counted as mastery.")));

        await using var dbContext = fixture.Factory.CreateDbContext();
        var signal = await dbContext.Set<CognitiveMemorySignalRecord>()
            .SingleAsync(item => item.Id == emitted.Signal.Id);
        var metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(signal.MetadataJson)!;
        Assert.Equal(CognitiveMemorySignalKind.ProfessorAnchorAcceptedUse, signal.SignalKind);
        Assert.Equal(CognitiveMemorySignalSourceKind.RecallTrace, signal.SourceKind);
        Assert.Equal(derivedMemoryId, signal.MemoryRecordId);
        Assert.False(signal.RequiresReview);
        Assert.Equal(recallUse.AcceptedOutcomeId.ToString("D"), metadata["acceptedOutcomeId"]);
        Assert.Contains("direct memory", directError.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AcceptedUseOutcomeEventHandler_EmitsAcceptedUseSignalIdempotentlyAndRejectsBroadLineage()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var service = CreateCuratorService(fixture);
        var session = await service.StartAsync(new CognitiveMemoryCuratorSessionStartRequest(
            projectId,
            "Accepted outcome event chat",
            Policy(projectId),
            CognitiveMemoryCuratorRuntimeMode.DirectLlm));
        var result = await service.RecordTurnAsync(new CognitiveMemoryCuratorTurnCaptureRequest(
            session.Id,
            "Remember that rollback restore needs signed release-owner approval.",
            "I will retain that as temporary professor guidance.",
            CognitiveMemoryCuratorRuntimeMode.DirectLlm,
            ExplicitCaptureKind: CognitiveMemoryCuratorCaptureKind.NewKnowledge));
        var capture = Assert.Single(result.CapturedImprovements);
        var derivedMemoryId = await SeedDerivedProfessorMemoryAsync(
            fixture,
            projectId,
            capture,
            "Rollback restore requires signed release-owner approval before traffic returns.");
        var recallUse = await SeedSynthesizedRecallUseAsync(fixture, projectId, derivedMemoryId, capture.EvidenceAnchorId!.Value);
        var broadRecallUse = await SeedSynthesizedRecallUseAsync(fixture, projectId, derivedMemoryId, capture.EvidenceAnchorId.Value);
        await using (var dbContext = fixture.Factory.CreateDbContext())
        {
            var broadMap = await dbContext.Set<CognitiveMemorySynthesizedStatementSourceMapRecord>()
                .SingleAsync(sourceMap => sourceMap.StatementId == broadRecallUse.StatementId.Value);
            broadMap.EvidenceAnchorId = null;
            await dbContext.SaveChangesAsync();
        }

        var signalLedger = CreateSignalLedger(fixture);
        var anchorService = new CognitiveMemoryProfessorAnchorService(fixture.Factory, fixture.Clock);
        var emitter = new CognitiveMemoryProfessorAcceptedUseSignalEmitter(fixture.Factory, signalLedger, anchorService);
        var handler = new CognitiveMemoryRecallOutcomeAcceptedEventHandler(fixture.Factory, emitter);
        var acceptedEvent = new CognitiveMemoryRecallOutcomeAcceptedEvent(
            projectId,
            Policy(projectId).ActorId,
            Policy(projectId),
            recallUse.RecallTraceId,
            recallUse.SynthesisId,
            recallUse.StatementId,
            new CognitiveMemoryRecordId(derivedMemoryId),
            recallUse.AcceptedOutcomeId,
            "Workflow answer was accepted by the operator.");

        var first = await handler.HandleAsync(acceptedEvent);
        var second = await handler.HandleAsync(acceptedEvent);
        var broadError = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await handler.HandleAsync(new CognitiveMemoryRecallOutcomeAcceptedEvent(
                projectId,
                Policy(projectId).ActorId,
                Policy(projectId),
                broadRecallUse.RecallTraceId,
                broadRecallUse.SynthesisId,
                broadRecallUse.StatementId,
                new CognitiveMemoryRecordId(derivedMemoryId),
                broadRecallUse.AcceptedOutcomeId,
                "Broad lineage must not count as accepted use.")));

        await using var assertContext = fixture.Factory.CreateDbContext();
        Assert.True(first.AcceptedUseSignalEmitted);
        Assert.False(second.AcceptedUseSignalEmitted);
        Assert.Equal(first.Signal.Id, second.Signal.Id);
        Assert.Equal(1, await assertContext.Set<CognitiveMemorySignalRecord>()
            .CountAsync(signal =>
                signal.SignalKind == CognitiveMemorySignalKind.ProfessorAnchorAcceptedUse &&
                signal.MemoryRecordId == derivedMemoryId));
        Assert.Contains("broad recall lineage", broadError.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProfessorLearningLifecycle_EnglishCaptureReviewAcceptedUseAssimilatesAndResolvesReferences()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var curator = CreateCuratorService(fixture);
        var session = await curator.StartAsync(new CognitiveMemoryCuratorSessionStartRequest(
            projectId,
            "End-to-end English professor lifecycle",
            Policy(projectId),
            CognitiveMemoryCuratorRuntimeMode.DirectLlm));
        await curator.RecordTurnAsync(new CognitiveMemoryCuratorTurnCaptureRequest(
            session.Id,
            "Why is that the wrong scope for the rollback answer?",
            "Because the wrong scope is saying that rollback approval comes from the health check; release-owner approval is the source of truth.",
            CognitiveMemoryCuratorRuntimeMode.DirectLlm));
        var captureResult = await curator.RecordTurnAsync(new CognitiveMemoryCuratorTurnCaptureRequest(
            session.Id,
            "Can you give an example and counterexample?",
            "Example: the release-owner approves rollback before production traffic returns. Counterexample: a health check alone is not approval.",
            CognitiveMemoryCuratorRuntimeMode.DirectLlm));
        var capture = Assert.Single(captureResult.CapturedImprovements);
        var derivedMemoryId = await SeedDerivedProfessorMemoryAsync(
            fixture,
            projectId,
            capture,
            "Rollback restoration requires release-owner approval before traffic returns.",
            independentContent: "Independent release audit confirms release-owner approval is required before production traffic returns.");
        await SeedDreamIntegrationForDerivedMemoryAsync(
            fixture,
            projectId,
            capture,
            derivedMemoryId,
            includeIndependentSourceMap: true);

        await using (var dbContext = fixture.Factory.CreateDbContext())
        {
            var comparingCapture = await dbContext.Set<CognitiveMemoryCuratorCapturedImprovementRecord>().SingleAsync(item => item.Id == capture.Id);
            comparingCapture.AnchorState = CognitiveMemoryProfessorAnchorState.Comparing;
            await dbContext.SaveChangesAsync();
        }

        var reviewService = new CognitiveMemoryProfessorReviewService(fixture.Factory, fixture.ScoreDriver, fixture.Clock);
        var reviewResolution = await reviewService.ResolveComparisonAsync(new CognitiveMemoryProfessorComparisonReviewResolutionRequest(
            capture.Id,
            CognitiveMemoryProfessorComparisonReviewOutcome.RejectComparisonReturnActive,
            Policy(projectId).ActorId,
            "Keep the professor anchor active until accepted-use evidence proves the derived memory."));
        var signalLedger = CreateSignalLedger(fixture);
        var anchorService = new CognitiveMemoryProfessorAnchorService(fixture.Factory, fixture.Clock);
        var emitter = new CognitiveMemoryProfessorAcceptedUseSignalEmitter(fixture.Factory, signalLedger, anchorService);
        var firstUse = await SeedSynthesizedRecallUseAsync(fixture, projectId, derivedMemoryId, capture.EvidenceAnchorId!.Value);
        var secondUse = await SeedSynthesizedRecallUseAsync(fixture, projectId, derivedMemoryId, capture.EvidenceAnchorId.Value);

        await emitter.EmitAsync(new CognitiveMemoryProfessorAcceptedUseSignalRequest(
            projectId,
            Policy(projectId).ActorId,
            Policy(projectId),
            firstUse.RecallTraceId,
            firstUse.SynthesisId,
            firstUse.StatementId,
            new CognitiveMemoryRecordId(derivedMemoryId),
            firstUse.AcceptedOutcomeId,
            "First accepted rollout answer used the derived professor memory."));
        var finalEmission = await emitter.EmitAsync(new CognitiveMemoryProfessorAcceptedUseSignalRequest(
            projectId,
            Policy(projectId).ActorId,
            Policy(projectId),
            secondUse.RecallTraceId,
            secondUse.SynthesisId,
            secondUse.StatementId,
            new CognitiveMemoryRecordId(derivedMemoryId),
            secondUse.AcceptedOutcomeId,
            "Second accepted rollout answer used the derived professor memory."));
        var resolver = new CognitiveMemoryReferenceResolver(fixture.Factory);
        var references = await resolver.ResolveAsync(new CognitiveMemoryReferenceResolverRequest(secondUse.StatementId, Policy(projectId)));

        await using var assertContext = fixture.Factory.CreateDbContext();
        var persistedCapture = await assertContext.Set<CognitiveMemoryCuratorCapturedImprovementRecord>().SingleAsync(item => item.Id == capture.Id);
        var acceptedUseCount = await assertContext.Set<CognitiveMemorySignalRecord>()
            .CountAsync(signal =>
                signal.SignalKind == CognitiveMemorySignalKind.ProfessorAnchorAcceptedUse &&
                signal.MemoryRecordId == derivedMemoryId);
        Assert.Equal(CognitiveMemoryProfessorAnchorState.Active, reviewResolution.AnchorState);
        Assert.Equal(CognitiveMemoryProfessorAnchorState.Faded, persistedCapture.AnchorState);
        Assert.Equal(derivedMemoryId, persistedCapture.AssimilatedMemoryRecordId);
        Assert.Equal(2, acceptedUseCount);
        Assert.Contains(finalEmission.AssimilationResults, result => result.CaptureId == capture.Id && result.AnchorState == CognitiveMemoryProfessorAnchorState.Faded);
        var reference = Assert.Single(references.References, item => item.MemoryRecordId.Value == derivedMemoryId);
        Assert.Equal(capture.EvidenceAnchorId.Value, reference.EvidenceAnchorId!.Value.Value);
        Assert.Contains("curator-session", reference.Locator, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SemanticInvariant_ProfessorComparisonReviewResolutionIsExplicitAndAudited()
    {
        var advancedContracts = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.CognitiveMemory",
            "Advanced",
            "CognitiveMemoryAdvancedContracts.cs");
        var reviewService = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.CognitiveMemory",
            "Advanced",
            "CognitiveMemoryProfessorReviewService.cs");

        Assert.Contains("CognitiveMemoryProfessorComparisonReviewOutcome", advancedContracts, StringComparison.Ordinal);
        Assert.Contains("ResolveComparisonAsync", advancedContracts, StringComparison.Ordinal);
        Assert.Contains("ResolveComparisonAsync", reviewService, StringComparison.Ordinal);
        Assert.Contains("ProfessorAnchorLifecycleTransition", reviewService, StringComparison.Ordinal);
        Assert.Contains("Comparing", reviewService, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProfessorComparisonReviewResolution_ReturnsComparingAnchorToActiveAndAuditsTransition()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var curator = CreateCuratorService(fixture);
        var session = await curator.StartAsync(new CognitiveMemoryCuratorSessionStartRequest(
            projectId,
            "Professor comparison resolver chat",
            Policy(projectId),
            CognitiveMemoryCuratorRuntimeMode.DirectLlm));
        var result = await curator.RecordTurnAsync(new CognitiveMemoryCuratorTurnCaptureRequest(
            session.Id,
            "Remember that restore requires release-owner approval before traffic returns.",
            "I will retain that as temporary professor guidance.",
            CognitiveMemoryCuratorRuntimeMode.DirectLlm,
            ExplicitCaptureKind: CognitiveMemoryCuratorCaptureKind.NewKnowledge));
        var capture = Assert.Single(result.CapturedImprovements);
        await using (var dbContext = fixture.Factory.CreateDbContext())
        {
            var persisted = await dbContext.Set<CognitiveMemoryCuratorCapturedImprovementRecord>().SingleAsync(item => item.Id == capture.Id);
            persisted.AnchorState = CognitiveMemoryProfessorAnchorState.Comparing;
            await dbContext.SaveChangesAsync();
        }

        var reviewService = new CognitiveMemoryProfessorReviewService(fixture.Factory, fixture.ScoreDriver, fixture.Clock);
        var resolved = await reviewService.ResolveComparisonAsync(new CognitiveMemoryProfessorComparisonReviewResolutionRequest(
            capture.Id,
            CognitiveMemoryProfessorComparisonReviewOutcome.RejectComparisonReturnActive,
            Policy(projectId).ActorId,
            "Aggregate comparison needs more production support before replacing the professor anchor."));

        await using var assertContext = fixture.Factory.CreateDbContext();
        var persistedCapture = await assertContext.Set<CognitiveMemoryCuratorCapturedImprovementRecord>().SingleAsync(item => item.Id == capture.Id);
        var auditSignals = await assertContext.Set<CognitiveMemorySignalRecord>()
            .Where(signal => signal.SignalKind == CognitiveMemorySignalKind.ProfessorAnchorLifecycleTransition)
            .ToListAsync();
        var auditSignal = Assert.Single(auditSignals, signal => signal.MetadataJson.Contains(capture.Id.ToString("D"), StringComparison.OrdinalIgnoreCase));
        Assert.Equal(CognitiveMemoryProfessorAnchorState.Active, resolved.AnchorState);
        Assert.Equal(CognitiveMemoryProfessorAnchorState.Active, persistedCapture.AnchorState);
        Assert.Contains("Comparing", auditSignal.MetadataJson, StringComparison.Ordinal);
        Assert.Contains("Active", auditSignal.MetadataJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SemanticInvariant_CuratorCaptureEnglishQuestionAnswerAndNaturalScopeCreatesProfessorAnchor()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var service = CreateCuratorService(fixture);
        var session = await service.StartAsync(new CognitiveMemoryCuratorSessionStartRequest(
            projectId,
            "English professor question-answer chat",
            Policy(projectId),
            CognitiveMemoryCuratorRuntimeMode.DirectLlm));

        await service.RecordTurnAsync(new CognitiveMemoryCuratorTurnCaptureRequest(
            session.Id,
            "Why is that the wrong scope for the previous deployment answer?",
            "Because the wrong scope is saying that rollback approval comes from the health check; release-owner approval is the source of truth.",
            CognitiveMemoryCuratorRuntimeMode.DirectLlm));
        var result = await service.RecordTurnAsync(new CognitiveMemoryCuratorTurnCaptureRequest(
            session.Id,
            "Can you give an example and counterexample?",
            "Example: the release-owner approves rollback before production traffic returns. Counterexample: a health check alone is not approval.",
            CognitiveMemoryCuratorRuntimeMode.DirectLlm));

        var capture = Assert.Single(result.CapturedImprovements);
        Assert.Equal(CognitiveMemoryProfessorAnchorState.Active, capture.AnchorState);
        Assert.Contains("Example", capture.Summary, StringComparison.Ordinal);
        Assert.Contains("Counterexample", capture.Summary, StringComparison.Ordinal);
        Assert.Contains("wrong scope", capture.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProfessorAnchor_AssimilatesAndFadesOnlyAfterDerivedMemoryExists()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var service = CreateCuratorService(fixture);
        var session = await service.StartAsync(new CognitiveMemoryCuratorSessionStartRequest(
            projectId,
            "Professor anchor chat",
            Policy(projectId),
            CognitiveMemoryCuratorRuntimeMode.DirectLlm));
        var result = await service.RecordTurnAsync(new CognitiveMemoryCuratorTurnCaptureRequest(
            session.Id,
            "Remember that production rollback needs signed release-owner approval.",
            "I will retain that as trusted project knowledge.",
            CognitiveMemoryCuratorRuntimeMode.DirectLlm,
            ExplicitCaptureKind: CognitiveMemoryCuratorCaptureKind.NewKnowledge));
        var capture = Assert.Single(result.CapturedImprovements);
        var anchorService = new CognitiveMemoryProfessorAnchorService(fixture.Factory, fixture.Clock);
        var derivedMemoryId = await SeedDerivedProfessorMemoryAsync(
            fixture,
            projectId,
            capture,
            "Production rollback approval is independently reinforced by release audit evidence.");
        await SeedAcceptedProfessorUseEventsAsync(fixture, projectId, derivedMemoryId, useCount: 1);

        var fadeError = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await anchorService.FadeAsync(capture.Id));
        var assimilated = await anchorService.MarkAssimilatedAsync(new CognitiveMemoryProfessorAnchorAssimilationRequest(
            capture.Id,
            new CognitiveMemoryRecordId(derivedMemoryId),
            ManualReviewConfirmed: true));
        var faded = await anchorService.FadeAsync(capture.Id);

        await using var dbContext = fixture.Factory.CreateDbContext();
        var persistedCapture = await dbContext.Set<CognitiveMemoryCuratorCapturedImprovementRecord>().SingleAsync(item => item.Id == capture.Id);
        Assert.Contains("cannot fade before assimilation", fadeError.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(CognitiveMemoryProfessorAnchorState.Assimilated, assimilated.AnchorState);
        Assert.Equal(CognitiveMemoryProfessorAnchorState.Faded, faded.AnchorState);
        Assert.Equal(CognitiveMemoryProfessorAnchorState.Faded, persistedCapture.AnchorState);
        Assert.Equal(derivedMemoryId, persistedCapture.AssimilatedMemoryRecordId);
        Assert.NotNull(persistedCapture.AnchorRetiredAtUtc);
    }

    [Fact]
    public async Task ProfessorAnchor_AssimilationRequiresMasteryEvidenceBeyondIndependentSupport()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var service = CreateCuratorService(fixture);
        var session = await service.StartAsync(new CognitiveMemoryCuratorSessionStartRequest(
            projectId,
            "Professor mastery gate chat",
            Policy(projectId),
            CognitiveMemoryCuratorRuntimeMode.DirectLlm));
        var result = await service.RecordTurnAsync(new CognitiveMemoryCuratorTurnCaptureRequest(
            session.Id,
            "Remember that production rollback requires signed release-owner approval before traffic is restored.",
            "I will retain that as temporary professor guidance.",
            CognitiveMemoryCuratorRuntimeMode.DirectLlm,
            ExplicitCaptureKind: CognitiveMemoryCuratorCaptureKind.NewKnowledge));
        var capture = Assert.Single(result.CapturedImprovements);
        var anchorService = new CognitiveMemoryProfessorAnchorService(fixture.Factory, fixture.Clock);
        var derivedMemoryId = await SeedDerivedProfessorMemoryAsync(
            fixture,
            projectId,
            capture,
            "Release-owner approval gate has independent support but has not yet been mastered by repeated use.");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await anchorService.MarkAssimilatedAsync(new CognitiveMemoryProfessorAnchorAssimilationRequest(
                capture.Id,
                new CognitiveMemoryRecordId(derivedMemoryId))));

        Assert.Contains("accepted-use", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProfessorAnchor_ManualAssimilationRequiresReviewConfirmation()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var service = CreateCuratorService(fixture);
        var session = await service.StartAsync(new CognitiveMemoryCuratorSessionStartRequest(
            projectId,
            "Professor manual review gate chat",
            Policy(projectId),
            CognitiveMemoryCuratorRuntimeMode.DirectLlm));
        var result = await service.RecordTurnAsync(new CognitiveMemoryCuratorTurnCaptureRequest(
            session.Id,
            "Remember that production rollback requires signed release-owner approval before traffic is restored.",
            "I will retain that as temporary professor guidance.",
            CognitiveMemoryCuratorRuntimeMode.DirectLlm,
            ExplicitCaptureKind: CognitiveMemoryCuratorCaptureKind.NewKnowledge));
        var capture = Assert.Single(result.CapturedImprovements);
        var derivedMemoryId = await SeedDerivedProfessorMemoryAsync(
            fixture,
            projectId,
            capture,
            "Release-owner approval gate is supported by accepted use evidence.",
            independentContent: "Independent release audit confirms release-owner approval before traffic restoration.");
        await SeedAcceptedProfessorUseEventsAsync(fixture, projectId, derivedMemoryId, useCount: 1);
        var anchorService = new CognitiveMemoryProfessorAnchorService(fixture.Factory, fixture.Clock);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await anchorService.MarkAssimilatedAsync(new CognitiveMemoryProfessorAnchorAssimilationRequest(
                capture.Id,
                new CognitiveMemoryRecordId(derivedMemoryId))));

        await using var dbContext = fixture.Factory.CreateDbContext();
        var persistedCapture = await dbContext.Set<CognitiveMemoryCuratorCapturedImprovementRecord>().SingleAsync(item => item.Id == capture.Id);
        Assert.Contains("review confirmation", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(CognitiveMemoryProfessorAnchorState.Active, persistedCapture.AnchorState);
        Assert.Null(persistedCapture.AssimilatedMemoryRecordId);
    }

    [Fact]
    public async Task ProfessorAnchor_DirectCaptureMemoryCannotAssimilateItsOwnAnchor()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var service = CreateCuratorService(fixture);
        var session = await service.StartAsync(new CognitiveMemoryCuratorSessionStartRequest(
            projectId,
            "Professor direct-proof guard chat",
            Policy(projectId),
            CognitiveMemoryCuratorRuntimeMode.DirectLlm));
        var result = await service.RecordTurnAsync(new CognitiveMemoryCuratorTurnCaptureRequest(
            session.Id,
            "Remember that production rollback needs signed release-owner approval.",
            "I will retain that as trusted project knowledge.",
            CognitiveMemoryCuratorRuntimeMode.DirectLlm,
            ExplicitCaptureKind: CognitiveMemoryCuratorCaptureKind.NewKnowledge));
        var capture = Assert.Single(result.CapturedImprovements);
        var anchorService = new CognitiveMemoryProfessorAnchorService(fixture.Factory, fixture.Clock);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await anchorService.MarkAssimilatedAsync(new CognitiveMemoryProfessorAnchorAssimilationRequest(
                capture.Id,
                new CognitiveMemoryRecordId(capture.AppliedMemoryRecordId!.Value))));

        Assert.Contains("direct capture", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProfessorAnchor_RejectsDescendantOnlyAggregateSupport()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var service = CreateCuratorService(fixture);
        var session = await service.StartAsync(new CognitiveMemoryCuratorSessionStartRequest(
            projectId,
            "Professor descendant-only support chat",
            Policy(projectId),
            CognitiveMemoryCuratorRuntimeMode.DirectLlm));
        var result = await service.RecordTurnAsync(new CognitiveMemoryCuratorTurnCaptureRequest(
            session.Id,
            "Remember that production rollback requires signed release-owner approval before traffic is restored.",
            "I will retain that as temporary professor guidance.",
            CognitiveMemoryCuratorRuntimeMode.DirectLlm,
            ExplicitCaptureKind: CognitiveMemoryCuratorCaptureKind.NewKnowledge));
        var capture = Assert.Single(result.CapturedImprovements);
        var derivedMemoryId = await SeedDerivedProfessorMemoryAsync(
            fixture,
            projectId,
            capture,
            "Release-owner approval gate is internalized by repeated use.",
            independentContent: "Independent release audit exists, but the aggregate fixture below only descends from the professor anchor.");
        await SeedDreamIntegrationForDerivedMemoryAsync(
            fixture,
            projectId,
            capture,
            derivedMemoryId,
            includeIndependentSourceMap: false);
        var anchorService = new CognitiveMemoryProfessorAnchorService(fixture.Factory, fixture.Clock);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await anchorService.MarkAssimilatedAsync(new CognitiveMemoryProfessorAnchorAssimilationRequest(
                capture.Id,
                new CognitiveMemoryRecordId(derivedMemoryId))));

        Assert.Contains("independent non-descendant support", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProfessorAnchor_FadeDemotesDirectCaptureMemory()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var service = CreateCuratorService(fixture);
        var session = await service.StartAsync(new CognitiveMemoryCuratorSessionStartRequest(
            projectId,
            "Professor fade direct quote chat",
            Policy(projectId),
            CognitiveMemoryCuratorRuntimeMode.DirectLlm));
        var result = await service.RecordTurnAsync(new CognitiveMemoryCuratorTurnCaptureRequest(
            session.Id,
            "Remember that production rollback needs signed release-owner approval.",
            "I will retain that as trusted project knowledge.",
            CognitiveMemoryCuratorRuntimeMode.DirectLlm,
            ExplicitCaptureKind: CognitiveMemoryCuratorCaptureKind.NewKnowledge));
        var capture = Assert.Single(result.CapturedImprovements);
        var derivedMemoryId = await SeedDerivedProfessorMemoryAsync(
            fixture,
            projectId,
            capture,
            "Production rollback approval is independently reinforced by release audit evidence.");
        await SeedAcceptedProfessorUseEventsAsync(fixture, projectId, derivedMemoryId, useCount: 1);
        var anchorService = new CognitiveMemoryProfessorAnchorService(fixture.Factory, fixture.Clock);
        await anchorService.MarkAssimilatedAsync(new CognitiveMemoryProfessorAnchorAssimilationRequest(
            capture.Id,
            new CognitiveMemoryRecordId(derivedMemoryId),
            ManualReviewConfirmed: true));

        await anchorService.FadeAsync(capture.Id);

        await using var dbContext = fixture.Factory.CreateDbContext();
        var directMemory = await dbContext.Set<CognitiveMemoryRecord>().SingleAsync(record => record.Id == capture.AppliedMemoryRecordId!.Value);
        var directClaim = await dbContext.Set<CognitiveMemoryClaimRecord>().SingleAsync(claim => claim.MemoryRecordId == directMemory.Id);
        Assert.Equal(CognitiveMemoryValidationState.Retired, directMemory.ValidationState);
        Assert.Equal(CognitiveMemoryStabilityState.Deprecated, directMemory.StabilityState);
        Assert.Equal(CognitiveMemoryValidationState.Retired, directClaim.ValidationState);
        Assert.Equal(CognitiveMemoryStabilityState.Deprecated, directClaim.StabilityState);
        Assert.Contains(derivedMemoryId.ToString("D"), directMemory.GeneratedReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProfessorAnchor_ScanAssimilatesAndFadesIntegratedMasteryEvidence()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var service = CreateCuratorService(fixture);
        var session = await service.StartAsync(new CognitiveMemoryCuratorSessionStartRequest(
            projectId,
            "Professor automatic assimilation chat",
            Policy(projectId),
            CognitiveMemoryCuratorRuntimeMode.DirectLlm));
        var result = await service.RecordTurnAsync(new CognitiveMemoryCuratorTurnCaptureRequest(
            session.Id,
            "Remember that production rollback requires signed release-owner approval before traffic is restored.",
            "I will retain that as temporary professor guidance.",
            CognitiveMemoryCuratorRuntimeMode.DirectLlm,
            ExplicitCaptureKind: CognitiveMemoryCuratorCaptureKind.NewKnowledge));
        var capture = Assert.Single(result.CapturedImprovements);
        var derivedMemoryId = await SeedDerivedProfessorMemoryAsync(
            fixture,
            projectId,
            capture,
            "Release-owner approval gate is internalized through repeated successful recall use.",
            independentContent: "Independent release audit confirms release-owner approval before traffic restoration.");
        await SeedDreamIntegrationForDerivedMemoryAsync(
            fixture,
            projectId,
            capture,
            derivedMemoryId,
            includeIndependentSourceMap: true);
        await SeedAcceptedProfessorUseEventsAsync(fixture, projectId, derivedMemoryId, useCount: 2);
        var anchorService = new CognitiveMemoryProfessorAnchorService(fixture.Factory, fixture.Clock);

        var scanResults = await anchorService.ScanAssimilationAsync(new CognitiveMemoryProfessorAnchorAssimilationScanRequest(projectId));

        await using var dbContext = fixture.Factory.CreateDbContext();
        var scanResult = Assert.Single(scanResults);
        var persistedCapture = await dbContext.Set<CognitiveMemoryCuratorCapturedImprovementRecord>().SingleAsync(item => item.Id == capture.Id);
        var directMemory = await dbContext.Set<CognitiveMemoryRecord>().SingleAsync(record => record.Id == capture.AppliedMemoryRecordId!.Value);
        var transitionSignals = await dbContext.Set<CognitiveMemorySignalRecord>()
            .Where(signal => signal.SignalKind == CognitiveMemorySignalKind.ProfessorAnchorLifecycleTransition)
            .ToListAsync();
        Assert.Equal(CognitiveMemoryProfessorAnchorState.Faded, scanResult.AnchorState);
        Assert.Equal(derivedMemoryId, scanResult.DerivedMemoryRecordId?.Value);
        Assert.Equal(CognitiveMemoryProfessorAnchorState.Faded, persistedCapture.AnchorState);
        Assert.Equal(derivedMemoryId, persistedCapture.AssimilatedMemoryRecordId);
        Assert.NotNull(persistedCapture.AnchorRetiredAtUtc);
        Assert.Equal(CognitiveMemoryValidationState.Retired, directMemory.ValidationState);
        Assert.Equal(CognitiveMemoryStabilityState.Deprecated, directMemory.StabilityState);
        Assert.Contains(transitionSignals, signal => signal.Summary.Contains("Active -> Faded", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SemanticInvariant_ProfessorAnchorScanRequiresAcceptedUseEventsInsteadOfSourceMapMentions()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var service = CreateCuratorService(fixture);
        var session = await service.StartAsync(new CognitiveMemoryCuratorSessionStartRequest(
            projectId,
            "Professor source-map-only use chat",
            Policy(projectId),
            CognitiveMemoryCuratorRuntimeMode.DirectLlm));
        var result = await service.RecordTurnAsync(new CognitiveMemoryCuratorTurnCaptureRequest(
            session.Id,
            "Remember that production rollback requires signed release-owner approval before traffic is restored.",
            "I will retain that as temporary professor guidance.",
            CognitiveMemoryCuratorRuntimeMode.DirectLlm,
            ExplicitCaptureKind: CognitiveMemoryCuratorCaptureKind.NewKnowledge));
        var capture = Assert.Single(result.CapturedImprovements);
        var derivedMemoryId = await SeedDerivedProfessorMemoryAsync(
            fixture,
            projectId,
            capture,
            "Release-owner approval gate is internalized through repeated successful recall use.",
            independentContent: "Independent release audit confirms release-owner approval before traffic restoration.");
        await SeedDreamIntegrationForDerivedMemoryAsync(
            fixture,
            projectId,
            capture,
            derivedMemoryId,
            includeIndependentSourceMap: true);
        await SeedProfessorRecallSourceMapMentionsAsync(
            fixture,
            projectId,
            derivedMemoryId,
            CognitiveMemoryQualityAlgorithmOptions.Current.ProfessorLifecycle.RequiredRepeatedUseCount);
        var anchorService = new CognitiveMemoryProfessorAnchorService(fixture.Factory, fixture.Clock);

        var scanResults = await anchorService.ScanAssimilationAsync(new CognitiveMemoryProfessorAnchorAssimilationScanRequest(projectId));

        await using var dbContext = fixture.Factory.CreateDbContext();
        var persistedCapture = await dbContext.Set<CognitiveMemoryCuratorCapturedImprovementRecord>().SingleAsync(item => item.Id == capture.Id);
        Assert.Empty(scanResults);
        Assert.Equal(CognitiveMemoryProfessorAnchorState.Active, persistedCapture.AnchorState);
        Assert.Null(persistedCapture.AssimilatedMemoryRecordId);
        Assert.Null(persistedCapture.AnchorRetiredAtUtc);
    }

    [Fact]
    public async Task ProfessorAnchor_ScanRequiresAggregateReadyIntegration()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var service = CreateCuratorService(fixture);
        var session = await service.StartAsync(new CognitiveMemoryCuratorSessionStartRequest(
            projectId,
            "Professor cluster-only integration chat",
            Policy(projectId),
            CognitiveMemoryCuratorRuntimeMode.DirectLlm));
        var result = await service.RecordTurnAsync(new CognitiveMemoryCuratorTurnCaptureRequest(
            session.Id,
            "Remember that production rollback requires signed release-owner approval before traffic is restored.",
            "I will retain that as temporary professor guidance.",
            CognitiveMemoryCuratorRuntimeMode.DirectLlm,
            ExplicitCaptureKind: CognitiveMemoryCuratorCaptureKind.NewKnowledge));
        var capture = Assert.Single(result.CapturedImprovements);
        var derivedMemoryId = await SeedDerivedProfessorMemoryAsync(
            fixture,
            projectId,
            capture,
            "Release-owner approval gate has accepted use events.",
            independentContent: "Independent release audit confirms release-owner approval before traffic restoration.");
        await SeedAcceptedProfessorUseEventsAsync(
            fixture,
            projectId,
            derivedMemoryId,
            CognitiveMemoryQualityAlgorithmOptions.Current.ProfessorLifecycle.RequiredRepeatedUseCount);
        await SeedNonAggregateReadyClusterMembershipAsync(fixture, projectId, derivedMemoryId);
        var anchorService = new CognitiveMemoryProfessorAnchorService(fixture.Factory, fixture.Clock);

        var scanResults = await anchorService.ScanAssimilationAsync(new CognitiveMemoryProfessorAnchorAssimilationScanRequest(projectId));

        await using var dbContext = fixture.Factory.CreateDbContext();
        var persistedCapture = await dbContext.Set<CognitiveMemoryCuratorCapturedImprovementRecord>().SingleAsync(item => item.Id == capture.Id);
        Assert.Empty(scanResults);
        Assert.Equal(CognitiveMemoryProfessorAnchorState.Active, persistedCapture.AnchorState);
        Assert.Null(persistedCapture.AssimilatedMemoryRecordId);
    }

    [Fact]
    public async Task ProfessorAnchor_ActiveAnchorSourceMovesDreamCandidateToComparisonReview()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var service = CreateCuratorService(fixture);
        var session = await service.StartAsync(new CognitiveMemoryCuratorSessionStartRequest(
            projectId,
            "Professor dream comparison chat",
            Policy(projectId),
            CognitiveMemoryCuratorRuntimeMode.DirectLlm));
        var result = await service.RecordTurnAsync(new CognitiveMemoryCuratorTurnCaptureRequest(
            session.Id,
            "Remember that production rollback needs signed release-owner approval.",
            "I will retain that as trusted project knowledge.",
            CognitiveMemoryCuratorRuntimeMode.DirectLlm,
            ExplicitCaptureKind: CognitiveMemoryCuratorCaptureKind.NewKnowledge));
        var capture = Assert.Single(result.CapturedImprovements);
        Assert.Equal(CognitiveMemoryProfessorAnchorState.Active, capture.AnchorState);
        await SeedLinkedAdvancedMemoryAsync(
            fixture,
            projectId,
            "Rollback audit evidence",
            "Production rollback needs signed release-owner approval and audit packet retention.",
            "Production rollback release-owner approval audit evidence.",
            topicKey: "production.rollback.approval");
        var dream = new CognitiveMemoryDreamConsolidationService(
            fixture.Factory,
            new CognitiveMemoryClusterPlanner(fixture.Factory, fixture.Clock),
            new CognitiveMemoryDreamValidator(fixture.Factory, fixture.Clock),
            fixture.Clock);

        var dreamResult = await dream.RunAsync(new CognitiveMemoryDreamRunRequest(
            projectId,
            CognitiveMemoryConsolidationMode.ProjectNightly,
            CognitiveMemoryConsolidationTriggerKind.Nightly,
            Policy(projectId),
            new CognitiveMemoryIdempotencyKey("dream-professor-anchor-review")));

        await using var dbContext = fixture.Factory.CreateDbContext();
        var persistedCapture = await dbContext.Set<CognitiveMemoryCuratorCapturedImprovementRecord>().SingleAsync(item => item.Id == capture.Id);
        var validations = await dbContext.Set<CognitiveMemoryDreamValidationRecord>().ToListAsync();
        Assert.Contains(dreamResult.AggregateCandidates, candidate => candidate.Status == CognitiveMemoryDreamAggregateCandidateStatus.NeedsHumanReview);
        Assert.Equal(CognitiveMemoryProfessorAnchorState.Comparing, persistedCapture.AnchorState);
        Assert.Contains(validations, validation => validation.IssuesJson.Contains(nameof(CognitiveMemoryDreamValidationIssueKind.WeakEvidence), StringComparison.Ordinal));
    }

    [Fact]
    public async Task ProfessorAnchor_RejectedComparisonReturnsAnchorToActiveWithAudit()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var service = CreateCuratorService(fixture);
        var session = await service.StartAsync(new CognitiveMemoryCuratorSessionStartRequest(
            projectId,
            "Professor rejected comparison chat",
            Policy(projectId),
            CognitiveMemoryCuratorRuntimeMode.DirectLlm));
        var result = await service.RecordTurnAsync(new CognitiveMemoryCuratorTurnCaptureRequest(
            session.Id,
            "Remember that production rollback needs signed release-owner approval.",
            "I will retain that as trusted project knowledge.",
            CognitiveMemoryCuratorRuntimeMode.DirectLlm,
            ExplicitCaptureKind: CognitiveMemoryCuratorCaptureKind.NewKnowledge));
        var capture = Assert.Single(result.CapturedImprovements);
        var candidateId = await SeedRejectedProfessorComparisonCandidateAsync(fixture, projectId, capture);
        var validator = new CognitiveMemoryDreamValidator(fixture.Factory, fixture.Clock);

        var validation = await validator.ValidateAsync(new CognitiveMemoryDreamValidationRequest(
            new CognitiveMemoryDreamAggregateCandidateId(candidateId),
            Policy(projectId)));

        await using var dbContext = fixture.Factory.CreateDbContext();
        var persistedCapture = await dbContext.Set<CognitiveMemoryCuratorCapturedImprovementRecord>().SingleAsync(item => item.Id == capture.Id);
        var transitionSignals = await dbContext.Set<CognitiveMemorySignalRecord>()
            .Where(signal => signal.SignalKind == CognitiveMemorySignalKind.ProfessorAnchorLifecycleTransition)
            .ToListAsync();
        Assert.Equal(CognitiveMemoryDreamValidationDecision.Rejected, validation.Decision);
        Assert.Equal(CognitiveMemoryProfessorAnchorState.Active, persistedCapture.AnchorState);
        Assert.Contains(transitionSignals, signal => signal.Summary.Contains("Active -> Comparing", StringComparison.Ordinal));
        Assert.Contains(transitionSignals, signal => signal.Summary.Contains("Comparing -> Active", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReferenceResolver_ExpandsFadedProfessorAnchorLineage()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var service = CreateCuratorService(fixture);
        var session = await service.StartAsync(new CognitiveMemoryCuratorSessionStartRequest(
            projectId,
            "Professor lineage chat",
            Policy(projectId),
            CognitiveMemoryCuratorRuntimeMode.DirectLlm));
        var result = await service.RecordTurnAsync(new CognitiveMemoryCuratorTurnCaptureRequest(
            session.Id,
            "Remember that production rollback needs signed release-owner approval.",
            "I will retain that as trusted project knowledge.",
            CognitiveMemoryCuratorRuntimeMode.DirectLlm,
            ExplicitCaptureKind: CognitiveMemoryCuratorCaptureKind.NewKnowledge));
        var capture = Assert.Single(result.CapturedImprovements);
        var derivedMemoryId = await SeedDerivedProfessorMemoryAsync(
            fixture,
            projectId,
            capture,
            "Production rollback approval is independently reinforced by release audit evidence.");
        await SeedAcceptedProfessorUseEventsAsync(fixture, projectId, derivedMemoryId, useCount: 1);
        var anchorService = new CognitiveMemoryProfessorAnchorService(fixture.Factory, fixture.Clock);
        await anchorService.MarkAssimilatedAsync(new CognitiveMemoryProfessorAnchorAssimilationRequest(
            capture.Id,
            new CognitiveMemoryRecordId(derivedMemoryId),
            ManualReviewConfirmed: true));
        await anchorService.FadeAsync(capture.Id);
        var statementId = CognitiveMemorySynthesizedStatementId.New();
        await using (var dbContext = fixture.Factory.CreateDbContext())
        {
            var synthesisId = CognitiveMemorySynthesizedRecallId.New();
            dbContext.Add(new CognitiveMemorySynthesizedRecallRecord
            {
                Id = synthesisId.Value,
                ProjectId = projectId,
                RecallTraceId = Guid.NewGuid(),
                Brief = "Production rollback: release-owner approval is required.",
                ReferencesShownByDefault = false,
                StatementCount = 1,
                SourceMapCount = 1,
                CreatedAtUtc = fixture.Clock.GetUtcNow(),
                ConcurrencyToken = Guid.NewGuid()
            });
            dbContext.Add(new CognitiveMemorySynthesizedStatementRecord
            {
                Id = statementId.Value,
                SynthesisId = synthesisId.Value,
                ProjectId = projectId,
                Sequence = 0,
                Text = "Production rollback: release-owner approval is required.",
                CreatedAtUtc = fixture.Clock.GetUtcNow()
            });
            dbContext.Add(new CognitiveMemorySynthesizedStatementSourceMapRecord
            {
                SynthesisId = synthesisId.Value,
                StatementId = statementId.Value,
                ProjectId = projectId,
                MemoryRecordId = derivedMemoryId,
                SourceSystem = "derived-professor-memory",
                Locator = "/derived/professor",
                Summary = "Derived professor memory.",
                AccessLevel = CognitiveMemoryAccessLevel.Project,
                RedactionState = CognitiveMemoryRedactionState.Safe,
                CreatedAtUtc = fixture.Clock.GetUtcNow()
            });
            await dbContext.SaveChangesAsync();
        }

        var resolver = new CognitiveMemoryReferenceResolver(fixture.Factory);
        var references = await resolver.ResolveAsync(new CognitiveMemoryReferenceResolverRequest(statementId, Policy(projectId)));

        Assert.Contains(references.References, reference =>
            reference.SourceItemId == new CognitiveMemorySourceItemId(capture.SourceItemId!.Value) &&
            reference.Summary.Contains("production rollback needs signed release-owner approval", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EndToEndProfessorCorrection_DreamsAssimilatesRecallsAndResolvesLineage()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var oldMemoryId = await SeedLinkedAdvancedMemoryAsync(
            fixture,
            projectId,
            "Rollback timing guess",
            "Production rollback can restore traffic immediately after health checks.",
            "Old rollback note says traffic restore can happen immediately after health checks.",
            "rollback.legacy.timing");
        var unrelatedMemoryId = await SeedLinkedAdvancedMemoryAsync(
            fixture,
            projectId,
            "Coffee machine maintenance",
            "Coffee machine maintenance replaces the filter after two hundred cups.",
            "Coffee maintenance source evidence.",
            "coffee.machine.maintenance");
        var recallTraceId = await SeedRecallTraceWithIncludedMemoryAsync(fixture, projectId, oldMemoryId);
        var curator = CreateCuratorService(fixture);
        var session = await curator.StartAsync(new CognitiveMemoryCuratorSessionStartRequest(
            projectId,
            "End-to-end professor correction",
            Policy(projectId),
            CognitiveMemoryCuratorRuntimeMode.DirectLlm));

        var correction = await curator.RecordTurnAsync(new CognitiveMemoryCuratorTurnCaptureRequest(
            session.Id,
            "In this project, production rollback approval is a release-owner gate because operators confuse health-check recovery with traffic restoration.",
            "Signed release-owner approval must happen before traffic is restored; health checks are only recovery evidence.",
            CognitiveMemoryCuratorRuntimeMode.DirectLlm,
            RecallTraceId: recallTraceId));

        var capture = Assert.Single(correction.CapturedImprovements);
        Assert.NotNull(capture.AppliedMemoryRecordId);
        await using (var dbContext = fixture.Factory.CreateDbContext())
        {
            var oldMemory = await dbContext.Set<CognitiveMemoryRecord>().SingleAsync(record => record.Id == oldMemoryId);
            var persistedCapture = await dbContext.Set<CognitiveMemoryCuratorCapturedImprovementRecord>().SingleAsync(item => item.Id == capture.Id);
            Assert.Equal(CognitiveMemoryValidationState.Approved, oldMemory.ValidationState);
            Assert.Equal(CognitiveMemoryStabilityState.Active, oldMemory.StabilityState);
            Assert.Equal(CognitiveMemoryCuratorCaptureKind.NewKnowledge, persistedCapture.CaptureKind);
            Assert.Equal(CognitiveMemoryProfessorAnchorState.Active, persistedCapture.AnchorState);
            Assert.Equal(CognitiveMemoryCuratorTargetingStatus.Untargeted, persistedCapture.TargetingStatus);
            Assert.Contains("Claim:", persistedCapture.Summary, StringComparison.Ordinal);
            Assert.Contains("Misconception:", persistedCapture.Summary, StringComparison.Ordinal);
        }

        var anchorService = new CognitiveMemoryProfessorAnchorService(fixture.Factory, fixture.Clock);
        var directCaptureError = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await anchorService.MarkAssimilatedAsync(new CognitiveMemoryProfessorAnchorAssimilationRequest(
                capture.Id,
                new CognitiveMemoryRecordId(capture.AppliedMemoryRecordId!.Value))));
        Assert.Contains("direct capture", directCaptureError.Message, StringComparison.OrdinalIgnoreCase);

        await SeedLinkedAdvancedMemoryAsync(
            fixture,
            projectId,
            "Release-owner approval gate evidence",
            "Release-owner approval gate requires signed release-owner approval before traffic restoration.",
            "Release audit evidence says the release-owner approval gate is required before traffic restoration.",
            "release.owner.approval.gate");
        var dream = CreateDreamService(fixture);
        var comparisonDream = await dream.RunAsync(new CognitiveMemoryDreamRunRequest(
            projectId,
            CognitiveMemoryConsolidationMode.ProjectNightly,
            CognitiveMemoryConsolidationTriggerKind.Nightly,
            Policy(projectId),
            new CognitiveMemoryIdempotencyKey("end-to-end-professor-comparison")));

        await using (var dbContext = fixture.Factory.CreateDbContext())
        {
            var persistedCapture = await dbContext.Set<CognitiveMemoryCuratorCapturedImprovementRecord>().SingleAsync(item => item.Id == capture.Id);
            Assert.Contains(comparisonDream.AggregateCandidates, candidate => candidate.Status == CognitiveMemoryDreamAggregateCandidateStatus.NeedsHumanReview);
            Assert.Equal(CognitiveMemoryProfessorAnchorState.Comparing, persistedCapture.AnchorState);
        }

        var derivedMemoryId = await SeedDerivedProfessorMemoryAsync(
            fixture,
            projectId,
            capture,
            "Release-owner approval gate is internalized: signed release-owner approval is required before traffic restoration.",
            title: "Derived professor release-owner approval gate memory",
            topicKey: "release.owner.approval.gate",
            independentContent: "Independent release audit confirms the release-owner approval gate before traffic restoration.",
            origin: CognitiveMemoryRecordOrigin.SourceDerived);
        await SeedDreamIntegrationForDerivedMemoryAsync(
            fixture,
            projectId,
            capture,
            derivedMemoryId,
            includeIndependentSourceMap: true);
        await SeedAcceptedProfessorUseEventsAsync(
            fixture,
            projectId,
            derivedMemoryId,
            CognitiveMemoryQualityAlgorithmOptions.Current.ProfessorLifecycle.RequiredRepeatedUseCount);

        var scanResults = await anchorService.ScanAssimilationAsync(new CognitiveMemoryProfessorAnchorAssimilationScanRequest(projectId));
        var scanResult = Assert.Single(scanResults);
        Assert.Equal(CognitiveMemoryProfessorAnchorState.Faded, scanResult.AnchorState);
        Assert.Equal(new CognitiveMemoryRecordId(derivedMemoryId), scanResult.DerivedMemoryRecordId);

        await using (var dbContext = fixture.Factory.CreateDbContext())
        {
            var persistedCapture = await dbContext.Set<CognitiveMemoryCuratorCapturedImprovementRecord>().SingleAsync(item => item.Id == capture.Id);
            var directCaptureMemory = await dbContext.Set<CognitiveMemoryRecord>().SingleAsync(record => record.Id == capture.AppliedMemoryRecordId!.Value);
            Assert.Equal(CognitiveMemoryProfessorAnchorState.Faded, persistedCapture.AnchorState);
            Assert.Equal(derivedMemoryId, persistedCapture.AssimilatedMemoryRecordId);
            Assert.Equal(CognitiveMemoryValidationState.Retired, directCaptureMemory.ValidationState);
            Assert.Equal(CognitiveMemoryStabilityState.Deprecated, directCaptureMemory.StabilityState);
        }

        var staleReviewDream = await dream.RunAsync(new CognitiveMemoryDreamRunRequest(
            projectId,
            CognitiveMemoryConsolidationMode.ProjectNightly,
            CognitiveMemoryConsolidationTriggerKind.Nightly,
            Policy(projectId),
            new CognitiveMemoryIdempotencyKey("end-to-end-professor-stale-review")));
        Assert.Contains(staleReviewDream.AggregateCandidates, candidate =>
            candidate.Status == CognitiveMemoryDreamAggregateCandidateStatus.Approved &&
            candidate.CanonicalText.Contains("release-owner approval", StringComparison.OrdinalIgnoreCase));

        await using (var dbContext = fixture.Factory.CreateDbContext())
        {
            var oldMemory = await dbContext.Set<CognitiveMemoryRecord>().SingleAsync(record => record.Id == oldMemoryId);
            oldMemory.StabilityState = CognitiveMemoryStabilityState.Deprecated;
            oldMemory.UpdatedAtUtc = fixture.Clock.GetUtcNow();
            oldMemory.ConcurrencyToken = Guid.NewGuid();
            var directCaptureMemory = await dbContext.Set<CognitiveMemoryRecord>().SingleAsync(record => record.Id == capture.AppliedMemoryRecordId!.Value);
            directCaptureMemory.StabilityState = CognitiveMemoryStabilityState.Deprecated;
            directCaptureMemory.UpdatedAtUtc = fixture.Clock.GetUtcNow();
            directCaptureMemory.ConcurrencyToken = Guid.NewGuid();
            await dbContext.SaveChangesAsync();
        }

        var finalDream = await dream.RunAsync(new CognitiveMemoryDreamRunRequest(
            projectId,
            CognitiveMemoryConsolidationMode.ProjectNightly,
            CognitiveMemoryConsolidationTriggerKind.Nightly,
            Policy(projectId),
            new CognitiveMemoryIdempotencyKey("end-to-end-professor-final")));
        var approvedCandidate = finalDream.AggregateCandidates.FirstOrDefault(candidate =>
            candidate.Status == CognitiveMemoryDreamAggregateCandidateStatus.Approved &&
            candidate.CanonicalText.Contains("release-owner approval", StringComparison.OrdinalIgnoreCase));
        var finalCandidateSummary = string.Join(
            " | ",
            finalDream.AggregateCandidates.Select(candidate => $"{candidate.Status}:{candidate.Title}:{candidate.CanonicalText}"));
        var finalValidationSummary = await LoadDreamValidationIssueSummaryAsync(fixture, finalDream.RunId.Value);
        Assert.True(approvedCandidate is not null, $"{finalCandidateSummary} :: {finalValidationSummary}");
        Assert.DoesNotContain("Coffee machine", approvedCandidate!.CanonicalText, StringComparison.OrdinalIgnoreCase);

        await using (var dbContext = fixture.Factory.CreateDbContext())
        {
            var sourceMaps = await dbContext.Set<CognitiveMemoryDreamAggregateClaimSourceMapRecord>()
                .Where(sourceMap => sourceMap.AggregateCandidateId == approvedCandidate.Id.Value)
                .ToListAsync();
            Assert.DoesNotContain(sourceMaps, sourceMap => sourceMap.SourceMemoryRecordId == unrelatedMemoryId);
        }

        var applicator = new CognitiveMemoryAggregateMemoryApplicator(
            fixture.Factory,
            new CognitiveMemoryRecordValidator(),
            fixture.Clock);
        var applied = await applicator.ApplyAsync(new CognitiveMemoryAggregateMemoryApplyRequest(
            approvedCandidate.Id,
            "agent:test",
            Policy(projectId)));

        await using (var dbContext = fixture.Factory.CreateDbContext())
        {
            var aggregateMemory = await dbContext.Set<CognitiveMemoryRecord>().SingleAsync(record => record.Id == applied.MemoryRecordId.Value);
            Assert.Equal(CognitiveMemoryStabilityState.Experimental, aggregateMemory.StabilityState);
            Assert.Equal("quality-aggregate-apply-v3-semantic-calibrated", aggregateMemory.AlgorithmVersion);
        }

        var aggregateRef = await CreateRecallSourceRefAsync(
            fixture,
            applied.MemoryRecordId.Value,
            "Dream aggregate says signed release-owner approval is required before traffic restore.");
        var derivedRef = await CreateRecallSourceRefAsync(
            fixture,
            derivedMemoryId,
            "Professor-derived memory says signed release-owner approval is required before traffic restore.");
        var synthesizedTraceId = await SeedRecallTraceAsync(fixture, projectId);
        var recallResult = new CognitiveMemoryRecallResult(
            synthesizedTraceId,
            new CognitiveMemoryRecallContextPack(
                CognitiveMemoryRecallContextPackId.New(),
                projectId,
                null,
                "What should happen during production rollback approval?",
                "Selected aggregate and professor-derived memories.",
                [
                    new CognitiveMemoryRecallContextSection(
                        new CognitiveMemorySectionId("selected-aggregate"),
                        CognitiveMemoryRecallContextSectionKind.SelectedMemory,
                        "Production rollback aggregate",
                        "Signed release-owner approval is required before traffic is restored.",
                        [applied.MemoryRecordId],
                        [],
                        [aggregateRef]),
                    new CognitiveMemoryRecallContextSection(
                        new CognitiveMemorySectionId("selected-derived-professor"),
                        CognitiveMemoryRecallContextSectionKind.SelectedMemory,
                        "Derived professor rollback approval",
                        "Signed release-owner approval is required before traffic is restored.",
                        [new CognitiveMemoryRecordId(derivedMemoryId)],
                        [],
                        [derivedRef])
                ],
                [aggregateRef, derivedRef],
                [],
                new Dictionary<string, string>()),
            [],
            [],
            []);
        var synthesis = new CognitiveMemoryRecallSynthesisService(fixture.Factory, fixture.Clock);

        var synthesized = await synthesis.SynthesizeAsync(new CognitiveMemoryRecallSynthesisRequest(
            recallResult,
            Policy(projectId),
            MaxStatements: 1));

        Assert.False(synthesized.ReferencesShownByDefault);
        Assert.StartsWith("Production", synthesized.Brief, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("release-owner approval", synthesized.Brief, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("curator-session/", synthesized.Brief, StringComparison.Ordinal);
        var statement = Assert.Single(synthesized.Statements);
        var resolver = new CognitiveMemoryReferenceResolver(fixture.Factory);
        var references = await resolver.ResolveAsync(new CognitiveMemoryReferenceResolverRequest(statement.StatementId, Policy(projectId)));

        Assert.Contains(references.References, reference => reference.MemoryRecordId == applied.MemoryRecordId);
        Assert.Contains(references.References, reference => reference.MemoryRecordId == new CognitiveMemoryRecordId(derivedMemoryId));
        Assert.Contains(references.References, reference => reference.SourceItemId == new CognitiveMemorySourceItemId(capture.SourceItemId!.Value));
        Assert.DoesNotContain(references.References, reference => reference.MemoryRecordId == new CognitiveMemoryRecordId(unrelatedMemoryId));
        Assert.All(references.References, reference => Assert.True(reference.Included));
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
    public async Task CuratorCapture_ExplicitCorrectionTargetWinsOverMultipleRecalledMemories()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var firstMemoryId = await SeedMemoryRecordAsync(fixture, projectId, CognitiveMemoryAccessLevel.Project);
        var targetMemoryId = await SeedMemoryRecordAsync(fixture, projectId, CognitiveMemoryAccessLevel.Project);
        var thirdMemoryId = await SeedMemoryRecordAsync(fixture, projectId, CognitiveMemoryAccessLevel.Project);
        var targetClaimId = await SeedClaimAsync(fixture, projectId, targetMemoryId);
        var recallTraceId = await SeedRecallTraceWithIncludedMemoriesAsync(fixture, projectId, [firstMemoryId, targetMemoryId, thirdMemoryId]);
        var service = CreateCuratorService(fixture);
        var session = await service.StartAsync(new CognitiveMemoryCuratorSessionStartRequest(
            projectId,
            "Explicit curator correction chat",
            Policy(projectId),
            CognitiveMemoryCuratorRuntimeMode.DirectLlm));

        var result = await service.RecordTurnAsync(new CognitiveMemoryCuratorTurnCaptureRequest(
            session.Id,
            "That is not correct. The signed release branch is the production source of truth.",
            "I found several deployment memories.",
            CognitiveMemoryCuratorRuntimeMode.DirectLlm,
            RecallTraceId: recallTraceId,
            ExplicitCaptureKind: CognitiveMemoryCuratorCaptureKind.Correction,
            ExplicitTargetMemoryRecordIds: [new CognitiveMemoryRecordId(targetMemoryId)],
            ExplicitTargetClaimIds: [new CognitiveMemoryClaimId(targetClaimId)],
            TargetConfidenceScore: 0.74,
            CaptureScope: "Project"));

        await using var dbContext = fixture.Factory.CreateDbContext();
        var capture = Assert.Single(result.CapturedImprovements);
        var persistedCapture = await dbContext.Set<CognitiveMemoryCuratorCapturedImprovementRecord>().SingleAsync();
        var mutation = await dbContext.Set<CognitiveMemoryMutationCommandRecord>().SingleAsync();
        var firstMemory = await dbContext.Set<CognitiveMemoryRecord>().SingleAsync(record => record.Id == firstMemoryId);
        var targetMemory = await dbContext.Set<CognitiveMemoryRecord>().SingleAsync(record => record.Id == targetMemoryId);
        var thirdMemory = await dbContext.Set<CognitiveMemoryRecord>().SingleAsync(record => record.Id == thirdMemoryId);
        var capturedAffectedIds = DeserializeGuidList(persistedCapture.AffectedMemoryRecordIdsJson);
        var capturedClaimIds = DeserializeGuidList(persistedCapture.TargetClaimIdsJson);
        var mutationAffectedIds = DeserializeGuidList(mutation.AffectedMemoryRecordIdsJson);
        var mutationClaimIds = DeserializeGuidList(mutation.AffectedClaimIdsJson);

        Assert.Equal(CognitiveMemoryCuratorCaptureStatus.Applied, capture.Status);
        Assert.Equal(CognitiveMemoryCuratorTargetingStatus.ExplicitTarget, persistedCapture.TargetingStatus);
        Assert.Equal(0.74, persistedCapture.TargetConfidenceScore, 3);
        Assert.Equal("Project", persistedCapture.CaptureScope);
        Assert.Contains(targetMemoryId, capturedAffectedIds);
        Assert.DoesNotContain(firstMemoryId, capturedAffectedIds);
        Assert.DoesNotContain(thirdMemoryId, capturedAffectedIds);
        Assert.Contains(targetClaimId, capturedClaimIds);
        Assert.Contains(targetMemoryId, mutationAffectedIds);
        Assert.Contains(targetClaimId, mutationClaimIds);
        Assert.Equal(CognitiveMemoryValidationState.Superseded, targetMemory.ValidationState);
        Assert.Equal(CognitiveMemoryStabilityState.Stale, targetMemory.StabilityState);
        Assert.Equal(CognitiveMemoryValidationState.Approved, firstMemory.ValidationState);
        Assert.Equal(CognitiveMemoryValidationState.Approved, thirdMemory.ValidationState);
    }

    [Fact]
    public async Task CuratorCapture_AmbiguousCorrectionWithMultipleRecallMemoriesCreatesReviewWithoutBroadSupersede()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var firstMemoryId = await SeedMemoryRecordAsync(fixture, projectId, CognitiveMemoryAccessLevel.Project);
        var secondMemoryId = await SeedMemoryRecordAsync(fixture, projectId, CognitiveMemoryAccessLevel.Project);
        var thirdMemoryId = await SeedMemoryRecordAsync(fixture, projectId, CognitiveMemoryAccessLevel.Project);
        var recallTraceId = await SeedRecallTraceWithIncludedMemoriesAsync(fixture, projectId, [firstMemoryId, secondMemoryId, thirdMemoryId]);
        var service = CreateCuratorService(fixture);
        var session = await service.StartAsync(new CognitiveMemoryCuratorSessionStartRequest(
            projectId,
            "Ambiguous curator correction chat",
            Policy(projectId),
            CognitiveMemoryCuratorRuntimeMode.DirectLlm));

        var result = await service.RecordTurnAsync(new CognitiveMemoryCuratorTurnCaptureRequest(
            session.Id,
            "That is not correct. The signed release branch is the production source of truth.",
            "I found several deployment memories.",
            CognitiveMemoryCuratorRuntimeMode.DirectLlm,
            RecallTraceId: recallTraceId,
            ExplicitCaptureKind: CognitiveMemoryCuratorCaptureKind.Correction));

        await using var dbContext = fixture.Factory.CreateDbContext();
        var capture = Assert.Single(result.CapturedImprovements);
        var records = await dbContext.Set<CognitiveMemoryRecord>()
            .Where(record => new[] { firstMemoryId, secondMemoryId, thirdMemoryId }.Contains(record.Id))
            .ToListAsync();

        Assert.Equal(CognitiveMemoryCuratorCaptureStatus.Captured, capture.Status);
        Assert.All(records, record =>
        {
            Assert.Equal(CognitiveMemoryValidationState.Approved, record.ValidationState);
            Assert.Equal(CognitiveMemoryStabilityState.Active, record.StabilityState);
        });
        Assert.Equal(0, await dbContext.Set<CognitiveMemoryRelationRecord>().CountAsync());
        Assert.Equal(0, await dbContext.Set<CognitiveMemoryConsolidationCandidateRecord>().CountAsync());
        Assert.Single(await dbContext.Set<CognitiveMemoryReviewItemRecord>().ToListAsync());
    }

    [Fact]
    public async Task CuratorCapture_EnglishNewKnowledgePhraseIsCapturedDeterministically()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var service = CreateCuratorService(fixture);
        var session = await service.StartAsync(new CognitiveMemoryCuratorSessionStartRequest(
            projectId,
            "English curator chat",
            Policy(projectId),
            CognitiveMemoryCuratorRuntimeMode.DirectLlm));

        var result = await service.RecordTurnAsync(new CognitiveMemoryCuratorTurnCaptureRequest(
            session.Id,
            "Remember that production deployment always requires a signed release branch.",
            "I will store that as project knowledge.",
            CognitiveMemoryCuratorRuntimeMode.DirectLlm));

        await using var dbContext = fixture.Factory.CreateDbContext();
        var capture = Assert.Single(result.CapturedImprovements);
        var memory = await dbContext.Set<CognitiveMemoryRecord>().SingleAsync();

        Assert.Equal(CognitiveMemoryCuratorCaptureKind.NewKnowledge, capture.CaptureKind);
        Assert.Contains("production deployment", memory.CanonicalText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(CognitiveMemoryCuratorCaptureStatus.Applied, capture.Status);
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
    public async Task CuratorSend_ConversationDepthControlsRecallBudgetPromptAndCaptureMetadata()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var providerId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var recall = new FakeRecallOrchestrator(projectId);
        var workspace = new FakeAgentFrameworkWorkspaceService
        {
            DirectResponseText = "Depth-aware curator response."
        };
        var service = CreateCuratorService(
            fixture,
            recall,
            CreateSettingsService(fixture, defaultProviderProfileId: providerId),
            workspace);
        var shortSession = await service.StartAsync(new CognitiveMemoryCuratorSessionStartRequest(
            projectId,
            "Short curator chat",
            Policy(projectId),
            CognitiveMemoryCuratorRuntimeMode.DirectLlm,
            CognitiveMemoryCuratorConversationDepth.Short));
        var longSession = await service.StartAsync(new CognitiveMemoryCuratorSessionStartRequest(
            projectId,
            "Long curator chat",
            Policy(projectId),
            CognitiveMemoryCuratorRuntimeMode.DirectLlm,
            CognitiveMemoryCuratorConversationDepth.Long));

        var shortResult = await service.SendAsync(new CognitiveMemoryCuratorSendRequest(
            shortSession.Id,
            "Remember that the compact review asks only for release blockers.",
            ExplicitCaptureKind: CognitiveMemoryCuratorCaptureKind.NewKnowledge));
        var longResult = await service.SendAsync(new CognitiveMemoryCuratorSendRequest(
            longSession.Id,
            "Remember that the long review should aggregate adjacent release evidence and alternative hypotheses.",
            ExplicitCaptureKind: CognitiveMemoryCuratorCaptureKind.NewKnowledge));

        await using var dbContext = fixture.Factory.CreateDbContext();
        var shortRecall = recall.Requests[0];
        var longRecall = recall.Requests[1];
        var captures = await dbContext.Set<CognitiveMemoryCuratorCapturedImprovementRecord>().ToListAsync();
        var shortCapture = Assert.Single(captures, item => item.Summary.Contains("compact review", StringComparison.Ordinal));
        var longCapture = Assert.Single(captures, item => item.Summary.Contains("long review", StringComparison.Ordinal));

        Assert.Equal(CognitiveMemoryCuratorConversationDepth.Short, shortResult.Turn.ConversationDepth);
        Assert.Equal(CognitiveMemoryCuratorConversationDepth.Long, longResult.Turn.ConversationDepth);
        Assert.True(shortRecall.Budget.ContextCharacterBudget < longRecall.Budget.ContextCharacterBudget);
        Assert.True(shortRecall.Budget.FocusLimit < longRecall.Budget.FocusLimit);
        Assert.Equal("Short", shortRecall.Metadata?["curatorConversationDepth"]);
        Assert.Equal("Long", longRecall.Metadata?["curatorConversationDepth"]);
        Assert.Contains("Keep the response short", workspace.DirectRequests[0].Request.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("Use a detailed response", workspace.DirectRequests[1].Request.SystemPrompt, StringComparison.Ordinal);
        Assert.Equal(CognitiveMemoryCuratorConversationDepth.Short, shortCapture.ConversationDepth);
        Assert.Equal(CognitiveMemoryCuratorConversationDepth.Long, longCapture.ConversationDepth);
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

    private static ICognitiveMemoryDreamConsolidationService CreateDreamService(TestFixture fixture)
    {
        var planner = new CognitiveMemoryClusterPlanner(fixture.Factory, fixture.Clock);
        var validator = new CognitiveMemoryDreamValidator(fixture.Factory, fixture.Clock);
        return new CognitiveMemoryDreamConsolidationService(
            fixture.Factory,
            planner,
            validator,
            fixture.Clock);
    }

    private static async Task<string> LoadDreamValidationIssueSummaryAsync(TestFixture fixture, Guid dreamRunId)
    {
        await using var dbContext = fixture.Factory.CreateDbContext();
        var rows = await dbContext.Set<CognitiveMemoryDreamAggregateCandidateRecord>()
            .Where(candidate => candidate.DreamRunId == dreamRunId)
            .Join(
                dbContext.Set<CognitiveMemoryDreamValidationRecord>(),
                candidate => candidate.ValidationRecordId,
                validation => validation.Id,
                (candidate, validation) => $"{candidate.Status}:{candidate.Title}:{validation.IssuesJson}")
            .ToListAsync();
        return string.Join(" | ", rows);
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

    private static async Task<CognitiveMemoryRecallSourceRef> CreateRecallSourceRefAsync(
        TestFixture fixture,
        Guid memoryRecordId,
        string summary)
    {
        await using var dbContext = fixture.Factory.CreateDbContext();
        var sourceLink = await dbContext.Set<CognitiveMemorySourceLinkRecord>()
            .Where(link => link.MemoryRecordId == memoryRecordId)
            .OrderBy(link => link.SourceItemId)
            .FirstAsync();
        var sourceItem = await dbContext.Set<CognitiveMemorySourceItemRecord>()
            .SingleAsync(item => item.Id == sourceLink.SourceItemId);
        var evidenceAnchor = await dbContext.Set<CognitiveMemoryRecordEvidenceAnchorRecord>()
            .Where(link => link.MemoryRecordId == memoryRecordId)
            .OrderBy(link => link.EvidenceAnchorId)
            .FirstOrDefaultAsync();
        var locator = sourceItem.Locator ?? sourceLink.Locator ?? $"memory:{memoryRecordId:D}";
        return new CognitiveMemoryRecallSourceRef(
            new CognitiveMemoryRecordId(memoryRecordId),
            new CognitiveMemorySourceItemId(sourceItem.Id),
            evidenceAnchor is null ? null : new CognitiveMemoryEvidenceAnchorId(evidenceAnchor.EvidenceAnchorId),
            sourceItem.SourceSystem,
            locator,
            summary,
            sourceItem.AccessLevel,
            sourceItem.RedactionState,
            IncludedInContext: true,
            CognitiveMemoryRecallExclusionReasonKind.None);
    }

    private static async Task<Guid> SeedRecallTraceWithIncludedMemoryAsync(TestFixture fixture, Guid projectId, Guid memoryRecordId)
        => await SeedRecallTraceWithIncludedMemoriesAsync(fixture, projectId, [memoryRecordId]);

    private static async Task<Guid> SeedRecallTraceWithIncludedMemoriesAsync(
        TestFixture fixture,
        Guid projectId,
        IReadOnlyList<Guid> memoryRecordIds)
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
            IncludedRecordCount = memoryRecordIds.Count,
            ConcurrencyToken = Guid.NewGuid()
        };
        var sourceRefs = memoryRecordIds.Select(memoryRecordId => new CognitiveMemoryRecallSourceRefRecord
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
        }).ToArray();
        dbContext.Add(trace);
        dbContext.AddRange(sourceRefs);
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

    private static async Task<Guid> SeedDerivedProfessorMemoryAsync(
        TestFixture fixture,
        Guid projectId,
        CognitiveMemoryCuratorCapturedImprovementRecord capture,
        string canonicalText,
        string title = "Derived professor rollback approval memory",
        string topicKey = "production.rollback.approval",
        string independentContent = "Independent release audit confirms production rollback requires release-owner approval.",
        CognitiveMemoryRecordOrigin origin = CognitiveMemoryRecordOrigin.MachineGenerated)
    {
        await using var dbContext = fixture.Factory.CreateDbContext();
        if (capture.SourceItemId is null || capture.EvidenceAnchorId is null)
        {
            throw new InvalidOperationException("Professor capture test fixture requires source and evidence anchor ids.");
        }

        var anchorSourceItem = await dbContext.Set<CognitiveMemorySourceItemRecord>().SingleAsync(item => item.Id == capture.SourceItemId.Value);
        var independent = await CreateLinkedSourceAsync(
            dbContext,
            fixture,
            projectId,
            "Professor derived audit source",
            independentContent);
        var memory = new CognitiveMemoryRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Kind = CognitiveMemoryRecordKind.Semantic,
            Origin = origin,
            Title = title,
            CanonicalText = canonicalText,
            SummaryText = canonicalText,
            TopicKey = topicKey,
            ValidationState = CognitiveMemoryValidationState.Approved,
            StabilityState = CognitiveMemoryStabilityState.Active,
            CreatedInMode = CognitiveMemoryOperationMode.Consolidate,
            AlgorithmVersion = "unit-test-derived-professor",
            ContentHash = CognitiveMemoryHash.FromUtf8(canonicalText).Value,
            SourceEvidenceCount = 2,
            EvidenceAnchorCount = 2,
            ConfidenceBucket = CognitiveMemoryScoreProjectionBucket.WeakAccept,
            ActivationBucket = CognitiveMemoryScoreProjectionBucket.WeakAccept,
            AccessLevel = CognitiveMemoryAccessLevel.Project,
            RiskLevel = CognitiveMemoryRiskLevel.Low,
            CreatedAtUtc = fixture.Clock.GetUtcNow(),
            UpdatedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        };
        dbContext.AddRange(
            memory,
            new CognitiveMemorySourceLinkRecord
            {
                MemoryRecordId = memory.Id,
                SourceManifestId = anchorSourceItem.SourceManifestId,
                SourceItemId = anchorSourceItem.Id,
                EvidenceRole = CognitiveMemoryEvidenceRole.SupportingSource,
                Locator = anchorSourceItem.Locator,
                Summary = "Professor anchor lineage for rollback approval.",
                CreatedAtUtc = fixture.Clock.GetUtcNow()
            },
            new CognitiveMemorySourceLinkRecord
            {
                MemoryRecordId = memory.Id,
                SourceManifestId = independent.SourceManifestId,
                SourceItemId = independent.SourceItemId,
                EvidenceRole = CognitiveMemoryEvidenceRole.SupportingSource,
                Locator = independent.Locator,
                Summary = independent.Content,
                CreatedAtUtc = fixture.Clock.GetUtcNow()
            },
            new CognitiveMemoryRecordEvidenceAnchorRecord
            {
                MemoryRecordId = memory.Id,
                EvidenceAnchorId = capture.EvidenceAnchorId.Value,
                EvidenceRole = CognitiveMemoryEvidenceRole.SupportingSource,
                Summary = "Professor anchor lineage.",
                CreatedAtUtc = fixture.Clock.GetUtcNow()
            },
            new CognitiveMemoryRecordEvidenceAnchorRecord
            {
                MemoryRecordId = memory.Id,
                EvidenceAnchorId = independent.EvidenceAnchorId,
                EvidenceRole = CognitiveMemoryEvidenceRole.SupportingSource,
                Summary = independent.Content,
                CreatedAtUtc = fixture.Clock.GetUtcNow()
            });
        await dbContext.SaveChangesAsync();
        return memory.Id;
    }

    private static async Task SeedDreamIntegrationForDerivedMemoryAsync(
        TestFixture fixture,
        Guid projectId,
        CognitiveMemoryCuratorCapturedImprovementRecord capture,
        Guid derivedMemoryId,
        bool includeIndependentSourceMap)
    {
        await using var dbContext = fixture.Factory.CreateDbContext();
        var candidateId = Guid.NewGuid();
        var claimId = Guid.NewGuid();
        var dreamRunId = Guid.NewGuid();
        var clusterId = Guid.NewGuid();
        dbContext.Add(new CognitiveMemoryDreamRunRecord
        {
            Id = dreamRunId,
            ProjectId = projectId,
            Mode = CognitiveMemoryConsolidationMode.ProjectNightly,
            TriggerKind = CognitiveMemoryConsolidationTriggerKind.Nightly,
            Status = CognitiveMemoryRunStatus.Succeeded,
            IdempotencyKey = $"unit-test-professor-integration-{candidateId:D}",
            PolicyProfileId = Policy(projectId).PolicyProfileId.Value,
            AlgorithmVersion = "unit-test-professor-integration",
            AggregateCandidatesCreated = 1,
            ApprovedCandidates = 1,
            StartedAtUtc = fixture.Clock.GetUtcNow(),
            CompletedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        });
        dbContext.Add(new CognitiveMemoryQualityClusterRecord
        {
            Id = clusterId,
            ProjectId = projectId,
            ClusterHash = CognitiveMemoryHash.FromUtf8($"{derivedMemoryId:D}:professor-cluster").Value,
            PrimaryKeyFamily = CognitiveMemoryQualityClusterKeyFamily.SemanticTopic,
            Readiness = CognitiveMemoryQualityClusterReadiness.AggregateReady,
            AccessLevel = CognitiveMemoryAccessLevel.Project,
            RiskLevel = CognitiveMemoryRiskLevel.Low,
            PolicyProfileId = Policy(projectId).PolicyProfileId.Value,
            AlgorithmVersion = "unit-test-professor-integration",
            KeyCount = 1,
            MemberCount = includeIndependentSourceMap ? 2 : 1,
            SourceEvidenceCount = includeIndependentSourceMap ? 2 : 1,
            SourceIndependenceScore = includeIndependentSourceMap ? 1 : 0,
            CompositeScore = 1,
            AggregateEligible = true,
            EligibilityReason = "Unit-test aggregate-ready professor integration.",
            CreatedAtUtc = fixture.Clock.GetUtcNow(),
            UpdatedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        });
        dbContext.Add(new CognitiveMemoryDreamAggregateCandidateRecord
        {
            Id = candidateId,
            DreamRunId = dreamRunId,
            ClusterId = clusterId,
            ProjectId = projectId,
            Mode = CognitiveMemoryConsolidationMode.ProjectNightly,
            Status = CognitiveMemoryDreamAggregateCandidateStatus.Applied,
            Title = "Derived professor integrated aggregate",
            SummaryText = "Release-owner approval is integrated from professor anchor and independent evidence.",
            CanonicalText = "Release-owner approval is required before traffic restoration.",
            AccessLevel = CognitiveMemoryAccessLevel.Project,
            RiskLevel = CognitiveMemoryRiskLevel.Low,
            AlgorithmVersion = "unit-test-professor-integration",
            PayloadHash = CognitiveMemoryHash.FromUtf8($"{derivedMemoryId:D}:professor-integration").Value,
            MemoryRecordId = derivedMemoryId,
            ClaimCount = 1,
            SourceMapCount = includeIndependentSourceMap ? 2 : 1,
            CreatedAtUtc = fixture.Clock.GetUtcNow(),
            UpdatedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        });
        dbContext.Add(new CognitiveMemoryDreamAggregateClaimRecord
        {
            Id = claimId,
            AggregateCandidateId = candidateId,
            ProjectId = projectId,
            Sequence = 0,
            ClaimKind = CognitiveMemoryClaimKind.Fact,
            ClaimText = "Release-owner approval is required before traffic restoration.",
            SubjectKey = "production.rollback.approval",
            PredicateKey = "requires",
            ObjectKey = "release-owner-approval",
            CreatedAtUtc = fixture.Clock.GetUtcNow()
        });
        dbContext.Add(new CognitiveMemoryDreamAggregateClaimSourceMapRecord
        {
            AggregateCandidateId = candidateId,
            AggregateClaimId = claimId,
            ProjectId = projectId,
            SourceMemoryRecordId = capture.AppliedMemoryRecordId!.Value,
            SourceItemId = capture.SourceItemId,
            EvidenceAnchorId = capture.EvidenceAnchorId,
            Direction = CognitiveMemoryEvidenceDirection.Supports,
            AccessLevel = CognitiveMemoryAccessLevel.Project,
            RedactionState = CognitiveMemoryRedactionState.Safe,
            Summary = "Professor anchor direct quote source map.",
            CreatedAtUtc = fixture.Clock.GetUtcNow()
        });

        if (includeIndependentSourceMap)
        {
            var independentMemoryId = await SeedLinkedAdvancedMemoryAsync(
                fixture,
                projectId,
                "Independent release audit memory",
                "Independent release audit confirms release-owner approval before traffic restoration.",
                "Independent release audit confirms release-owner approval before traffic restoration.",
                "release.owner.independent.audit");
            var independentSourceLink = await dbContext.Set<CognitiveMemorySourceLinkRecord>()
                .AsNoTracking()
                .SingleAsync(link => link.MemoryRecordId == independentMemoryId);
            var independentEvidenceLink = await dbContext.Set<CognitiveMemoryRecordEvidenceAnchorRecord>()
                .AsNoTracking()
                .SingleAsync(link => link.MemoryRecordId == independentMemoryId);
            dbContext.Add(new CognitiveMemoryDreamAggregateClaimSourceMapRecord
            {
                AggregateCandidateId = candidateId,
                AggregateClaimId = claimId,
                ProjectId = projectId,
                SourceMemoryRecordId = independentMemoryId,
                SourceItemId = independentSourceLink.SourceItemId,
                EvidenceAnchorId = independentEvidenceLink.EvidenceAnchorId,
                Direction = CognitiveMemoryEvidenceDirection.Supports,
                AccessLevel = CognitiveMemoryAccessLevel.Project,
                RedactionState = CognitiveMemoryRedactionState.Safe,
                Summary = "Independent release audit source map.",
                CreatedAtUtc = fixture.Clock.GetUtcNow()
            });
        }

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedProfessorRecallSourceMapMentionsAsync(
        TestFixture fixture,
        Guid projectId,
        Guid derivedMemoryId,
        int useCount)
    {
        await using var dbContext = fixture.Factory.CreateDbContext();
        for (var index = 0; index < useCount; index++)
        {
            var synthesisId = CognitiveMemorySynthesizedRecallId.New();
            var statementId = CognitiveMemorySynthesizedStatementId.New();
            dbContext.Add(new CognitiveMemorySynthesizedRecallRecord
            {
                Id = synthesisId.Value,
                ProjectId = projectId,
                RecallTraceId = Guid.NewGuid(),
                Brief = $"Professor mastery recall use {index}.",
                ReferencesShownByDefault = false,
                StatementCount = 1,
                SourceMapCount = 1,
                CreatedAtUtc = fixture.Clock.GetUtcNow(),
                ConcurrencyToken = Guid.NewGuid()
            });
            dbContext.Add(new CognitiveMemorySynthesizedStatementRecord
            {
                Id = statementId.Value,
                SynthesisId = synthesisId.Value,
                ProjectId = projectId,
                Sequence = 0,
                Text = "Release-owner approval is required before traffic restoration.",
                CreatedAtUtc = fixture.Clock.GetUtcNow()
            });
            dbContext.Add(new CognitiveMemorySynthesizedStatementSourceMapRecord
            {
                SynthesisId = synthesisId.Value,
                StatementId = statementId.Value,
                ProjectId = projectId,
                MemoryRecordId = derivedMemoryId,
                SourceSystem = "professor-derived-memory",
                Locator = $"/professor/use/{index}",
                Summary = "Derived professor memory was used successfully.",
                AccessLevel = CognitiveMemoryAccessLevel.Project,
                RedactionState = CognitiveMemoryRedactionState.Safe,
                CreatedAtUtc = fixture.Clock.GetUtcNow()
            });
        }

        await dbContext.SaveChangesAsync();
    }

    private static async Task<AcceptedRecallUseFixture> SeedSynthesizedRecallUseAsync(
        TestFixture fixture,
        Guid projectId,
        Guid memoryRecordId,
        Guid evidenceAnchorId)
    {
        await using var dbContext = fixture.Factory.CreateDbContext();
        var recallTraceId = Guid.NewGuid();
        var synthesisId = CognitiveMemorySynthesizedRecallId.New();
        var statementId = CognitiveMemorySynthesizedStatementId.New();
        var acceptedOutcomeId = Guid.NewGuid();
        var evidenceAnchor = await dbContext.Set<CognitiveMemoryEvidenceAnchorRecord>()
            .AsNoTracking()
            .SingleAsync(anchor => anchor.Id == evidenceAnchorId);
        dbContext.Add(new CognitiveMemorySynthesizedRecallRecord
        {
            Id = synthesisId.Value,
            ProjectId = projectId,
            RecallTraceId = recallTraceId,
            Brief = "Operator-facing recall used derived professor memory.",
            ReferencesShownByDefault = false,
            StatementCount = 1,
            SourceMapCount = 1,
            CreatedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        });
        dbContext.Add(new CognitiveMemorySynthesizedStatementRecord
        {
            Id = statementId.Value,
            SynthesisId = synthesisId.Value,
            ProjectId = projectId,
            Sequence = 0,
            Text = "Rollback restore requires signed release-owner approval before traffic returns.",
            CreatedAtUtc = fixture.Clock.GetUtcNow()
        });
        dbContext.Add(new CognitiveMemorySynthesizedStatementSourceMapRecord
        {
            SynthesisId = synthesisId.Value,
            StatementId = statementId.Value,
            ProjectId = projectId,
            MemoryRecordId = memoryRecordId,
            SourceItemId = evidenceAnchor.SourceItemId,
            EvidenceAnchorId = evidenceAnchor.Id,
            SourceSystem = evidenceAnchor.SourceSystem,
            Locator = evidenceAnchor.Locator,
            Summary = "Derived professor memory supported the accepted statement.",
            AccessLevel = CognitiveMemoryAccessLevel.Project,
            RedactionState = CognitiveMemoryRedactionState.Safe,
            CreatedAtUtc = fixture.Clock.GetUtcNow()
        });
        await dbContext.SaveChangesAsync();
        return new AcceptedRecallUseFixture(recallTraceId, synthesisId, statementId, acceptedOutcomeId);
    }

    private static async Task SeedAcceptedProfessorUseEventsAsync(
        TestFixture fixture,
        Guid projectId,
        Guid derivedMemoryId,
        int useCount)
    {
        await using var dbContext = fixture.Factory.CreateDbContext();
        for (var index = 0; index < useCount; index++)
        {
            var traceId = Guid.NewGuid();
            dbContext.Add(new CognitiveMemoryScoreEvaluationTraceRecord
            {
                Id = traceId,
                ProjectId = projectId,
                OwnerKind = CognitiveMemoryScoreOwnerKind.MemoryRecord,
                OwnerId = derivedMemoryId,
                SpaceKind = CognitiveMemoryScoreSpaceKind.SalienceSignal,
                SchemaVersion = "unit-test-professor-accepted-use-v1",
                NormalizationProfile = "unit-test-professor-accepted-use",
                AlgorithmVersion = "unit-test-professor-accepted-use",
                InputHash = CognitiveMemoryHash.FromUtf8($"{derivedMemoryId:D}:accepted-use:{index}").Value,
                ScalarProjectionKind = CognitiveMemoryScoreScalarProjectionKind.DisplayOnly,
                ProjectionBucket = CognitiveMemoryScoreProjectionBucket.StrongAccept,
                DisplayScore = 1,
                MatchedShapeCount = 1,
                TracePayloadJson = "{}",
                CalculatedAtUtc = fixture.Clock.GetUtcNow(),
                CreatedAtUtc = fixture.Clock.GetUtcNow(),
                ConcurrencyToken = Guid.NewGuid()
            });
            dbContext.Add(new CognitiveMemorySignalRecord
            {
                ProjectId = projectId,
                SignalKind = CognitiveMemorySignalKind.ProfessorAnchorAcceptedUse,
                SourceKind = CognitiveMemorySignalSourceKind.RecallTrace,
                ActorKind = CognitiveMemoryActorKind.System,
                ActorId = "agent:test",
                PolicyProfileId = Policy(projectId).PolicyProfileId.Value,
                AccessLevel = CognitiveMemoryAccessLevel.Project,
                RedactionState = CognitiveMemoryRedactionState.Safe,
                RiskLevel = CognitiveMemoryRiskLevel.Low,
                RequiresReview = false,
                MemoryRecordId = derivedMemoryId,
                SignalScoreEvaluationTraceId = traceId,
                ScoreSchemaVersion = "unit-test-professor-accepted-use-v1",
                NormalizationProfileId = "unit-test-professor-accepted-use",
                AlgorithmVersion = "unit-test-professor-accepted-use",
                ComponentCount = 1,
                MatchedShapeCount = 1,
                DisplayMagnitudeProjection = 1,
                Summary = $"Accepted professor memory use event {index}.",
                MetadataJson = "{\"acceptedOutcome\":true}",
                ObservedAtUtc = fixture.Clock.GetUtcNow(),
                CreatedAtUtc = fixture.Clock.GetUtcNow(),
                ConcurrencyToken = Guid.NewGuid()
            });
        }

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedNonAggregateReadyClusterMembershipAsync(
        TestFixture fixture,
        Guid projectId,
        Guid derivedMemoryId)
    {
        await using var dbContext = fixture.Factory.CreateDbContext();
        var clusterId = Guid.NewGuid();
        dbContext.Add(new CognitiveMemoryQualityClusterRecord
        {
            Id = clusterId,
            ProjectId = projectId,
            ClusterHash = CognitiveMemoryHash.FromUtf8($"{derivedMemoryId:D}:non-ready-cluster").Value,
            PrimaryKeyFamily = CognitiveMemoryQualityClusterKeyFamily.SemanticTopic,
            Readiness = CognitiveMemoryQualityClusterReadiness.NeedsMoreEvidence,
            AccessLevel = CognitiveMemoryAccessLevel.Project,
            RiskLevel = CognitiveMemoryRiskLevel.Low,
            PolicyProfileId = Policy(projectId).PolicyProfileId.Value,
            AlgorithmVersion = "unit-test-professor-cluster-only",
            KeyCount = 1,
            MemberCount = 1,
            SourceEvidenceCount = 1,
            SourceIndependenceScore = 0,
            CompositeScore = 0.4,
            AggregateEligible = false,
            EligibilityReason = "Unit-test cluster membership without aggregate-ready integration.",
            CreatedAtUtc = fixture.Clock.GetUtcNow(),
            UpdatedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        });
        dbContext.Add(new CognitiveMemoryQualityClusterMemberRecord
        {
            ClusterId = clusterId,
            ProjectId = projectId,
            MemberKind = CognitiveMemoryQualityClusterMemberKind.MemoryRecord,
            MemoryRecordId = derivedMemoryId,
            AccessLevel = CognitiveMemoryAccessLevel.Project,
            RiskLevel = CognitiveMemoryRiskLevel.Low,
            ValidationState = CognitiveMemoryValidationState.Approved,
            StabilityState = CognitiveMemoryStabilityState.Active,
            CreatedAtUtc = fixture.Clock.GetUtcNow()
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task<Guid> SeedRejectedProfessorComparisonCandidateAsync(
        TestFixture fixture,
        Guid projectId,
        CognitiveMemoryCuratorCapturedImprovementRecord capture)
    {
        await using var dbContext = fixture.Factory.CreateDbContext();
        var candidateId = Guid.NewGuid();
        var mappedClaimId = Guid.NewGuid();
        var missingClaimId = Guid.NewGuid();
        var dreamRunId = Guid.NewGuid();
        var clusterId = Guid.NewGuid();
        dbContext.Add(new CognitiveMemoryDreamRunRecord
        {
            Id = dreamRunId,
            ProjectId = projectId,
            Mode = CognitiveMemoryConsolidationMode.ProjectNightly,
            TriggerKind = CognitiveMemoryConsolidationTriggerKind.Nightly,
            Status = CognitiveMemoryRunStatus.Succeeded,
            IdempotencyKey = $"unit-test-professor-rejected-{candidateId:D}",
            PolicyProfileId = Policy(projectId).PolicyProfileId.Value,
            AlgorithmVersion = "unit-test-professor-rejected-comparison",
            AggregateCandidatesCreated = 1,
            StartedAtUtc = fixture.Clock.GetUtcNow(),
            CompletedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        });
        dbContext.Add(new CognitiveMemoryQualityClusterRecord
        {
            Id = clusterId,
            ProjectId = projectId,
            ClusterHash = CognitiveMemoryHash.FromUtf8($"{candidateId:D}:rejected-comparison-cluster").Value,
            PrimaryKeyFamily = CognitiveMemoryQualityClusterKeyFamily.SemanticTopic,
            Readiness = CognitiveMemoryQualityClusterReadiness.AggregateReady,
            AccessLevel = CognitiveMemoryAccessLevel.Project,
            RiskLevel = CognitiveMemoryRiskLevel.Low,
            PolicyProfileId = Policy(projectId).PolicyProfileId.Value,
            AlgorithmVersion = "unit-test-professor-rejected-comparison",
            KeyCount = 1,
            MemberCount = 1,
            SourceEvidenceCount = 1,
            SourceIndependenceScore = 1,
            CompositeScore = 1,
            AggregateEligible = true,
            EligibilityReason = "Unit-test rejected comparison cluster.",
            CreatedAtUtc = fixture.Clock.GetUtcNow(),
            UpdatedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        });
        dbContext.Add(new CognitiveMemoryDreamAggregateCandidateRecord
        {
            Id = candidateId,
            DreamRunId = dreamRunId,
            ClusterId = clusterId,
            ProjectId = projectId,
            Mode = CognitiveMemoryConsolidationMode.ProjectNightly,
            Status = CognitiveMemoryDreamAggregateCandidateStatus.Proposed,
            Title = "Rejected professor comparison aggregate",
            SummaryText = "One professor comparison claim is mapped and one claim intentionally has no source map.",
            CanonicalText = "Production rollback needs signed release-owner approval. Missing claim has no support.",
            AccessLevel = CognitiveMemoryAccessLevel.Project,
            RiskLevel = CognitiveMemoryRiskLevel.Low,
            AlgorithmVersion = "unit-test-professor-rejected-comparison",
            PayloadHash = CognitiveMemoryHash.FromUtf8($"{candidateId:D}:rejected-comparison").Value,
            ClaimCount = 2,
            SourceMapCount = 1,
            CreatedAtUtc = fixture.Clock.GetUtcNow(),
            UpdatedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        });
        dbContext.AddRange(
            new CognitiveMemoryDreamAggregateClaimRecord
            {
                Id = mappedClaimId,
                AggregateCandidateId = candidateId,
                ProjectId = projectId,
                Sequence = 0,
                ClaimKind = CognitiveMemoryClaimKind.Fact,
                ClaimText = "Production rollback needs signed release-owner approval.",
                SubjectKey = "production.rollback.approval",
                PredicateKey = "needs",
                ObjectKey = "release-owner-approval",
                CreatedAtUtc = fixture.Clock.GetUtcNow()
            },
            new CognitiveMemoryDreamAggregateClaimRecord
            {
                Id = missingClaimId,
                AggregateCandidateId = candidateId,
                ProjectId = projectId,
                Sequence = 1,
                ClaimKind = CognitiveMemoryClaimKind.Fact,
                ClaimText = "Coffee machines need signed release-owner approval.",
                SubjectKey = "coffee.machine",
                PredicateKey = "needs",
                ObjectKey = "release-owner-approval",
                CreatedAtUtc = fixture.Clock.GetUtcNow()
            });
        dbContext.Add(new CognitiveMemoryDreamAggregateClaimSourceMapRecord
        {
            AggregateCandidateId = candidateId,
            AggregateClaimId = mappedClaimId,
            ProjectId = projectId,
            SourceMemoryRecordId = capture.AppliedMemoryRecordId!.Value,
            SourceItemId = capture.SourceItemId,
            EvidenceAnchorId = capture.EvidenceAnchorId,
            Direction = CognitiveMemoryEvidenceDirection.Supports,
            AccessLevel = CognitiveMemoryAccessLevel.Project,
            RedactionState = CognitiveMemoryRedactionState.Safe,
            Summary = "Professor anchor source map for rejected comparison candidate.",
            CreatedAtUtc = fixture.Clock.GetUtcNow()
        });
        await dbContext.SaveChangesAsync();
        return candidateId;
    }

    private static async Task<Guid> SeedLinkedAdvancedMemoryAsync(
        TestFixture fixture,
        Guid projectId,
        string title,
        string canonicalText,
        string sourceText,
        string topicKey)
    {
        await using var dbContext = fixture.Factory.CreateDbContext();
        var linkedSource = await CreateLinkedSourceAsync(dbContext, fixture, projectId, title, sourceText);
        var memory = new CognitiveMemoryRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Kind = CognitiveMemoryRecordKind.Semantic,
            Origin = CognitiveMemoryRecordOrigin.SourceDerived,
            Title = title,
            CanonicalText = canonicalText,
            SummaryText = canonicalText,
            TopicKey = topicKey,
            ValidationState = CognitiveMemoryValidationState.Approved,
            StabilityState = CognitiveMemoryStabilityState.Active,
            CreatedInMode = CognitiveMemoryOperationMode.Observe,
            AlgorithmVersion = "unit-test-linked",
            ContentHash = CognitiveMemoryHash.FromUtf8(canonicalText).Value,
            SourceEvidenceCount = 1,
            EvidenceAnchorCount = 1,
            AccessLevel = CognitiveMemoryAccessLevel.Project,
            RiskLevel = CognitiveMemoryRiskLevel.Low,
            CreatedAtUtc = fixture.Clock.GetUtcNow(),
            UpdatedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        };
        dbContext.AddRange(
            memory,
            new CognitiveMemorySourceLinkRecord
            {
                MemoryRecordId = memory.Id,
                SourceManifestId = linkedSource.SourceManifestId,
                SourceItemId = linkedSource.SourceItemId,
                EvidenceRole = CognitiveMemoryEvidenceRole.PrimarySource,
                Locator = linkedSource.Locator,
                Summary = linkedSource.Content,
                CreatedAtUtc = fixture.Clock.GetUtcNow()
            },
            new CognitiveMemoryRecordEvidenceAnchorRecord
            {
                MemoryRecordId = memory.Id,
                EvidenceAnchorId = linkedSource.EvidenceAnchorId,
                EvidenceRole = CognitiveMemoryEvidenceRole.PrimarySource,
                Summary = linkedSource.Content,
                CreatedAtUtc = fixture.Clock.GetUtcNow()
            });
        await dbContext.SaveChangesAsync();
        return memory.Id;
    }

    private static async Task<LinkedSourceSeed> CreateLinkedSourceAsync(
        AppDbContext dbContext,
        TestFixture fixture,
        Guid projectId,
        string title,
        string content)
    {
        var contentHash = CognitiveMemoryHash.FromUtf8(content).Value;
        var manifest = new CognitiveMemorySourceManifestRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            SourceSystem = "unit-test",
            SourceScopeKey = projectId.ToString("D"),
            SourceSnapshotId = $"snapshot-{Guid.NewGuid():N}",
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
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            SourceManifestId = manifest.Id,
            SourceSystem = "unit-test",
            SourceItemKey = $"source-{Guid.NewGuid():N}",
            SourceItemType = "test-node",
            Title = title,
            ContentText = content,
            Locator = $"/unit/{Guid.NewGuid():N}",
            ContentHash = contentHash,
            RedactionState = CognitiveMemoryRedactionState.Safe,
            AccessLevel = CognitiveMemoryAccessLevel.Project,
            AccessScope = projectId.ToString("D"),
            ObservedAtUtc = fixture.Clock.GetUtcNow(),
            CreatedAtUtc = fixture.Clock.GetUtcNow(),
            UpdatedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        };
        var evidenceAnchor = new CognitiveMemoryEvidenceAnchorRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            AnchorKind = CognitiveMemoryEvidenceAnchorKind.TextSpan,
            SourceManifestId = manifest.Id,
            SourceItemId = sourceItem.Id,
            SourceSystem = "unit-test",
            Locator = sourceItem.Locator,
            StructuredPath = "$.content",
            TextStart = 0,
            TextEnd = content.Length,
            QuoteHash = CognitiveMemoryHash.FromUtf8($"quote:{sourceItem.Id:D}").Value,
            TrustLevel = CognitiveMemorySourceTrustLevel.RuntimeSource,
            RedactionState = CognitiveMemoryRedactionState.Safe,
            SourceHash = contentHash,
            ObservedAtUtc = fixture.Clock.GetUtcNow(),
            CreatedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        };
        dbContext.AddRange(manifest, sourceItem, evidenceAnchor);
        await dbContext.SaveChangesAsync();
        return new LinkedSourceSeed(
            manifest.Id,
            sourceItem.Id,
            evidenceAnchor.Id,
            sourceItem.Locator,
            content);
    }

    private static async Task<Guid> SeedClaimAsync(
        TestFixture fixture,
        Guid projectId,
        Guid memoryRecordId)
    {
        await using var dbContext = fixture.Factory.CreateDbContext();
        var claim = new CognitiveMemoryClaimRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            MemoryRecordId = memoryRecordId,
            ClaimKind = CognitiveMemoryClaimKind.Fact,
            ClaimText = "Production deploy source of truth is unresolved.",
            SubjectKey = "production.deploy",
            PredicateKey = "source-of-truth",
            ObjectKey = "unresolved",
            CurrentBeliefState = CognitiveMemoryBeliefStateKind.Supported,
            CurrentBeliefBucket = CognitiveMemoryScoreProjectionBucket.StrongAccept,
            ValidationState = CognitiveMemoryValidationState.Approved,
            StabilityState = CognitiveMemoryStabilityState.Active,
            AlgorithmVersion = "unit-test",
            CreatedAtUtc = fixture.Clock.GetUtcNow(),
            UpdatedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        };
        dbContext.Add(claim);
        await dbContext.SaveChangesAsync();
        return claim.Id;
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

    private static CognitiveMemorySignalLedger CreateSignalLedger(TestFixture fixture)
        => new(
            fixture.Factory,
            new CognitiveMemoryScoreSpaceRegistry(),
            fixture.ScoreDriver,
            fixture.Clock);

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

    private static string ReadRepositoryFile(params string[] relativePathSegments)
    {
        var root = FindRepositoryRoot();
        var pathSegments = new[] { root }.Concat(relativePathSegments).ToArray();
        return File.ReadAllText(Path.Combine(pathSegments));
    }

    private static string ReadRepositoryFiles(params string[] relativePathSegments)
    {
        var root = FindRepositoryRoot();
        var pathSegments = new[] { root }.Concat(relativePathSegments).ToArray();
        var directory = Path.Combine(pathSegments);
        return string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "CanDoItAll.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from the test working directory.");
    }

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

    private sealed record LinkedSourceSeed(
        Guid SourceManifestId,
        Guid SourceItemId,
        Guid EvidenceAnchorId,
        string Locator,
        string Content);

    private sealed record TestFixture(
        TestDbContextFactory Factory,
        FixedClock Clock,
        ICognitiveMemoryScoreGeometryDriver ScoreDriver);

    private sealed record AcceptedRecallUseFixture(
        Guid RecallTraceId,
        CognitiveMemorySynthesizedRecallId SynthesisId,
        CognitiveMemorySynthesizedStatementId StatementId,
        Guid AcceptedOutcomeId);

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

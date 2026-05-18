using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit;

public sealed class CognitiveMemoryAdvancedServicesTests
{
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

    private static CognitiveMemoryPolicyContext Policy(
        Guid? projectId,
        CognitiveMemoryRiskLevel riskLevel = CognitiveMemoryRiskLevel.Low,
        bool allowRestrictedContent = false)
        => new(
            projectId,
            "agent:test",
            CognitiveMemoryAccessLevel.Project,
            new CognitiveMemoryPolicyProfileId("policy:test"),
            riskLevel,
            allowRestrictedContent);

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

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }

    private sealed class FakeRecallOrchestrator(Guid projectId) : ICognitiveMemoryRecallOrchestrator
    {
        public ValueTask<CognitiveMemoryRecallResult> RecallAsync(
            CognitiveMemoryRecallRequest request,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new CognitiveMemoryRecallResult(
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
                            "Production deployment must cite the production runbook.",
                            [],
                            [],
                            [])
                    ],
                    [],
                    [],
                    new Dictionary<string, string>()),
                [],
                [],
                []));
    }
}

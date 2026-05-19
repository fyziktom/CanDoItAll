using System.Text.Json;
using Bunit;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.Modules.CognitiveMemory.Pages;
using CanDoItAll.Modules.Projects;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class CognitiveMemoryPageTests
{
    [Fact]
    public async Task CognitiveMemoryPage_RendersReviewTraceHealthAndPersistsReviewDecision()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var projectId = await CreateProjectAsync(projectsService, "Cognitive memory review UI");
        var reviewItemId = await SeedReviewUiEvidenceAsync(harness, projectId);
        var reviewUiService = harness.Context.Services.GetRequiredService<ICognitiveMemoryReviewUiService>();
        var directSnapshot = await reviewUiService.GetSnapshotAsync(new CognitiveMemoryReviewUiQuery(projectId));
        var memoryRecord = Assert.Single(directSnapshot.MemoryRecords);
        Assert.Single(memoryRecord.SourceLinks);
        Assert.Single(directSnapshot.ReviewItems);
        Assert.Single(directSnapshot.ProcedureSkills);
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo($"/cognitive-memory?projectId={projectId:D}");
        var cut = harness.Context.RenderComponent<CognitiveMemoryPage>();

        cut.WaitForElement("[data-testid='cognitive-memory-summary']");
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Deployment rollback procedure", cut.Markup);
            Assert.Contains("Projection health", cut.Markup);
            Assert.Contains("Procedure library", cut.Markup);
        });

        cut.Find("[data-testid='cognitive-memory-tab-probe-workbench']").Click();
        cut.WaitForElement("[data-testid='cognitive-memory-probe-workbench']");
        Assert.NotNull(cut.Find("[data-testid='cognitive-memory-probe-voice-mode']"));
        Assert.NotNull(cut.Find("[data-testid='cognitive-memory-probe-voice-question']"));
        Assert.NotNull(cut.Find("[data-testid='cognitive-memory-probe-voice-correction']"));
        Assert.Contains("Audio ready.", cut.Markup);

        cut.Find("[data-testid='cognitive-memory-tab-settings']").Click();
        cut.WaitForElement("[data-testid='cognitive-memory-settings']");
        Assert.Contains("Schedule and consolidation triggers", cut.Markup);
        Assert.Contains("Project and process sources", cut.Markup);
        Assert.Contains("Run configured memory work", cut.Markup);
        Assert.NotNull(cut.Find("[data-testid='cognitive-memory-run-automation']"));
        Assert.NotNull(cut.Find("[data-testid='cognitive-memory-rebuild-projections']"));
        Assert.NotNull(cut.Find("[data-testid='cognitive-memory-automation-run-progress']"));
        Assert.NotNull(cut.Find("[data-testid='cognitive-memory-projection-rebuild-progress']"));
        Assert.Contains("Ready.", cut.Markup);

        cut.Find("[data-testid='cognitive-memory-tab-sources']").Click();
        cut.WaitForElement("[data-testid='cognitive-memory-external-sources']");
        Assert.Contains("Drop a source document", cut.Markup);
        Assert.Contains("Ingest a website link", cut.Markup);
        Assert.Contains("External source status", cut.Markup);

        cut.Find("[data-testid='cognitive-memory-tab-memory']").Click();
        cut.WaitForElement("[data-testid='cognitive-memory-explorer']");
        Assert.Contains("Memory source evidence", cut.Markup);
        Assert.Contains("Runtime source evidence.", cut.Markup);

        cut.Find("[data-testid='cognitive-memory-tab-review']").Click();
        cut.WaitForElement("[data-testid='cognitive-memory-review-queue']");
        Assert.Contains("Proposed memory", cut.Markup);
        Assert.Contains("Docker rollback candidate", cut.Markup);
        Assert.Contains("Rollback source evidence.", cut.Markup);
        cut.Find("[data-testid='cognitive-memory-review-notes']").Change("Needs source-backed rollback validation.");
        cut.Find("[data-testid='cognitive-memory-review-needs-changes']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Needs Changes", cut.Markup);
        });
        var dbContextFactory = harness.Context.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            var reviewItem = await dbContext.Set<CognitiveMemoryReviewItemRecord>().SingleAsync(item => item.Id == reviewItemId);
            Assert.Equal(CognitiveMemoryReviewStatus.NeedsChanges, reviewItem.Status);
            Assert.Equal("Needs source-backed rollback validation.", reviewItem.DecisionNotes);
        }

        cut.Find("[data-testid='cognitive-memory-tab-traces']").Click();
        cut.WaitForElement("[data-testid='cognitive-memory-trace-viewer']");
        Assert.Contains("Stage, candidate, and source evidence", cut.Markup);
        Assert.Contains("Strong source-backed recall candidate.", cut.Markup);

        cut.Find("[data-testid='cognitive-memory-tab-health']").Click();
        cut.WaitForElement("[data-testid='cognitive-memory-health']");
        Assert.Contains("Fixture consolidation failure.", cut.Markup);
        Assert.Contains("Projection is stale.", cut.Markup);
        Assert.Contains("Procedure validation replay required.", cut.Markup);
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projectsService, string name)
    {
        var result = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = name,
            Description = $"{name} description",
            Objective = $"{name} objective",
            CurrentPhase = "Validation"
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static async Task<Guid> SeedReviewUiEvidenceAsync(ComponentTestHarness harness, Guid projectId)
    {
        var dbContextFactory = harness.Context.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var now = DateTimeOffset.UnixEpoch;
        var maturityTraceId = Guid.NewGuid();
        var recallScoreTraceId = Guid.NewGuid();
        var simulationRiskTraceId = Guid.NewGuid();
        var replayPriorityTraceId = Guid.NewGuid();
        var consolidationRunId = Guid.NewGuid();
        var memoryRecord = new CognitiveMemoryRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Kind = CognitiveMemoryRecordKind.Procedural,
            Origin = CognitiveMemoryRecordOrigin.SourceDerived,
            Title = "Docker deploy memory",
            CanonicalText = "Docker deploy memory text.",
            SummaryText = "Docker deploy summary.",
            TopicKey = "docker.deploy",
            ValidationState = CognitiveMemoryValidationState.NeedsHumanReview,
            StabilityState = CognitiveMemoryStabilityState.Experimental,
            AlgorithmVersion = "component-test",
            ContentHash = CognitiveMemoryHash.FromUtf8("component memory").Value,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        var sourceManifest = new CognitiveMemorySourceManifestRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            SourceSystem = "component-test",
            SourceScopeKey = "project:component",
            SourceSnapshotId = "component-snapshot",
            SnapshotHash = CognitiveMemoryHash.FromUtf8("component snapshot").Value,
            ProviderVersion = "component-test",
            ScanStatus = CognitiveMemoryRunStatus.Succeeded,
            ObservedAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        var sourceItem = new CognitiveMemorySourceItemRecord
        {
            Id = Guid.NewGuid(),
            SourceManifestId = sourceManifest.Id,
            ProjectId = projectId,
            SourceSystem = "component-test",
            SourceItemKey = "component-source-item",
            SourceItemType = "runbook",
            Title = "Component deployment runbook",
            ContentText = "Rollback source evidence.",
            Locator = "/component/source",
            ContentHash = CognitiveMemoryHash.FromUtf8("component source item").Value,
            RedactionState = CognitiveMemoryRedactionState.Safe,
            AccessLevel = CognitiveMemoryAccessLevel.Project,
            AccessScope = projectId.ToString("D"),
            ObservedAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        var sourceLink = new CognitiveMemorySourceLinkRecord
        {
            Id = Guid.NewGuid(),
            MemoryRecordId = memoryRecord.Id,
            SourceManifestId = sourceManifest.Id,
            SourceItemId = sourceItem.Id,
            EvidenceRole = CognitiveMemoryEvidenceRole.PrimarySource,
            Locator = "/component/source",
            QuoteHash = CognitiveMemoryHash.FromUtf8("component quote").Value,
            Summary = "Runtime source evidence.",
            CreatedAtUtc = now
        };
        var evidenceAnchor = new CognitiveMemoryEvidenceAnchorRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            SourceSystem = "component-test",
            Locator = "/component/source",
            StructuredPath = "$.source",
            QuoteHash = CognitiveMemoryHash.FromUtf8("component quote").Value,
            TrustLevel = CognitiveMemorySourceTrustLevel.RuntimeSource,
            RedactionState = CognitiveMemoryRedactionState.Safe,
            SourceHash = CognitiveMemoryHash.FromUtf8("component source").Value,
            ObservedAtUtc = now,
            CreatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        var procedureSkill = new CognitiveMemoryProcedureSkillRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = "Deployment rollback procedure",
            Purpose = "Rollback an unhealthy deployment.",
            Maturity = CognitiveMemoryProcedureSkillMaturity.Observed,
            RiskLevel = CognitiveMemoryRiskLevel.High,
            ValidationState = CognitiveMemoryValidationState.NeedsHumanReview,
            AccessLevel = CognitiveMemoryAccessLevel.Project,
            MaturityScoreEvaluationTraceId = maturityTraceId,
            MaturityBucket = CognitiveMemoryScoreProjectionBucket.NeedsReview,
            DisplayMaturityScore = 0.42,
            StepCount = 3,
            FailureModeCount = 1,
            ValidationEvidenceCount = 1,
            AlgorithmVersion = "component-test",
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        var reviewItem = new CognitiveMemoryReviewItemRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            ReviewKind = CognitiveMemoryReviewKind.ProcedureSkill,
            Status = CognitiveMemoryReviewStatus.Pending,
            SubjectKind = CognitiveMemoryReviewSubjectKind.ProcedureSkill,
            SubjectId = procedureSkill.Id,
            RiskLevel = CognitiveMemoryRiskLevel.High,
            ReasonCode = "HighRiskProcedure",
            ReasonText = "High-risk procedure requires source-backed review.",
            SourceEvidenceCount = 1,
            CreatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        var recallTrace = new CognitiveMemoryRecallTraceRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            RecallMode = CognitiveMemoryRecallMode.FocusedTaskContext,
            RequestedByActorId = "agent:component",
            PolicyProfileId = "policy:component",
            RequestHash = CognitiveMemoryHash.FromUtf8("component recall").Value,
            AlgorithmVersion = "component-test",
            Outcome = CognitiveMemoryRunStatus.Succeeded,
            IncludedRecordCount = 1,
            ExcludedRecordCount = 1,
            SelectedClaimCount = 1,
            SelectedEvidenceAnchorCount = 1,
            InhibitedCandidateCount = 1,
            StartedAtUtc = now,
            CompletedAtUtc = now.AddMinutes(1),
            ConcurrencyToken = Guid.NewGuid()
        };
        var recallStage = new CognitiveMemoryRecallTraceStageRecord
        {
            Id = Guid.NewGuid(),
            RecallTraceId = recallTrace.Id,
            ProjectId = projectId,
            StageKind = CognitiveMemoryRecallTraceStageKind.CoarseCandidateActivation,
            ChannelKind = CognitiveMemoryRecallChannelKind.VectorProjection,
            Status = CognitiveMemoryRecallStageStatus.Completed,
            CandidateCount = 2,
            SelectedCount = 1,
            ExcludedCount = 1,
            StartedAtUtc = now,
            CompletedAtUtc = now.AddSeconds(10)
        };
        var recallCandidate = new CognitiveMemoryRecallCandidateRecord
        {
            Id = Guid.NewGuid(),
            RecallTraceId = recallTrace.Id,
            ProjectId = projectId,
            PrimaryChannelKind = CognitiveMemoryRecallChannelKind.VectorProjection,
            DecisionKind = CognitiveMemoryRecallCandidateDecisionKind.Selected,
            MemoryRecordId = memoryRecord.Id,
            EvidenceAnchorId = evidenceAnchor.Id,
            ScoreEvaluationTraceId = recallScoreTraceId,
            ScoreBucket = CognitiveMemoryScoreProjectionBucket.StrongAccept,
            DisplayRankProjection = 0.91,
            HasSourceDetail = true,
            SourceRefCount = 1,
            Title = "Docker deploy memory",
            Summary = "Selected due to source-backed deployment evidence.",
            Reason = "Strong source-backed recall candidate.",
            CreatedAtUtc = now
        };
        var sourceReference = new CognitiveMemoryRecallSourceRefRecord
        {
            Id = Guid.NewGuid(),
            RecallTraceId = recallTrace.Id,
            ProjectId = projectId,
            MemoryRecordId = memoryRecord.Id,
            EvidenceAnchorId = evidenceAnchor.Id,
            SourceSystem = "component-test",
            Locator = "/component/source",
            QuoteHash = evidenceAnchor.QuoteHash,
            Summary = "Runtime source evidence.",
            AccessLevel = CognitiveMemoryAccessLevel.Project,
            RedactionState = CognitiveMemoryRedactionState.Safe,
            IncludedInContext = true,
            CreatedAtUtc = now
        };
        var consolidationRun = new CognitiveMemoryConsolidationRunRecord
        {
            Id = consolidationRunId,
            ProjectId = projectId,
            Mode = CognitiveMemoryConsolidationMode.IncrementalRecent,
            TriggerKind = CognitiveMemoryConsolidationTriggerKind.SourceChanged,
            Status = CognitiveMemoryRunStatus.Failed,
            ProfileName = "component-test",
            IdempotencyKey = "component-consolidation",
            InputHash = CognitiveMemoryHash.FromUtf8("component input").Value,
            OutputHash = CognitiveMemoryHash.FromUtf8("component output").Value,
            AlgorithmVersion = "component-test",
            SourceItemsScanned = 2,
            CandidatesCreated = 1,
            ReviewItemsCreated = 1,
            FailureCode = "FixtureFailure",
            FailureMessage = "Fixture consolidation failure.",
            StartedAtUtc = now,
            CompletedAtUtc = now.AddMinutes(2),
            ConcurrencyToken = Guid.NewGuid()
        };
        var candidatePayload = new CognitiveMemoryConsolidationCandidatePayload(
            CognitiveMemoryConsolidationCandidateKind.Procedure,
            sourceItem.Id,
            evidenceAnchor.Id,
            null,
            reviewItem.Id,
            "unit-test",
            "runbook",
            "Docker rollback candidate",
            "Docker rollback requires health check validation and explicit source evidence.",
            sourceItem.ContentHash,
            "Generated from unit rollback source evidence.");
        var consolidationCandidate = new CognitiveMemoryConsolidationCandidateRecord
        {
            Id = Guid.NewGuid(),
            RunId = consolidationRun.Id,
            ProjectId = projectId,
            CandidateKind = CognitiveMemoryConsolidationCandidateKind.Procedure,
            Status = CognitiveMemoryConsolidationCandidateStatus.ReviewRequired,
            SourceItemId = sourceItem.Id,
            EvidenceAnchorId = evidenceAnchor.Id,
            ReviewItemId = reviewItem.Id,
            ScoreBucket = CognitiveMemoryScoreProjectionBucket.NeedsReview,
            DisplayPriorityProjection = 0.58,
            SourceContentHash = sourceItem.ContentHash,
            OutputHash = CognitiveMemoryHash.FromUtf8(candidatePayload.Summary).Value,
            AlgorithmVersion = "component-test",
            ReasonCode = "GeneratedCandidate",
            ReasonText = candidatePayload.Reason,
            PayloadJson = JsonSerializer.Serialize(
                candidatePayload,
                CognitiveMemoryJsonSerializerContext.Default.CognitiveMemoryConsolidationCandidatePayload),
            CreatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        var projectionState = new CognitiveMemoryProjectionStateRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            ProjectionKind = CognitiveMemoryProjectionKind.VectorCollection,
            TargetProvider = "qdrant",
            ProjectionSchemaVersion = "component-test",
            AlgorithmVersion = "component-test",
            Status = CognitiveMemoryProjectionStatus.RebuildRequired,
            LastSourceHash = CognitiveMemoryHash.FromUtf8("component projection").Value,
            FailureCode = "Stale",
            FailureMessage = "Projection is stale.",
            RebuildRequired = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        var simulation = new CognitiveMemoryProcedureSimulationRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            OutputKind = CognitiveMemoryProcedureSimulationOutputKind.RiskAnalysis,
            Status = CognitiveMemoryProcedureSimulationStatus.NeedsReview,
            Summary = "Speculative rollback risk analysis.",
            IsSpeculative = true,
            SpeculationLabel = "speculative-hypothesis",
            RiskLevel = CognitiveMemoryRiskLevel.High,
            RiskScoreEvaluationTraceId = simulationRiskTraceId,
            RiskBucket = CognitiveMemoryScoreProjectionBucket.NeedsReview,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        var replayJob = new CognitiveMemoryReplayJobRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            JobKind = CognitiveMemoryReplayJobKind.ValidateProcedure,
            State = CognitiveMemoryReplayJobState.NeedsReview,
            Reason = "Procedure validation replay required.",
            PriorityScoreEvaluationTraceId = replayPriorityTraceId,
            PriorityBucket = CognitiveMemoryScoreProjectionBucket.NeedsReview,
            DisplayPriorityProjection = 0.77,
            QueuePriority = 77,
            InputHash = CognitiveMemoryHash.FromUtf8("component replay").Value,
            ExpectedOutputSchema = "procedure-validation",
            AlgorithmVersion = "component-test",
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };

        dbContext.AddRange(
            CreateScoreTrace(
                maturityTraceId,
                projectId,
                CognitiveMemoryScoreOwnerKind.ProcedureSkill,
                procedureSkill.Id,
                CognitiveMemoryScoreSpaceKind.ProcedureMaturity,
                CognitiveMemoryScoreProjectionBucket.NeedsReview,
                0.42,
                now),
            CreateScoreTrace(
                recallScoreTraceId,
                projectId,
                CognitiveMemoryScoreOwnerKind.RecallCandidate,
                recallCandidate.Id,
                CognitiveMemoryScoreSpaceKind.RecallCandidate,
                CognitiveMemoryScoreProjectionBucket.StrongAccept,
                0.91,
                now),
            CreateScoreTrace(
                simulationRiskTraceId,
                projectId,
                CognitiveMemoryScoreOwnerKind.ProcedureSimulation,
                simulation.Id,
                CognitiveMemoryScoreSpaceKind.SimulationRisk,
                CognitiveMemoryScoreProjectionBucket.NeedsReview,
                0.84,
                now),
            CreateScoreTrace(
                replayPriorityTraceId,
                projectId,
                CognitiveMemoryScoreOwnerKind.ReplayJob,
                replayJob.Id,
                CognitiveMemoryScoreSpaceKind.ReplayPriority,
                CognitiveMemoryScoreProjectionBucket.NeedsReview,
                0.77,
                now),
            new CognitiveMemoryRunRecord
            {
                Id = consolidationRunId,
                ProjectId = projectId,
                RunKind = CognitiveMemoryRunKind.Consolidation,
                Status = CognitiveMemoryRunStatus.Failed,
                OperationMode = CognitiveMemoryOperationMode.Observe,
                IdempotencyKey = "component-consolidation",
                InputHash = CognitiveMemoryHash.FromUtf8("component run input").Value,
                AlgorithmVersion = "component-test",
                FailureCode = "FixtureFailure",
                FailureMessage = "Fixture consolidation failure.",
                StartedAtUtc = now,
                CompletedAtUtc = now.AddMinutes(2),
                ConcurrencyToken = Guid.NewGuid()
            },
            sourceManifest,
            sourceItem,
            memoryRecord,
            sourceLink,
            evidenceAnchor,
            procedureSkill,
            reviewItem,
            recallTrace,
            recallStage,
            recallCandidate,
            sourceReference,
            consolidationRun,
            consolidationCandidate,
            projectionState,
            simulation,
            replayJob);
        await dbContext.SaveChangesAsync();
        return reviewItem.Id;
    }

    private static CognitiveMemoryScoreEvaluationTraceRecord CreateScoreTrace(
        Guid id,
        Guid projectId,
        CognitiveMemoryScoreOwnerKind ownerKind,
        Guid ownerId,
        CognitiveMemoryScoreSpaceKind spaceKind,
        CognitiveMemoryScoreProjectionBucket bucket,
        double displayScore,
        DateTimeOffset now)
    {
        return new CognitiveMemoryScoreEvaluationTraceRecord
        {
            Id = id,
            ProjectId = projectId,
            OwnerKind = ownerKind,
            OwnerId = ownerId,
            SpaceKind = spaceKind,
            SchemaVersion = "component-test",
            NormalizationProfile = "component-test",
            AlgorithmVersion = "component-test",
            InputHash = CognitiveMemoryHash.FromUtf8($"{ownerKind}:{ownerId:D}:{spaceKind}").Value,
            ScalarProjectionKind = CognitiveMemoryScoreScalarProjectionKind.DisplayOnly,
            ProjectionBucket = bucket,
            DisplayScore = displayScore,
            MatchedShapeCount = 1,
            TracePayloadJson = "{}",
            CalculatedAtUtc = now,
            CreatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
    }
}

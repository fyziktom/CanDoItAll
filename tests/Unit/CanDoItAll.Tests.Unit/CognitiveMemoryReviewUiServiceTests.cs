using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit;

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
public sealed class CognitiveMemoryReviewUiServiceTests
{
    [Fact]
    public async Task GetSnapshotAsync_ReturnsReviewTraceHealthProcedureAndReplayEvidence()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SeedOperatorEvidenceAsync(fixture, projectId);
        var service = CreateService(fixture);

        var snapshot = await service.GetSnapshotAsync(new CognitiveMemoryReviewUiQuery(projectId));

        Assert.Equal(1, snapshot.Summary.MemoryRecordCount);
        Assert.Equal(1, snapshot.Summary.PendingReviewCount);
        Assert.Equal(1, snapshot.Summary.HighRiskReviewCount);
        Assert.Equal(1, snapshot.Summary.RecallTraceCount);
        Assert.Equal(1, snapshot.Summary.ConsolidationIssueCount);
        Assert.Equal(1, snapshot.Summary.ProjectionIssueCount);
        Assert.Equal(1, snapshot.Summary.ProcedureReviewCount);
        Assert.Equal(1, snapshot.Summary.SimulationReviewCount);
        Assert.Equal(1, snapshot.Summary.ProbeSessionCount);
        Assert.Equal(1, snapshot.Summary.SelfRegulationActionCount);
        Assert.Equal(1, snapshot.Summary.AnswerGateInterventionCount);
        Assert.Equal(1, snapshot.Summary.ProfessorReviewCount);
        Assert.Equal(1, snapshot.Summary.LearningProposalCount);
        Assert.Equal(1, snapshot.Summary.CrossProjectReviewCount);
        Assert.Equal(1, snapshot.Summary.DistributedIssueCount);
        var memory = Assert.Single(snapshot.MemoryRecords);
        Assert.Equal("Docker deploy memory", memory.Title);
        Assert.Single(memory.SourceLinks);
        Assert.Single(snapshot.ReviewItems);
        Assert.Single(snapshot.RecallTraces[0].Stages);
        Assert.Single(snapshot.RecallTraces[0].Candidates);
        Assert.Single(snapshot.RecallTraces[0].SourceReferences);
        Assert.Single(snapshot.ConsolidationRuns);
        Assert.Single(snapshot.ProjectionHealth);
        Assert.Single(snapshot.ProcedureSkills);
        Assert.Single(snapshot.ReplayJobs);
        Assert.Single(snapshot.ProbeSessions);
        Assert.Single(snapshot.SelfRegulationAssessments);
        Assert.Single(snapshot.AnswerGateDecisions);
        Assert.Single(snapshot.ProfessorReviews);
        Assert.Single(snapshot.LearningProposals);
        Assert.Single(snapshot.CrossProjectPromotions);
        Assert.Single(snapshot.DistributedJobs);
        Assert.Contains(snapshot.OperatorAudit, item => item.AuditKind == CognitiveMemoryOperatorAuditKind.MutationCommand);
        Assert.Contains(snapshot.OperatorAudit, item => item.AuditKind == CognitiveMemoryOperatorAuditKind.MutationAuditEvent);
        Assert.Contains(snapshot.OperatorAudit, item => item.AuditKind == CognitiveMemoryOperatorAuditKind.ClaimState);
        Assert.Contains(snapshot.OperatorAudit, item => item.AuditKind == CognitiveMemoryOperatorAuditKind.EvidenceAnchor);
        Assert.Contains(snapshot.OperatorAudit, item => item.AuditKind == CognitiveMemoryOperatorAuditKind.ProjectionFailure);
        Assert.Contains(snapshot.OperatorAudit, item => item.AuditKind == CognitiveMemoryOperatorAuditKind.RetentionCleanup);
        Assert.Contains("Deployment rollback", snapshot.ReviewItems[0].SubjectTitle, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DecideReviewItemAsync_PersistsDecisionAndRejectsStaleToken()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var reviewItemId = await SeedOperatorEvidenceAsync(fixture, projectId);
        var service = CreateService(fixture);
        var snapshot = await service.GetSnapshotAsync(new CognitiveMemoryReviewUiQuery(projectId));
        var reviewItem = Assert.Single(snapshot.ReviewItems);

        var updated = await service.DecideReviewItemAsync(new CognitiveMemoryReviewDecisionRequest(
            new CognitiveMemoryReviewItemId(reviewItemId),
            CognitiveMemoryReviewDecisionKind.RequestChanges,
            "operator:test",
            "Add source-backed rollback evidence.",
            reviewItem.ConcurrencyToken));

        Assert.Equal(CognitiveMemoryReviewStatus.NeedsChanges, updated.Status);
        Assert.Equal("operator:test", updated.DecidedByActorId);
        Assert.Equal("Add source-backed rollback evidence.", updated.DecisionNotes);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.DecideReviewItemAsync(new CognitiveMemoryReviewDecisionRequest(
            new CognitiveMemoryReviewItemId(reviewItemId),
            CognitiveMemoryReviewDecisionKind.Approve,
            "operator:test",
            string.Empty,
            reviewItem.ConcurrencyToken)));
    }

    [Fact]
    public async Task DecideReviewItemAsync_ApprovesDreamAggregateCandidate()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var now = fixture.Clock.GetUtcNow();
        var reviewItemId = Guid.NewGuid();
        var reviewToken = Guid.NewGuid();
        var dreamRunId = Guid.NewGuid();
        var clusterId = Guid.NewGuid();
        var candidateId = Guid.NewGuid();
        await using (var dbContext = fixture.Factory.CreateDbContext())
        {
            dbContext.Add(new CognitiveMemoryQualityClusterRecord
            {
                Id = clusterId,
                ProjectId = projectId,
                ClusterHash = CognitiveMemoryHash.FromUtf8("dream aggregate review cluster").Value,
                PrimaryKeyFamily = CognitiveMemoryQualityClusterKeyFamily.SemanticTopic,
                Readiness = CognitiveMemoryQualityClusterReadiness.Restricted,
                AccessLevel = CognitiveMemoryAccessLevel.Restricted,
                RiskLevel = CognitiveMemoryRiskLevel.High,
                PolicyProfileId = "unit-test",
                AlgorithmVersion = "unit-test",
                KeyCount = 1,
                MemberCount = 2,
                SourceEvidenceCount = 2,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                ConcurrencyToken = Guid.NewGuid()
            });
            dbContext.Add(new CognitiveMemoryDreamRunRecord
            {
                Id = dreamRunId,
                ProjectId = projectId,
                Mode = CognitiveMemoryConsolidationMode.ProjectNightly,
                TriggerKind = CognitiveMemoryConsolidationTriggerKind.Manual,
                Status = CognitiveMemoryRunStatus.Succeeded,
                IdempotencyKey = "dream-review-decision",
                PolicyProfileId = "unit-test",
                AlgorithmVersion = "unit-test",
                StartedAtUtc = now,
                CompletedAtUtc = now,
                ConcurrencyToken = Guid.NewGuid()
            });
            dbContext.Add(new CognitiveMemoryReviewItemRecord
            {
                Id = reviewItemId,
                ProjectId = projectId,
                ReviewKind = CognitiveMemoryReviewKind.GeneratedMemory,
                Status = CognitiveMemoryReviewStatus.Pending,
                SubjectKind = CognitiveMemoryReviewSubjectKind.Run,
                SubjectId = dreamRunId,
                RiskLevel = CognitiveMemoryRiskLevel.High,
                ReasonCode = "dream.aggregate.validation",
                ReasonText = "Restricted aggregate requires approval.",
                SourceEvidenceCount = 2,
                CreatedAtUtc = now,
                DecidedByActorId = string.Empty,
                DecisionNotes = string.Empty,
                ConcurrencyToken = reviewToken
            });
            dbContext.Add(new CognitiveMemoryDreamAggregateCandidateRecord
            {
                Id = candidateId,
                DreamRunId = dreamRunId,
                ClusterId = clusterId,
                ProjectId = projectId,
                Mode = CognitiveMemoryConsolidationMode.ProjectNightly,
                Status = CognitiveMemoryDreamAggregateCandidateStatus.NeedsHumanReview,
                Title = "ProjectNightly synthesis: Restricted source truth",
                SummaryText = "Restricted source-backed aggregate.",
                CanonicalText = "Restricted source-backed aggregate.",
                AccessLevel = CognitiveMemoryAccessLevel.Restricted,
                RiskLevel = CognitiveMemoryRiskLevel.High,
                AlgorithmVersion = "unit-test",
                PayloadHash = CognitiveMemoryHash.FromUtf8("dream aggregate payload").Value,
                ReviewItemId = reviewItemId,
                ClaimCount = 2,
                SourceMapCount = 2,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                ConcurrencyToken = Guid.NewGuid()
            });
            await dbContext.SaveChangesAsync();
        }

        var service = CreateService(fixture);
        var updated = await service.DecideReviewItemAsync(new CognitiveMemoryReviewDecisionRequest(
            new CognitiveMemoryReviewItemId(reviewItemId),
            CognitiveMemoryReviewDecisionKind.Approve,
            "operator:test",
            string.Empty,
            reviewToken));

        await using var assertionContext = fixture.Factory.CreateDbContext();
        var candidate = await assertionContext.Set<CognitiveMemoryDreamAggregateCandidateRecord>().SingleAsync(item => item.Id == candidateId);
        Assert.Equal(CognitiveMemoryReviewStatus.Approved, updated.Status);
        Assert.Equal(CognitiveMemoryDreamAggregateCandidateStatus.Approved, candidate.Status);
    }

    [Fact]
    public async Task GetSnapshotAsync_ExcludesResolvedReviewItemsByDefault()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var reviewItemId = await SeedOperatorEvidenceAsync(fixture, projectId);
        var service = CreateService(fixture);
        var snapshot = await service.GetSnapshotAsync(new CognitiveMemoryReviewUiQuery(projectId));
        var reviewItem = Assert.Single(snapshot.ReviewItems);

        await service.DecideReviewItemAsync(new CognitiveMemoryReviewDecisionRequest(
            new CognitiveMemoryReviewItemId(reviewItemId),
            CognitiveMemoryReviewDecisionKind.Reject,
            "operator:test",
            "Rejected noisy evidence.",
            reviewItem.ConcurrencyToken));

        var defaultSnapshot = await service.GetSnapshotAsync(new CognitiveMemoryReviewUiQuery(projectId));
        var historySnapshot = await service.GetSnapshotAsync(new CognitiveMemoryReviewUiQuery(
            projectId,
            IncludeResolvedReviewItems: true));

        Assert.Empty(defaultSnapshot.ReviewItems);
        var resolvedItem = Assert.Single(historySnapshot.ReviewItems);
        Assert.Equal(CognitiveMemoryReviewStatus.Rejected, resolvedItem.Status);
    }

    [Fact]
    public async Task GetSnapshotAsync_AppliesPerCollectionPagingAndReturnsQualityOperations()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SeedOperatorEvidenceAsync(fixture, projectId);
        await SeedQualityEvidenceAsync(fixture, projectId);
        var service = CreateService(fixture);

        var snapshot = await service.GetSnapshotAsync(new CognitiveMemoryReviewUiQuery(
            projectId,
            Take: 1,
            IncludeResolvedReviewItems: true,
            PageRequests:
            [
                new(CognitiveMemoryReviewUiCollectionKind.QualityClusters, 1, 2),
                new(CognitiveMemoryReviewUiCollectionKind.DreamRuns, 0, 2),
                new(CognitiveMemoryReviewUiCollectionKind.AggregateCandidates, 0, 2),
                new(CognitiveMemoryReviewUiCollectionKind.SynthesizedRecalls, 0, 2)
            ]));

        Assert.Equal(3, snapshot.Summary.QualityClusterCount);
        Assert.Equal(3, snapshot.Summary.ClusterSearchResultCount);
        Assert.Equal(3, snapshot.Summary.DreamRunCount);
        Assert.Equal(3, snapshot.Summary.AggregateCandidateCount);
        Assert.Equal(3, snapshot.Summary.SynthesizedRecallCount);
        Assert.Single(snapshot.QualityClusters);
        Assert.Equal(2, snapshot.DreamRuns.Count);
        Assert.Equal(2, snapshot.AggregateCandidates.Count);
        Assert.Equal(2, snapshot.SynthesizedRecalls.Count);
        var clusterPage = snapshot.Paging.PageFor(CognitiveMemoryReviewUiCollectionKind.QualityClusters);
        Assert.Equal(1, clusterPage.PageIndex);
        Assert.Equal(2, clusterPage.PageSize);
        Assert.Equal(3, clusterPage.TotalCount);
        Assert.Equal(3, clusterPage.FirstRowNumber);
        Assert.Equal(3, clusterPage.LastRowNumber);

        var clampedSnapshot = await service.GetSnapshotAsync(new CognitiveMemoryReviewUiQuery(
            projectId,
            IncludeResolvedReviewItems: true,
            PageRequests:
            [
                new(CognitiveMemoryReviewUiCollectionKind.QualityClusters, 99, 2)
            ]));
        var clampedClusterPage = clampedSnapshot.Paging.PageFor(CognitiveMemoryReviewUiCollectionKind.QualityClusters);
        Assert.Equal(1, clampedClusterPage.PageIndex);
        Assert.Single(clampedSnapshot.QualityClusters);
    }

    [Fact]
    public async Task GetSnapshotAsync_FiltersClusterSearchWithServerPaging()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SeedOperatorEvidenceAsync(fixture, projectId);
        await SeedQualityEvidenceAsync(fixture, projectId);
        var service = CreateService(fixture);

        var snapshot = await service.GetSnapshotAsync(new CognitiveMemoryReviewUiQuery(
            projectId,
            IncludeResolvedReviewItems: true,
            PageRequests:
            [
                new(CognitiveMemoryReviewUiCollectionKind.ClusterSearchResults, 0, 1)
            ],
            ClusterSearch: new CognitiveMemoryClusterSearchFilter(
                "validation",
                CognitiveMemoryQualityClusterKeyFamily.SemanticTopic,
                CognitiveMemoryQualityClusterReadiness.AggregateReady,
                CognitiveMemoryRiskLevel.Low)));

        Assert.Equal(1, snapshot.Summary.ClusterSearchResultCount);
        var page = snapshot.Paging.PageFor(CognitiveMemoryReviewUiCollectionKind.ClusterSearchResults);
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(1, page.PageSize);
        var result = Assert.Single(snapshot.ClusterSearchResults);
        Assert.Contains(result.Keys, key => key.DisplayText == "Validation dreaming cluster");
        Assert.Contains(result.Members, member => member.MemberKind == CognitiveMemoryQualityClusterMemberKind.MemoryRecord);
    }

    private static async Task<Guid> SeedOperatorEvidenceAsync(TestFixture fixture, Guid projectId)
    {
        await using var dbContext = fixture.Factory.CreateDbContext();
        var now = fixture.Clock.GetUtcNow();
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
            AlgorithmVersion = "unit-test",
            ContentHash = CognitiveMemoryHash.FromUtf8("docker deploy memory").Value,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        var sourceManifest = new CognitiveMemorySourceManifestRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            SourceSystem = "unit-test",
            SourceScopeKey = "project:unit",
            SourceSnapshotId = "unit-snapshot",
            SnapshotHash = CognitiveMemoryHash.FromUtf8("unit snapshot").Value,
            ProviderVersion = "unit-test",
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
            SourceSystem = "unit-test",
            SourceItemKey = "unit-source-item",
            SourceItemType = "runbook",
            Title = "Unit deployment runbook",
            ContentText = "Rollback source evidence.",
            Locator = "/unit/source",
            ContentHash = CognitiveMemoryHash.FromUtf8("unit source item").Value,
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
            Locator = "/unit/source",
            QuoteHash = CognitiveMemoryHash.FromUtf8("source quote").Value,
            Summary = "Runtime source evidence.",
            CreatedAtUtc = now
        };
        var evidenceAnchor = new CognitiveMemoryEvidenceAnchorRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            SourceSystem = "unit-test",
            Locator = "/unit/source",
            StructuredPath = "$.source",
            QuoteHash = CognitiveMemoryHash.FromUtf8("source quote").Value,
            TrustLevel = CognitiveMemorySourceTrustLevel.RuntimeSource,
            RedactionState = CognitiveMemoryRedactionState.Safe,
            SourceHash = CognitiveMemoryHash.FromUtf8("source").Value,
            ObservedAtUtc = now,
            CreatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        var claim = new CognitiveMemoryClaimRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            MemoryRecordId = memoryRecord.Id,
            ClaimKind = CognitiveMemoryClaimKind.ProcedureConstraint,
            ClaimText = "Docker rollback requires source-backed validation.",
            SubjectKey = "docker.rollback",
            PredicateKey = "requires",
            ObjectKey = "source.validation",
            CurrentBeliefState = CognitiveMemoryBeliefStateKind.Supported,
            CurrentBeliefBucket = CognitiveMemoryScoreProjectionBucket.NeedsReview,
            ValidationState = CognitiveMemoryValidationState.NeedsHumanReview,
            StabilityState = CognitiveMemoryStabilityState.Experimental,
            AlgorithmVersion = "unit-test",
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        var mutationCommand = new CognitiveMemoryMutationCommandRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            CommandKind = CognitiveMemoryMutationCommandKind.ValidateClaim,
            Status = CognitiveMemoryMutationCommandStatus.ReviewRequired,
            ActorKind = CognitiveMemoryActorKind.Agent,
            ActorId = "agent:test",
            IdempotencyKey = "unit-test-mutation-command",
            AffectedMemoryRecordIdsJson = $"[\"{memoryRecord.Id:D}\"]",
            AffectedClaimIdsJson = $"[\"{claim.Id:D}\"]",
            EvidenceAnchorIdsJson = $"[\"{evidenceAnchor.Id:D}\"]",
            RequiresHumanReview = true,
            ReviewReason = "Claim validation requires operator review.",
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        var mutationAuditEvent = new CognitiveMemoryMutationAuditEventRecord
        {
            Id = Guid.NewGuid(),
            MutationCommandId = mutationCommand.Id,
            ProjectId = projectId,
            Sequence = 1,
            EventKind = CognitiveMemoryMutationAuditEventKind.ReviewRequired,
            Message = "Claim validation routed to operator audit.",
            CreatedAtUtc = now
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
            MaturityScoreEvaluationTraceId = Guid.NewGuid(),
            MaturityBucket = CognitiveMemoryScoreProjectionBucket.NeedsReview,
            DisplayMaturityScore = 0.42,
            StepCount = 3,
            FailureModeCount = 1,
            ValidationEvidenceCount = 1,
            AlgorithmVersion = "unit-test",
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
            RequestedByActorId = "agent:test",
            PolicyProfileId = "policy:test",
            RequestHash = CognitiveMemoryHash.FromUtf8("recall request").Value,
            AlgorithmVersion = "unit-test",
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
            ScoreEvaluationTraceId = Guid.NewGuid(),
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
            SourceSystem = "unit-test",
            Locator = "/unit/source",
            QuoteHash = evidenceAnchor.QuoteHash,
            Summary = "Runtime source evidence.",
            AccessLevel = CognitiveMemoryAccessLevel.Project,
            RedactionState = CognitiveMemoryRedactionState.Safe,
            IncludedInContext = true,
            CreatedAtUtc = now
        };
        var consolidationRun = new CognitiveMemoryConsolidationRunRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Mode = CognitiveMemoryConsolidationMode.IncrementalRecent,
            TriggerKind = CognitiveMemoryConsolidationTriggerKind.SourceChanged,
            Status = CognitiveMemoryRunStatus.Failed,
            ProfileName = "unit-test",
            IdempotencyKey = "unit-test-consolidation",
            InputHash = CognitiveMemoryHash.FromUtf8("input").Value,
            OutputHash = CognitiveMemoryHash.FromUtf8("output").Value,
            AlgorithmVersion = "unit-test",
            SourceItemsScanned = 2,
            CandidatesCreated = 1,
            ReviewItemsCreated = 1,
            FailureCode = "FixtureFailure",
            FailureMessage = "Fixture consolidation failure.",
            StartedAtUtc = now,
            CompletedAtUtc = now.AddMinutes(2),
            ConcurrencyToken = Guid.NewGuid()
        };
        var projectionState = new CognitiveMemoryProjectionStateRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            ProjectionKind = CognitiveMemoryProjectionKind.VectorCollection,
            TargetProvider = "qdrant",
            ProjectionSchemaVersion = "unit-test",
            AlgorithmVersion = "unit-test",
            Status = CognitiveMemoryProjectionStatus.RebuildRequired,
            LastSourceHash = CognitiveMemoryHash.FromUtf8("projection-source").Value,
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
            RiskScoreEvaluationTraceId = Guid.NewGuid(),
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
            PriorityScoreEvaluationTraceId = Guid.NewGuid(),
            PriorityBucket = CognitiveMemoryScoreProjectionBucket.NeedsReview,
            DisplayPriorityProjection = 0.77,
            QueuePriority = 77,
            InputHash = CognitiveMemoryHash.FromUtf8("replay-input").Value,
            ExpectedOutputSchema = "procedure-validation",
            AlgorithmVersion = "unit-test",
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        var probeSession = new CognitiveMemoryProbeSessionRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Status = CognitiveMemoryProbeSessionStatus.Active,
            RecallMode = CognitiveMemoryRecallMode.FocusedTaskContext,
            Title = "Docker memory oral exam",
            ActorId = "agent:test",
            PolicyProfileId = "policy:test",
            AlgorithmVersion = "unit-test",
            TurnCount = 2,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        var selfRegulationAssessment = new CognitiveMemorySelfRegulationAssessmentRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            RecallTraceId = recallTrace.Id,
            ActorId = "agent:test",
            ModelProfileId = new CognitiveMemoryModelProfileId("unit-model"),
            DomainKey = "docker",
            TaskTypeKey = "deployment",
            State = CognitiveMemorySelfRegulationStateKind.SourcePoor,
            AssessmentScoreEvaluationTraceId = Guid.NewGuid(),
            AssessmentBucket = CognitiveMemoryScoreProjectionBucket.NeedsReview,
            DisplayAssessmentScore = 0.38,
            WarningsJson = "[\"source-poor\"]",
            RequiredOperationsJson = "[\"SourceAudit\"]",
            AlgorithmVersion = "unit-test",
            CreatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        var answerGateDecision = new CognitiveMemoryAnswerGateDecisionRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            RecallTraceId = recallTrace.Id,
            SelfRegulationAssessmentId = selfRegulationAssessment.Id,
            DecisionKind = CognitiveMemoryAnswerGateDecisionKind.SourceAudit,
            ScoreEvaluationTraceId = Guid.NewGuid(),
            DecisionBucket = CognitiveMemoryScoreProjectionBucket.NeedsReview,
            DisplayConfidenceProjection = 0.41,
            WarningsJson = "[\"source-sufficiency-limited\"]",
            RequiredOperationsJson = "[\"SourceAudit\"]",
            Reason = "Answer gate selected source audit.",
            DraftAnswerSummary = "Docker deployment answer needs more source evidence.",
            CreatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        var professorReview = new CognitiveMemoryProfessorReviewRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            ReviewMode = CognitiveMemoryProfessorReviewMode.SourceSufficiencyReview,
            Status = CognitiveMemoryProfessorReviewStatus.Requested,
            RequestedByActorId = "agent:test",
            ModelProfileId = new CognitiveMemoryModelProfileId("unit-professor"),
            PromptProfileVersion = "unit-test",
            PolicyProfileId = "policy:test",
            RoutingScoreEvaluationTraceId = Guid.NewGuid(),
            InputSummary = "Review source sufficiency for Docker rollback guidance.",
            ContextSummary = "[redacted by unit test]",
            OutputHash = CognitiveMemoryHash.FromUtf8("professor-request").Value,
            RequiresHumanReview = true,
            CreatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        var learningProposal = new CognitiveMemoryLearningProposalRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            KnowledgeGapId = Guid.NewGuid(),
            Status = CognitiveMemoryLearningProposalStatus.PendingApproval,
            Title = "Expand Docker rollback source coverage",
            Explanation = "Source audit found thin rollback evidence.",
            EvidenceRefsJson = "[]",
            Risks = new CognitiveMemoryRiskNotes("Learning output must stay review-gated."),
            AcceptanceCriteria = "Add source refs before canonical truth.",
            NeedScoreEvaluationTraceId = Guid.NewGuid(),
            NeedBucket = CognitiveMemoryScoreProjectionBucket.NeedsReview,
            DisplayPriorityProjection = 0.82,
            CreatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        var crossProjectPromotion = new CognitiveMemoryCrossProjectPromotionCandidateRecord
        {
            Id = Guid.NewGuid(),
            SourceProjectId = projectId,
            SourceMemoryRecordId = memoryRecord.Id,
            Status = CognitiveMemoryCrossProjectPromotionStatus.PendingReview,
            PromotionScoreEvaluationTraceId = Guid.NewGuid(),
            PromotionBucket = CognitiveMemoryScoreProjectionBucket.NeedsReview,
            RequestedByActorId = "agent:test",
            Reason = "Candidate may be reusable after review.",
            ReviewItemId = reviewItem.Id,
            CreatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        var distributedJob = new CognitiveMemoryDistributedJobRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            JobKind = CognitiveMemoryDistributedJobKind.ReplayAnalysis,
            State = CognitiveMemoryDistributedJobState.Rejected,
            SourceScopeKey = "project:docker",
            InputPayloadJson = "{}",
            InputHash = CognitiveMemoryHash.FromUtf8("{}").Value,
            ExpectedOutputSchema = "unit-schema",
            AlgorithmVersion = "unit-test",
            PolicyProfileId = "policy:test",
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        var retentionCleanupRun = new CognitiveMemoryRunRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            RunKind = CognitiveMemoryRunKind.RetentionCleanup,
            Status = CognitiveMemoryRunStatus.Succeeded,
            OperationMode = CognitiveMemoryOperationMode.Maintenance,
            IdempotencyKey = "unit-test-retention-cleanup",
            InputHash = CognitiveMemoryHash.FromUtf8("retention-cleanup").Value,
            AlgorithmVersion = "unit-test",
            Cursor = "RecallTraces,ProbeSessions",
            StartedAtUtc = now,
            CompletedAtUtc = now.AddMinutes(3),
            ConcurrencyToken = Guid.NewGuid()
        };

        dbContext.AddRange(
            sourceManifest,
            sourceItem,
            memoryRecord,
            sourceLink,
            evidenceAnchor,
            claim,
            mutationCommand,
            mutationAuditEvent,
            procedureSkill,
            reviewItem,
            recallTrace,
            recallStage,
            recallCandidate,
            sourceReference,
            consolidationRun,
            projectionState,
            simulation,
            replayJob,
            probeSession,
            selfRegulationAssessment,
            answerGateDecision,
            professorReview,
            learningProposal,
            crossProjectPromotion,
            distributedJob,
            retentionCleanupRun);
        await dbContext.SaveChangesAsync();
        return reviewItem.Id;
    }

    private static async Task SeedQualityEvidenceAsync(TestFixture fixture, Guid projectId)
    {
        await using var dbContext = fixture.Factory.CreateDbContext();
        var now = fixture.Clock.GetUtcNow();
        var recallTraceId = await dbContext.Set<CognitiveMemoryRecallTraceRecord>()
            .Where(trace => trace.ProjectId == projectId)
            .Select(trace => trace.Id)
            .FirstAsync();
        var memoryRecordId = await dbContext.Set<CognitiveMemoryRecord>()
            .Where(record => record.ProjectId == projectId)
            .Select(record => record.Id)
            .FirstAsync();
        var sourceItemId = await dbContext.Set<CognitiveMemorySourceItemRecord>()
            .Where(item => item.ProjectId == projectId)
            .Select(item => item.Id)
            .FirstAsync();
        var evidenceAnchorId = await dbContext.Set<CognitiveMemoryEvidenceAnchorRecord>()
            .Where(anchor => anchor.ProjectId == projectId)
            .Select(anchor => anchor.Id)
            .FirstAsync();

        for (var index = 0; index < 3; index++)
        {
            var clusterId = Guid.NewGuid();
            var dreamRunId = Guid.NewGuid();
            var primaryKeyFamily = index == 2
                ? CognitiveMemoryQualityClusterKeyFamily.ProjectScope
                : CognitiveMemoryQualityClusterKeyFamily.SemanticTopic;
            dbContext.Add(new CognitiveMemoryQualityClusterRecord
            {
                Id = clusterId,
                ProjectId = projectId,
                ClusterHash = CognitiveMemoryHash.FromUtf8($"cluster-{index}").Value,
                PrimaryKeyFamily = primaryKeyFamily,
                Readiness = index == 0
                    ? CognitiveMemoryQualityClusterReadiness.NeedsHumanReview
                    : CognitiveMemoryQualityClusterReadiness.AggregateReady,
                AccessLevel = CognitiveMemoryAccessLevel.Project,
                RiskLevel = index == 0 ? CognitiveMemoryRiskLevel.High : CognitiveMemoryRiskLevel.Low,
                PolicyProfileId = "unit-test",
                AlgorithmVersion = "unit-test",
                KeyCount = 2,
                MemberCount = 3 + index,
                SourceEvidenceCount = 4 + index,
                ContradictionCount = index == 0 ? 1 : 0,
                CreatedAtUtc = now.AddMinutes(index),
                UpdatedAtUtc = now.AddMinutes(index),
                ConcurrencyToken = Guid.NewGuid()
            });
            dbContext.Add(new CognitiveMemoryQualityClusterKeyRecord
            {
                Id = Guid.NewGuid(),
                ClusterId = clusterId,
                ProjectId = projectId,
                KeyFamily = primaryKeyFamily,
                Key = index switch
                {
                    0 => "topic:rollback",
                    1 => "topic:validation-dreaming",
                    _ => "project:source-truth-transfer"
                },
                DisplayText = index switch
                {
                    0 => "Rollback high risk cluster",
                    1 => "Validation dreaming cluster",
                    _ => "Project transfer source truth"
                },
                CreatedAtUtc = now.AddMinutes(index)
            });
            dbContext.Add(new CognitiveMemoryQualityClusterKeyRecord
            {
                Id = Guid.NewGuid(),
                ClusterId = clusterId,
                ProjectId = projectId,
                KeyFamily = CognitiveMemoryQualityClusterKeyFamily.TaskIntent,
                Key = $"task:intent-{index}",
                DisplayText = $"Approval probe intent {index}",
                CreatedAtUtc = now.AddMinutes(index)
            });
            dbContext.Add(new CognitiveMemoryQualityClusterMemberRecord
            {
                Id = Guid.NewGuid(),
                ClusterId = clusterId,
                ProjectId = projectId,
                MemberKind = CognitiveMemoryQualityClusterMemberKind.MemoryRecord,
                MemoryRecordId = memoryRecordId,
                EvidenceAnchorId = evidenceAnchorId,
                AccessLevel = CognitiveMemoryAccessLevel.Project,
                RiskLevel = index == 0 ? CognitiveMemoryRiskLevel.High : CognitiveMemoryRiskLevel.Low,
                ValidationState = index == 0
                    ? CognitiveMemoryValidationState.NeedsHumanReview
                    : CognitiveMemoryValidationState.Approved,
                StabilityState = CognitiveMemoryStabilityState.Active,
                CreatedAtUtc = now.AddMinutes(index)
            });
            dbContext.Add(new CognitiveMemoryQualityClusterMemberRecord
            {
                Id = Guid.NewGuid(),
                ClusterId = clusterId,
                ProjectId = projectId,
                MemberKind = CognitiveMemoryQualityClusterMemberKind.SourceItem,
                SourceItemId = sourceItemId,
                EvidenceAnchorId = evidenceAnchorId,
                AccessLevel = CognitiveMemoryAccessLevel.Project,
                RiskLevel = index == 0 ? CognitiveMemoryRiskLevel.High : CognitiveMemoryRiskLevel.Low,
                ValidationState = CognitiveMemoryValidationState.Approved,
                StabilityState = CognitiveMemoryStabilityState.Active,
                CreatedAtUtc = now.AddMinutes(index)
            });
            dbContext.Add(new CognitiveMemoryDreamRunRecord
            {
                Id = dreamRunId,
                ProjectId = projectId,
                Mode = CognitiveMemoryConsolidationMode.ProjectNightly,
                TriggerKind = CognitiveMemoryConsolidationTriggerKind.Manual,
                Status = index == 0 ? CognitiveMemoryRunStatus.Running : CognitiveMemoryRunStatus.Succeeded,
                IdempotencyKey = $"quality-unit-{index}",
                PolicyProfileId = "unit-test",
                AlgorithmVersion = "unit-test",
                ClustersConsidered = 3,
                AggregateCandidatesCreated = 1,
                ApprovedCandidates = index == 1 ? 1 : 0,
                NeedsReviewCandidates = index == 0 ? 1 : 0,
                RejectedCandidates = index == 2 ? 1 : 0,
                EvidenceCoverageRatio = 0.75,
                StartedAtUtc = now.AddMinutes(index),
                CompletedAtUtc = index == 0 ? null : now.AddMinutes(index + 1),
                ConcurrencyToken = Guid.NewGuid()
            });
            dbContext.Add(new CognitiveMemoryDreamAggregateCandidateRecord
            {
                Id = Guid.NewGuid(),
                DreamRunId = dreamRunId,
                ClusterId = clusterId,
                ProjectId = projectId,
                Mode = CognitiveMemoryConsolidationMode.ProjectNightly,
                Status = index == 0
                    ? CognitiveMemoryDreamAggregateCandidateStatus.NeedsHumanReview
                    : CognitiveMemoryDreamAggregateCandidateStatus.Proposed,
                Title = $"Aggregate candidate {index}",
                SummaryText = $"Aggregate candidate summary {index}.",
                CanonicalText = $"Aggregate candidate canonical text {index}.",
                AccessLevel = CognitiveMemoryAccessLevel.Project,
                RiskLevel = index == 0 ? CognitiveMemoryRiskLevel.High : CognitiveMemoryRiskLevel.Low,
                AlgorithmVersion = "unit-test",
                PayloadHash = CognitiveMemoryHash.FromUtf8($"candidate-{index}").Value,
                ClaimCount = 2,
                SourceMapCount = 3,
                CreatedAtUtc = now.AddMinutes(index),
                UpdatedAtUtc = now.AddMinutes(index),
                ConcurrencyToken = Guid.NewGuid()
            });
            dbContext.Add(new CognitiveMemorySynthesizedRecallRecord
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                RecallTraceId = recallTraceId,
                Brief = $"Synthesized recall {index}.",
                ReferencesShownByDefault = index % 2 == 0,
                StatementCount = 2,
                SourceMapCount = 3,
                CreatedAtUtc = now.AddMinutes(index),
                ConcurrencyToken = Guid.NewGuid()
            });
        }

        await dbContext.SaveChangesAsync();
    }

    private static TestFixture CreateFixture()
    {
        AppDbContextModelRegistry.ConfigureAssemblies([typeof(CognitiveMemoryModuleAssemblyMarker).Assembly]);
        var options = AppDbContextTestOptionsBuilder.Create()
            .UseInMemoryDatabase($"cognitive-memory-review-ui-{Guid.NewGuid():N}")
            .Options;
        return new TestFixture(new TestDbContextFactory(options), new FixedClock());
    }

    private static CognitiveMemoryReviewUiService CreateService(TestFixture fixture)
        => new(
            fixture.Factory,
            fixture.Clock,
            new CognitiveMemoryConsolidationCandidateApplicator(new CognitiveMemoryRecordValidator()));

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

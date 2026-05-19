using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Rag.Driver.Abstractions;
using CanDoItAll.AgentFramework.Rag.Driver.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class CognitiveMemoryOperationalServicesTests
{
    [Fact]
    public async Task ProjectionRebuildService_RebuildsStaleProjectionFromDurableMemory()
    {
        var fixture = CreateFixture();
        var graph = await SeedProjectionGraphAsync(fixture.Factory, includeClaim: true);
        var lifecycle = new RecordingProjectionLifecycleService();
        var service = new CognitiveMemoryProjectionRebuildService(
            fixture.Factory,
            lifecycle,
            fixture.Clock,
            NullLogger<CognitiveMemoryProjectionRebuildService>.Instance);

        var result = await service.RebuildAsync(new CognitiveMemoryProjectionRebuildRequest(
            graph.ProjectId,
            Take: 10,
            ActorId: "test:operator"));

        Assert.Equal(CognitiveMemoryRunStatus.Succeeded, result.Status);
        Assert.Equal(1, result.SelectedCount);
        Assert.Equal(1, result.ProjectedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(0, result.SkippedCount);
        var rebuildRequest = Assert.Single(lifecycle.Requests);
        Assert.Equal(graph.RecordId, rebuildRequest.MemoryRecord.Id);
        Assert.Equal("workbench", rebuildRequest.SourceSystem);
        Assert.Equal("node-1", rebuildRequest.SourceItemKey);
        Assert.Equal(graph.ClaimId, Assert.Single(rebuildRequest.ClaimPayload.ClaimIds).Value);

        await using var dbContext = fixture.Factory.CreateDbContext();
        var projection = await dbContext.Set<CognitiveMemoryProjectionRecord>().SingleAsync();
        Assert.Equal(CognitiveMemoryProjectionStatus.Projected, projection.Status);
        Assert.False(projection.RebuildRequired);
        Assert.Equal("rebuilt-point", projection.PointId);
    }

    [Fact]
    public async Task ProjectionRebuildService_SkipsProjectionWhenDurableInputsAreIncomplete()
    {
        var fixture = CreateFixture();
        var graph = await SeedProjectionGraphAsync(fixture.Factory, includeClaim: false);
        var lifecycle = new RecordingProjectionLifecycleService();
        var service = new CognitiveMemoryProjectionRebuildService(
            fixture.Factory,
            lifecycle,
            fixture.Clock,
            NullLogger<CognitiveMemoryProjectionRebuildService>.Instance);

        var result = await service.RebuildAsync(new CognitiveMemoryProjectionRebuildRequest(
            graph.ProjectId,
            Take: 10,
            ActorId: "test:operator"));

        Assert.Equal(CognitiveMemoryRunStatus.Succeeded, result.Status);
        Assert.Equal(1, result.SelectedCount);
        Assert.Equal(0, result.ProjectedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Empty(lifecycle.Requests);
        Assert.Contains(result.Warnings, warning => warning.Contains("no claims", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ProjectionRebuildService_RebuildsThroughRagProjectionAdapter()
    {
        var fixture = CreateFixture();
        var graph = await SeedProjectionGraphAsync(fixture.Factory, includeClaim: true);
        var rag = new RecordingRagDriver();
        var lifecycle = new CognitiveMemoryProjectionLifecycleService(
            new FixedEmbeddingProvider(new CognitiveMemoryEmbeddingProfileId("embedding-v1")),
            new RagCognitiveMemoryProjectionAdapter(rag),
            new CognitiveMemoryTaxonomyValidator(new CognitiveMemoryRecordValidator()),
            fixture.Clock,
            NullLogger<CognitiveMemoryProjectionLifecycleService>.Instance);
        var service = new CognitiveMemoryProjectionRebuildService(
            fixture.Factory,
            lifecycle,
            fixture.Clock,
            NullLogger<CognitiveMemoryProjectionRebuildService>.Instance);

        var result = await service.RebuildAsync(new CognitiveMemoryProjectionRebuildRequest(
            graph.ProjectId,
            Take: 10,
            ActorId: "test:operator"));

        Assert.Equal(CognitiveMemoryRunStatus.Succeeded, result.Status);
        Assert.Equal(1, result.ProjectedCount);
        var upsert = Assert.Single(rag.UpsertRequests);
        var knowledge = Assert.Single(upsert.Entries);
        Assert.Equal(graph.RecordId.ToString("D"), knowledge.Metadata["memoryRecordId"]);
        Assert.Equal("workbench", knowledge.Metadata["sourceSystem"]);
        Assert.NotNull(knowledge.Vector);

        await using var dbContext = fixture.Factory.CreateDbContext();
        var projection = await dbContext.Set<CognitiveMemoryProjectionRecord>().SingleAsync();
        Assert.Equal(CognitiveMemoryProjectionStatus.Projected, projection.Status);
        Assert.False(projection.RebuildRequired);
        Assert.Contains("fake-rag", Assert.Single(result.Items).ProviderTrace, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProjectionRebuildService_RecordsProviderFailureAndKeepsProjectionRebuildable()
    {
        var fixture = CreateFixture();
        var graph = await SeedProjectionGraphAsync(fixture.Factory, includeClaim: true);
        var service = new CognitiveMemoryProjectionRebuildService(
            fixture.Factory,
            new FailingProjectionLifecycleService("provider unavailable"),
            fixture.Clock,
            NullLogger<CognitiveMemoryProjectionRebuildService>.Instance);

        var result = await service.RebuildAsync(new CognitiveMemoryProjectionRebuildRequest(
            graph.ProjectId,
            Take: 10,
            ActorId: "test:operator"));

        Assert.Equal(CognitiveMemoryRunStatus.Blocked, result.Status);
        Assert.Equal(1, result.SelectedCount);
        Assert.Equal(0, result.ProjectedCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Contains(result.Warnings, warning => warning.Contains("provider unavailable", StringComparison.Ordinal));
        var item = Assert.Single(result.Items);
        Assert.Equal(CognitiveMemoryProjectionLifecycleDecisionKind.Failed, item.DecisionKind);
        Assert.Equal(CognitiveMemoryProjectionStatus.Failed, item.Status);
        Assert.Equal("provider unavailable", item.FailureMessage);

        await using var dbContext = fixture.Factory.CreateDbContext();
        var projection = await dbContext.Set<CognitiveMemoryProjectionRecord>().SingleAsync();
        Assert.Equal(CognitiveMemoryProjectionStatus.Failed, projection.Status);
        Assert.True(projection.RebuildRequired);
        Assert.Equal(CognitiveMemoryProjectionStaleReason.PreviousFailure, projection.StaleReason);
        Assert.Equal(nameof(InvalidOperationException), projection.FailureCode);
        Assert.Equal("provider unavailable", projection.FailureMessage);

        var run = await dbContext.Set<CognitiveMemoryRunRecord>().SingleAsync();
        Assert.Equal(CognitiveMemoryRunStatus.Blocked, run.Status);
        Assert.Equal("ProjectionRebuildFailures", run.FailureCode);
        Assert.Contains("1 projection rebuild item", run.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScheduledAutomationRunner_SkipsDisabledScheduleTrigger()
    {
        var settings = CognitiveMemoryAutomationSettings.Defaults(DateTimeOffset.UnixEpoch) with
        {
            ScheduleMode = CognitiveMemoryAutomationScheduleMode.ManualOnly
        };
        var ingestion = new RecordingSourceIngestionService();
        var consolidation = new RecordingConsolidationEngine();
        var runner = new CognitiveMemoryScheduledAutomationRunner(
            new FixedAutomationSettingsService(settings),
            ingestion,
            consolidation,
            new FixedClock());

        var result = await runner.RunAsync(new CognitiveMemoryScheduledAutomationRunRequest(
            ProjectId: Guid.NewGuid(),
            CognitiveMemoryAutomationTriggerKind.Nightly,
            ActorId: "test:operator"));

        Assert.False(result.Executed);
        Assert.Empty(ingestion.Requests);
        Assert.Empty(consolidation.Requests);
        Assert.Contains(result.Warnings, warning => warning.Contains("disabled", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ScheduledAutomationRunner_RunsEnabledSourceIngestionAndConsolidation()
    {
        var settings = CognitiveMemoryAutomationSettings.Defaults(DateTimeOffset.UnixEpoch) with
        {
            ScheduleMode = CognitiveMemoryAutomationScheduleMode.Nightly,
            AutoIngestProjectStructure = true,
            AutoIngestProcessRuntime = true,
            AutoConsolidateAfterIngestion = true
        };
        var ingestion = new RecordingSourceIngestionService();
        var consolidation = new RecordingConsolidationEngine();
        var projectId = Guid.NewGuid();
        var runner = new CognitiveMemoryScheduledAutomationRunner(
            new FixedAutomationSettingsService(settings),
            ingestion,
            consolidation,
            new FixedClock());

        var result = await runner.RunAsync(new CognitiveMemoryScheduledAutomationRunRequest(
            projectId,
            CognitiveMemoryAutomationTriggerKind.Nightly,
            ActorId: "test:operator",
            Take: 25));

        Assert.True(result.Executed);
        Assert.Equal(2, result.SourceIngestionRuns);
        Assert.Equal(12, result.SourceItemsSeen);
        Assert.Equal(5, result.SourceItemsCreated);
        Assert.Equal(1, result.ConsolidationRuns);
        Assert.Equal(CognitiveMemoryRunStatus.Succeeded, result.ConsolidationStatus);
        Assert.Equal(
            [MemorySourceKind.WorkbenchProjectStructure, MemorySourceKind.ProcessRuntime],
            ingestion.Requests.Select(request => request.SourceKind).ToArray());
        Assert.Equal(CognitiveMemoryConsolidationTriggerKind.Nightly, Assert.Single(consolidation.Requests).TriggerKind);
    }

    [Fact]
    public async Task RetentionCleanupService_DryRunReportsCountsWithoutDeleting()
    {
        var fixture = CreateFixture();
        var graph = await SeedRetentionGraphAsync(fixture.Factory);
        var service = new CognitiveMemoryRetentionCleanupService(
            fixture.Factory,
            fixture.Clock,
            NullLogger<CognitiveMemoryRetentionCleanupService>.Instance);

        var result = await service.CleanupAsync(new CognitiveMemoryRetentionCleanupRequest(
            graph.ProjectId,
            graph.CutoffUtc,
            DryRun: true,
            CognitiveMemoryRetentionCleanupRequest.DefaultScopes,
            ActorId: "test:operator"));

        Assert.True(result.DryRun);
        Assert.Equal(4, result.TotalMatchedRootRecords);
        Assert.Equal(0, result.TotalDeletedRecords);
        Assert.All(result.Scopes, scope => Assert.Equal(0, scope.DeletedRecords));

        await using var dbContext = fixture.Factory.CreateDbContext();
        var run = await dbContext.Set<CognitiveMemoryRunRecord>()
            .SingleAsync(item => item.RunKind == CognitiveMemoryRunKind.RetentionCleanup);
        Assert.Equal(CognitiveMemoryRunStatus.Succeeded, run.Status);
        Assert.Equal(CognitiveMemoryOperationMode.Observe, run.OperationMode);
        Assert.NotNull(await dbContext.Set<CognitiveMemoryRecallTraceRecord>().FindAsync(graph.OldRecallTraceId));
        Assert.NotNull(await dbContext.Set<CognitiveMemoryConsolidationCandidateRecord>().FindAsync(graph.OldRejectedCandidateId));
        Assert.NotNull(await dbContext.Set<CognitiveMemoryProbeSessionRecord>().FindAsync(graph.OldClosedProbeSessionId));
        Assert.NotNull(await dbContext.Set<CognitiveMemoryDistributedJobRecord>().FindAsync(graph.OldCompletedDistributedJobId));
    }

    [Fact]
    public async Task RetentionCleanupService_DeletesOnlyEligibleOperationalRecords()
    {
        var fixture = CreateFixture();
        var graph = await SeedRetentionGraphAsync(fixture.Factory);
        var service = new CognitiveMemoryRetentionCleanupService(
            fixture.Factory,
            fixture.Clock,
            NullLogger<CognitiveMemoryRetentionCleanupService>.Instance);

        var result = await service.CleanupAsync(new CognitiveMemoryRetentionCleanupRequest(
            graph.ProjectId,
            graph.CutoffUtc,
            DryRun: false,
            CognitiveMemoryRetentionCleanupRequest.DefaultScopes,
            ActorId: "test:operator"));

        Assert.False(result.DryRun);
        Assert.Equal(4, result.TotalMatchedRootRecords);
        Assert.Equal(15, result.TotalDeletedRecords);

        await using var dbContext = fixture.Factory.CreateDbContext();
        var run = await dbContext.Set<CognitiveMemoryRunRecord>()
            .SingleAsync(item => item.RunKind == CognitiveMemoryRunKind.RetentionCleanup);
        Assert.Equal(CognitiveMemoryRunStatus.Succeeded, run.Status);
        Assert.Equal(CognitiveMemoryOperationMode.Maintenance, run.OperationMode);
        Assert.Null(await dbContext.Set<CognitiveMemoryRecallTraceRecord>().FindAsync(graph.OldRecallTraceId));
        Assert.Empty(await dbContext.Set<CognitiveMemoryRecallTraceStageRecord>().Where(stage => stage.RecallTraceId == graph.OldRecallTraceId).ToListAsync());
        Assert.Empty(await dbContext.Set<CognitiveMemoryRecallCandidateRecord>().Where(candidate => candidate.RecallTraceId == graph.OldRecallTraceId).ToListAsync());
        Assert.Empty(await dbContext.Set<CognitiveMemoryRecallContextPackRecord>().Where(pack => pack.RecallTraceId == graph.OldRecallTraceId).ToListAsync());
        Assert.Empty(await dbContext.Set<CognitiveMemoryRecallContextSectionRecord>().Where(section => section.RecallTraceId == graph.OldRecallTraceId).ToListAsync());
        Assert.Empty(await dbContext.Set<CognitiveMemoryRecallSourceRefRecord>().Where(sourceRef => sourceRef.RecallTraceId == graph.OldRecallTraceId).ToListAsync());
        Assert.Null(await dbContext.Set<CognitiveMemoryConsolidationCandidateRecord>().FindAsync(graph.OldRejectedCandidateId));
        Assert.Null(await dbContext.Set<CognitiveMemoryProbeSessionRecord>().FindAsync(graph.OldClosedProbeSessionId));
        Assert.Empty(await dbContext.Set<CognitiveMemoryProbeTurnRecord>().Where(turn => turn.ProbeSessionId == graph.OldClosedProbeSessionId).ToListAsync());
        Assert.Null(await dbContext.Set<CognitiveMemoryDistributedJobRecord>().FindAsync(graph.OldCompletedDistributedJobId));
        Assert.Empty(await dbContext.Set<CognitiveMemoryDistributedWorkerResultRecord>().Where(result => result.DistributedJobId == graph.OldCompletedDistributedJobId).ToListAsync());

        Assert.NotNull(await dbContext.Set<CognitiveMemoryRecallTraceRecord>().FindAsync(graph.FreshRecallTraceId));
        Assert.NotNull(await dbContext.Set<CognitiveMemoryConsolidationCandidateRecord>().FindAsync(graph.OldReviewRequiredCandidateId));
        Assert.NotNull(await dbContext.Set<CognitiveMemoryProbeSessionRecord>().FindAsync(graph.OldActiveProbeSessionId));
        Assert.NotNull(await dbContext.Set<CognitiveMemoryDistributedJobRecord>().FindAsync(graph.OldQueuedDistributedJobId));
    }

    private static TestFixture CreateFixture()
    {
        AppDbContextModelRegistry.ConfigureAssemblies([typeof(CognitiveMemoryModuleAssemblyMarker).Assembly]);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"cognitive-memory-p0-{Guid.NewGuid():N}")
            .Options;
        return new TestFixture(new TestDbContextFactory(options), new FixedClock());
    }

    private static async Task<ProjectionGraph> SeedProjectionGraphAsync(
        TestDbContextFactory factory,
        bool includeClaim)
    {
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var recordId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var claimId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var contextFrameId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var entityId = Guid.Parse("dddddddd-dddd-dddd-dddd-eeeeeeeeeeee");
        var sourceManifestId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        var sourceItemId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var evidenceAnchorId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var now = DateTimeOffset.UnixEpoch;

        await using var dbContext = factory.CreateDbContext();
        dbContext.Add(new CognitiveMemorySourceManifestRecord
        {
            Id = sourceManifestId,
            ProjectId = projectId,
            SourceSystem = "workbench",
            SourceScopeKey = projectId.ToString("D"),
            SourceSnapshotId = "snapshot-1",
            SnapshotHash = CognitiveMemoryHash.FromUtf8("snapshot").Value,
            ProviderVersion = "test",
            ScanStatus = CognitiveMemoryRunStatus.Succeeded,
            ObservedAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        dbContext.Add(new CognitiveMemorySourceItemRecord
        {
            Id = sourceItemId,
            SourceManifestId = sourceManifestId,
            ProjectId = projectId,
            SourceSystem = "workbench",
            SourceItemKey = "node-1",
            SourceItemType = "ProjectNode",
            Title = "Docker contexts",
            ContentText = "Docker local and production contexts are not substitutable.",
            ContentHash = CognitiveMemoryHash.FromUtf8("source-item").Value,
            AccessScope = projectId.ToString("D"),
            ObservedAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        dbContext.Add(new CognitiveMemoryEvidenceAnchorRecord
        {
            Id = evidenceAnchorId,
            ProjectId = projectId,
            SourceManifestId = sourceManifestId,
            SourceItemId = sourceItemId,
            SourceSystem = "workbench",
            Locator = "node-1",
            StructuredPath = "node",
            QuoteHash = CognitiveMemoryHash.FromUtf8("quote").Value,
            SourceHash = CognitiveMemoryHash.FromUtf8("source-item").Value,
            ObservedAtUtc = now,
            CreatedAtUtc = now
        });
        dbContext.Add(new CognitiveMemoryContextFrameRecord
        {
            Id = contextFrameId,
            ProjectId = projectId,
            FrameKind = CognitiveMemoryContextFrameKind.Composite,
            DisplayName = "Docker deployment contexts",
            ConfidenceBucket = CognitiveMemoryScoreProjectionBucket.StrongAccept,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        dbContext.Add(new CognitiveMemoryEntityRecord
        {
            Id = entityId,
            ProjectId = projectId,
            EntityKind = CognitiveMemoryEntityKind.TechnologyTopic,
            CanonicalName = "Docker deployment contexts",
            CanonicalNameKey = "docker.deployment.contexts",
            PrimaryContextFrameId = contextFrameId,
            ConfidenceBucket = CognitiveMemoryScoreProjectionBucket.StrongAccept,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        dbContext.Add(new CognitiveMemoryRecord
        {
            Id = recordId,
            ProjectId = projectId,
            Kind = CognitiveMemoryRecordKind.Semantic,
            Origin = CognitiveMemoryRecordOrigin.SourceDerived,
            Title = "Docker contexts",
            SummaryText = "Local Docker is not production evidence.",
            CanonicalText = "Docker local, CI, and production contexts are context separated.",
            TopicKey = "docker.contexts",
            ValidationState = CognitiveMemoryValidationState.Approved,
            StabilityState = CognitiveMemoryStabilityState.Active,
            CreatedInMode = CognitiveMemoryOperationMode.Observe,
            AlgorithmVersion = "taxonomy-v1",
            ContentHash = CognitiveMemoryHash.FromUtf8("record").Value,
            SourceEvidenceCount = 1,
            EvidenceAnchorCount = 1,
            PrimaryClaimId = includeClaim ? claimId : null,
            PrimaryContextFrameId = contextFrameId,
            AccessLevel = CognitiveMemoryAccessLevel.Project,
            RiskLevel = CognitiveMemoryRiskLevel.Low,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        dbContext.Add(new CognitiveMemorySourceLinkRecord
        {
            MemoryRecordId = recordId,
            SourceManifestId = sourceManifestId,
            SourceItemId = sourceItemId,
            EvidenceRole = CognitiveMemoryEvidenceRole.PrimarySource,
            QuoteHash = CognitiveMemoryHash.FromUtf8("quote").Value,
            Summary = "Workbench node.",
            CreatedAtUtc = now
        });
        dbContext.Add(new CognitiveMemoryRecordEvidenceAnchorRecord
        {
            MemoryRecordId = recordId,
            EvidenceAnchorId = evidenceAnchorId,
            EvidenceRole = CognitiveMemoryEvidenceRole.PrimarySource,
            Summary = "Anchor.",
            CreatedAtUtc = now
        });
        if (includeClaim)
        {
            dbContext.Add(new CognitiveMemoryClaimRecord
            {
                Id = claimId,
                ProjectId = projectId,
                MemoryRecordId = recordId,
                ClaimKind = CognitiveMemoryClaimKind.Fact,
                ClaimText = "Docker contexts are separated.",
                SubjectKey = "docker",
                PredicateKey = "has-context-boundary",
                ObjectKey = "production",
                PrimaryContextFrameId = contextFrameId,
                CurrentBeliefState = CognitiveMemoryBeliefStateKind.Supported,
                CurrentBeliefBucket = CognitiveMemoryScoreProjectionBucket.StrongAccept,
                ValidationState = CognitiveMemoryValidationState.Approved,
                StabilityState = CognitiveMemoryStabilityState.Active,
                AlgorithmVersion = "taxonomy-v1",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }

        dbContext.Add(new CognitiveMemoryProjectionRecord
        {
            ProjectId = projectId,
            MemoryRecordId = recordId,
            ProjectionStoreKind = CognitiveMemoryProjectionStoreKind.GenericRag,
            ProjectionKind = CognitiveMemoryProjectionKind.VectorCollection,
            TargetProviderName = "fake-rag",
            CollectionName = "cm-project-semantic",
            PointId = "old-point",
            ProjectionProfileId = "projection-v1",
            EmbeddingProfileId = "embedding-v1",
            ProjectionSchemaVersion = "projection-payload-v1",
            AlgorithmVersion = "taxonomy-v1",
            VectorDimensions = 3,
            SourceHash = CognitiveMemoryHash.FromUtf8("old-source").Value,
            PayloadHash = CognitiveMemoryHash.FromUtf8("old-payload").Value,
            Status = CognitiveMemoryProjectionStatus.RebuildRequired,
            StaleReason = CognitiveMemoryProjectionStaleReason.SourceHashChanged,
            RebuildRequired = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await dbContext.SaveChangesAsync();

        return new ProjectionGraph(projectId, recordId, claimId);
    }

    private static async Task<RetentionGraph> SeedRetentionGraphAsync(TestDbContextFactory factory)
    {
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-bbbbbbbbbbbb");
        var oldUtc = DateTimeOffset.UnixEpoch.AddDays(-60);
        var cutoffUtc = DateTimeOffset.UnixEpoch.AddDays(-30);
        var freshUtc = DateTimeOffset.UnixEpoch.AddDays(-10);
        var oldRecallTraceId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var freshRecallTraceId = Guid.Parse("10000000-0000-0000-0000-000000000002");
        var oldRejectedCandidateId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        var oldReviewRequiredCandidateId = Guid.Parse("20000000-0000-0000-0000-000000000002");
        var oldClosedProbeSessionId = Guid.Parse("30000000-0000-0000-0000-000000000001");
        var oldActiveProbeSessionId = Guid.Parse("30000000-0000-0000-0000-000000000002");
        var oldCompletedDistributedJobId = Guid.Parse("40000000-0000-0000-0000-000000000001");
        var oldQueuedDistributedJobId = Guid.Parse("40000000-0000-0000-0000-000000000002");
        var oldProbeTurnId = Guid.Parse("50000000-0000-0000-0000-000000000001");
        var oldRegressionCaseId = Guid.Parse("60000000-0000-0000-0000-000000000001");
        var oldContextPackId = Guid.Parse("70000000-0000-0000-0000-000000000001");

        await using var dbContext = factory.CreateDbContext();
        dbContext.AddRange(
            new CognitiveMemoryRecallTraceRecord
            {
                Id = oldRecallTraceId,
                ProjectId = projectId,
                RequestedByActorId = "test",
                PolicyProfileId = "test",
                RequestHash = CognitiveMemoryHash.FromUtf8("old-trace").Value,
                AlgorithmVersion = "test",
                Outcome = CognitiveMemoryRunStatus.Succeeded,
                StartedAtUtc = oldUtc,
                CompletedAtUtc = oldUtc
            },
            new CognitiveMemoryRecallTraceRecord
            {
                Id = freshRecallTraceId,
                ProjectId = projectId,
                RequestedByActorId = "test",
                PolicyProfileId = "test",
                RequestHash = CognitiveMemoryHash.FromUtf8("fresh-trace").Value,
                AlgorithmVersion = "test",
                Outcome = CognitiveMemoryRunStatus.Succeeded,
                StartedAtUtc = freshUtc,
                CompletedAtUtc = freshUtc
            },
            new CognitiveMemoryRecallTraceStageRecord
            {
                RecallTraceId = oldRecallTraceId,
                ProjectId = projectId,
                Status = CognitiveMemoryRecallStageStatus.Completed,
                StartedAtUtc = oldUtc,
                CompletedAtUtc = oldUtc
            },
            new CognitiveMemoryRecallCandidateRecord
            {
                RecallTraceId = oldRecallTraceId,
                ProjectId = projectId,
                MemoryRecordId = Guid.NewGuid(),
                ScoreEvaluationTraceId = Guid.NewGuid(),
                Title = "Old candidate",
                CreatedAtUtc = oldUtc
            },
            new CognitiveMemoryRecallContextPackRecord
            {
                Id = oldContextPackId,
                RecallTraceId = oldRecallTraceId,
                ProjectId = projectId,
                Title = "Old pack",
                CreatedAtUtc = oldUtc
            },
            new CognitiveMemoryRecallContextSectionRecord
            {
                ContextPackId = oldContextPackId,
                RecallTraceId = oldRecallTraceId,
                ProjectId = projectId,
                SectionKey = "old",
                Title = "Old section",
                CreatedAtUtc = oldUtc
            },
            new CognitiveMemoryRecallSourceRefRecord
            {
                RecallTraceId = oldRecallTraceId,
                ContextPackId = oldContextPackId,
                ProjectId = projectId,
                MemoryRecordId = Guid.NewGuid(),
                SourceSystem = "test",
                Locator = "old",
                QuoteHash = CognitiveMemoryHash.FromUtf8("old-source-ref").Value,
                CreatedAtUtc = oldUtc
            },
            new CognitiveMemoryConsolidationCandidateRecord
            {
                Id = oldRejectedCandidateId,
                RunId = Guid.NewGuid(),
                ProjectId = projectId,
                Status = CognitiveMemoryConsolidationCandidateStatus.Rejected,
                SourceContentHash = CognitiveMemoryHash.FromUtf8("old-rejected").Value,
                OutputHash = CognitiveMemoryHash.FromUtf8("old-rejected-output").Value,
                AlgorithmVersion = "test",
                CreatedAtUtc = oldUtc
            },
            new CognitiveMemoryConsolidationCandidateRecord
            {
                Id = oldReviewRequiredCandidateId,
                RunId = Guid.NewGuid(),
                ProjectId = projectId,
                Status = CognitiveMemoryConsolidationCandidateStatus.ReviewRequired,
                SourceContentHash = CognitiveMemoryHash.FromUtf8("old-review-required").Value,
                OutputHash = CognitiveMemoryHash.FromUtf8("old-review-required-output").Value,
                AlgorithmVersion = "test",
                CreatedAtUtc = oldUtc
            },
            new CognitiveMemoryConsolidationCandidateRecord
            {
                RunId = Guid.NewGuid(),
                ProjectId = projectId,
                Status = CognitiveMemoryConsolidationCandidateStatus.Rejected,
                SourceContentHash = CognitiveMemoryHash.FromUtf8("fresh-rejected").Value,
                OutputHash = CognitiveMemoryHash.FromUtf8("fresh-rejected-output").Value,
                AlgorithmVersion = "test",
                CreatedAtUtc = freshUtc
            },
            new CognitiveMemoryProbeSessionRecord
            {
                Id = oldClosedProbeSessionId,
                ProjectId = projectId,
                Status = CognitiveMemoryProbeSessionStatus.Closed,
                Title = "Old closed probe",
                CreatedAtUtc = oldUtc,
                UpdatedAtUtc = oldUtc,
                ClosedAtUtc = oldUtc,
                ConcurrencyToken = Guid.NewGuid()
            },
            new CognitiveMemoryProbeSessionRecord
            {
                Id = oldActiveProbeSessionId,
                ProjectId = projectId,
                Status = CognitiveMemoryProbeSessionStatus.Active,
                Title = "Old active probe",
                CreatedAtUtc = oldUtc,
                UpdatedAtUtc = oldUtc,
                ConcurrencyToken = Guid.NewGuid()
            },
            new CognitiveMemoryProbeTurnRecord
            {
                Id = oldProbeTurnId,
                ProbeSessionId = oldClosedProbeSessionId,
                ProjectId = projectId,
                Sequence = 1,
                Question = "Old question",
                RecallTraceId = oldRecallTraceId,
                ProbeScoreEvaluationTraceId = Guid.NewGuid(),
                CreatedAtUtc = oldUtc,
                UpdatedAtUtc = oldUtc,
                ConcurrencyToken = Guid.NewGuid()
            },
            new CognitiveMemoryProbeFeedbackRecord
            {
                ProbeTurnId = oldProbeTurnId,
                ProbeSessionId = oldClosedProbeSessionId,
                ProjectId = projectId,
                CreatedAtUtc = oldUtc,
                ConcurrencyToken = Guid.NewGuid()
            },
            new CognitiveMemoryProbeFindingRecord
            {
                ProbeTurnId = oldProbeTurnId,
                ProjectId = projectId,
                Summary = "Old finding",
                CreatedAtUtc = oldUtc
            },
            new CognitiveMemoryProbeRegressionTestCaseRecord
            {
                Id = oldRegressionCaseId,
                ProjectId = projectId,
                ProbeTurnId = oldProbeTurnId,
                Question = "Old regression",
                CreatedAtUtc = oldUtc,
                ConcurrencyToken = Guid.NewGuid()
            },
            new CognitiveMemoryProbeRegressionRunRecord
            {
                ProjectId = projectId,
                RegressionTestCaseId = oldRegressionCaseId,
                StartedAtUtc = oldUtc,
                CompletedAtUtc = oldUtc,
                ConcurrencyToken = Guid.NewGuid()
            },
            new CognitiveMemoryDistributedJobRecord
            {
                Id = oldCompletedDistributedJobId,
                ProjectId = projectId,
                State = CognitiveMemoryDistributedJobState.Completed,
                SourceScopeKey = "old",
                InputHash = CognitiveMemoryHash.FromUtf8("old-job").Value,
                ExpectedOutputSchema = "test",
                AlgorithmVersion = "test",
                PolicyProfileId = "test",
                CreatedAtUtc = oldUtc,
                UpdatedAtUtc = oldUtc,
                ConcurrencyToken = Guid.NewGuid()
            },
            new CognitiveMemoryDistributedWorkerResultRecord
            {
                DistributedJobId = oldCompletedDistributedJobId,
                ProjectId = projectId,
                Status = CognitiveMemoryDistributedResultStatus.Accepted,
                WorkerId = "worker-1",
                InputHash = CognitiveMemoryHash.FromUtf8("old-job").Value,
                OutputHash = CognitiveMemoryHash.FromUtf8("old-output").Value,
                AlgorithmVersion = "test",
                OutputSchema = "test",
                SubmittedAtUtc = oldUtc,
                AcceptedAtUtc = oldUtc,
                ConcurrencyToken = Guid.NewGuid()
            },
            new CognitiveMemoryDistributedJobRecord
            {
                Id = oldQueuedDistributedJobId,
                ProjectId = projectId,
                State = CognitiveMemoryDistributedJobState.Queued,
                SourceScopeKey = "old-queued",
                InputHash = CognitiveMemoryHash.FromUtf8("old-queued-job").Value,
                ExpectedOutputSchema = "test",
                AlgorithmVersion = "test",
                PolicyProfileId = "test",
                CreatedAtUtc = oldUtc,
                UpdatedAtUtc = oldUtc,
                ConcurrencyToken = Guid.NewGuid()
            },
            new CognitiveMemoryDistributedJobRecord
            {
                ProjectId = projectId,
                State = CognitiveMemoryDistributedJobState.Completed,
                SourceScopeKey = "fresh-completed",
                InputHash = CognitiveMemoryHash.FromUtf8("fresh-completed-job").Value,
                ExpectedOutputSchema = "test",
                AlgorithmVersion = "test",
                PolicyProfileId = "test",
                CreatedAtUtc = freshUtc,
                UpdatedAtUtc = freshUtc,
                ConcurrencyToken = Guid.NewGuid()
            });
        await dbContext.SaveChangesAsync();

        return new RetentionGraph(
            projectId,
            cutoffUtc,
            oldRecallTraceId,
            freshRecallTraceId,
            oldRejectedCandidateId,
            oldReviewRequiredCandidateId,
            oldClosedProbeSessionId,
            oldActiveProbeSessionId,
            oldCompletedDistributedJobId,
            oldQueuedDistributedJobId);
    }

    private sealed class RecordingProjectionLifecycleService : ICognitiveMemoryProjectionLifecycleService
    {
        public List<CognitiveMemoryProjectionLifecycleRequest> Requests { get; } = [];

        public CognitiveMemoryProjectionLifecycleDecision EvaluateLifecycle(CognitiveMemoryProjectionLifecycleEvaluationRequest request)
            => new(CognitiveMemoryProjectionLifecycleDecisionKind.Rebuild, CognitiveMemoryProjectionStaleReason.SourceHashChanged, "test");

        public ValueTask<CognitiveMemoryProjectionLifecycleResult> ProjectAsync(
            CognitiveMemoryProjectionLifecycleRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            var projection = new CognitiveMemoryProjectionRecord
            {
                ProjectId = request.MemoryRecord.ProjectId,
                MemoryRecordId = request.MemoryRecord.Id,
                ProjectionStoreKind = request.ProjectionStoreKind,
                ProjectionKind = request.ProjectionKind,
                TargetProviderName = request.TargetProviderName,
                CollectionName = request.CollectionName.Value,
                PointId = "rebuilt-point",
                ProjectionProfileId = request.ProjectionProfileId.Value,
                EmbeddingProfileId = request.EmbeddingProfileId.Value,
                ProjectionSchemaVersion = request.ProjectionSchemaVersion.Value,
                AlgorithmVersion = request.AlgorithmVersion.Value,
                VectorDimensions = 3,
                SourceHash = CognitiveMemoryHash.FromUtf8("rebuilt-source").Value,
                PayloadHash = CognitiveMemoryHash.FromUtf8("rebuilt-payload").Value,
                Status = CognitiveMemoryProjectionStatus.Projected,
                RebuildRequired = false,
                CreatedAtUtc = DateTimeOffset.UnixEpoch,
                UpdatedAtUtc = DateTimeOffset.UnixEpoch,
                LastProjectedAtUtc = DateTimeOffset.UnixEpoch
            };
            return ValueTask.FromResult(new CognitiveMemoryProjectionLifecycleResult(
                new CognitiveMemoryProjectionLifecycleDecision(
                    CognitiveMemoryProjectionLifecycleDecisionKind.Rebuild,
                    CognitiveMemoryProjectionStaleReason.SourceHashChanged,
                    "rebuilt"),
                projection,
                ProjectionWriteRequest: null,
                ProviderTrace: "fake-rag:projected"));
        }
    }

    private sealed class FailingProjectionLifecycleService(string failureMessage) : ICognitiveMemoryProjectionLifecycleService
    {
        public CognitiveMemoryProjectionLifecycleDecision EvaluateLifecycle(CognitiveMemoryProjectionLifecycleEvaluationRequest request)
            => new(CognitiveMemoryProjectionLifecycleDecisionKind.Rebuild, CognitiveMemoryProjectionStaleReason.PreviousFailure, "test");

        public ValueTask<CognitiveMemoryProjectionLifecycleResult> ProjectAsync(
            CognitiveMemoryProjectionLifecycleRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException(failureMessage);
        }
    }

    private sealed class FixedAutomationSettingsService(CognitiveMemoryAutomationSettings settings) : ICognitiveMemoryAutomationSettingsService
    {
        public ValueTask<CognitiveMemoryAutomationSettings> GetAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(settings);

        public ValueTask<CognitiveMemoryAutomationSettings> SaveAsync(
            CognitiveMemoryAutomationSettingsUpdate update,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class RecordingSourceIngestionService : ICognitiveMemorySourceIngestionService
    {
        public List<CognitiveMemorySourceIngestionRequest> Requests { get; } = [];

        public ValueTask<CognitiveMemorySourceIngestionResult> IngestAsync(
            CognitiveMemorySourceIngestionRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return ValueTask.FromResult(new CognitiveMemorySourceIngestionResult(
                CognitiveMemorySourceIngestionStatus.Ingested,
                Guid.NewGuid(),
                ManifestId: Guid.NewGuid(),
                NextCursor: null,
                HasMore: false,
                SourceItemCount: request.SourceKind == MemorySourceKind.WorkbenchProjectStructure ? 7 : 5,
                CreatedSourceItemCount: request.SourceKind == MemorySourceKind.WorkbenchProjectStructure ? 3 : 2,
                UpdatedSourceItemCount: 0,
                CreatedEvidenceAnchorCount: 0,
                CreatedContextHintCount: 0,
                CreatedLayoutCount: 0,
                CreatedGraphLinkCount: 0,
                CreatedTombstoneCount: 0,
                FailureId: null,
                FailureCode: null));
        }
    }

    private sealed class RecordingConsolidationEngine : ICognitiveMemoryConsolidationEngine
    {
        public List<CognitiveMemoryConsolidationRunRequest> Requests { get; } = [];

        public ValueTask<CognitiveMemoryConsolidationRunResult> RunAsync(
            CognitiveMemoryConsolidationRunRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return ValueTask.FromResult(new CognitiveMemoryConsolidationRunResult(
                CognitiveMemoryConsolidationRunId.New(),
                CognitiveMemoryRunStatus.Succeeded,
                SourceItemsScanned: 12,
                CandidatesCreated: 4,
                MutationCommandsSubmitted: 2,
                ReviewItemsCreated: 1,
                ProjectionInvalidations: 0,
                NextCursor: null,
                ReportHash: null,
                Warnings: []));
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

    private sealed record TestFixture(
        TestDbContextFactory Factory,
        FixedClock Clock);

    private sealed record ProjectionGraph(
        Guid ProjectId,
        Guid RecordId,
        Guid ClaimId);

    private sealed record RetentionGraph(
        Guid ProjectId,
        DateTimeOffset CutoffUtc,
        Guid OldRecallTraceId,
        Guid FreshRecallTraceId,
        Guid OldRejectedCandidateId,
        Guid OldReviewRequiredCandidateId,
        Guid OldClosedProbeSessionId,
        Guid OldActiveProbeSessionId,
        Guid OldCompletedDistributedJobId,
        Guid OldQueuedDistributedJobId);

    private sealed class FixedEmbeddingProvider(CognitiveMemoryEmbeddingProfileId profileId) : ICognitiveMemoryEmbeddingProvider
    {
        public ValueTask<CognitiveMemoryEmbeddingResult> EmbedAsync(
            CognitiveMemoryEmbeddingRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(profileId, request.EmbeddingProfileId);
            return ValueTask.FromResult(new CognitiveMemoryEmbeddingResult(
                request.EmbeddingProfileId,
                CognitiveMemoryHash.FromUtf8(request.Input),
                new CognitiveMemoryVector(new[] { 0.1f, 0.2f, 0.3f }),
                "fixed-embedding"));
        }
    }

    private sealed class RecordingRagDriver : IRagDriver
    {
        public string ProviderName => "fake-rag";

        public RagDriverCapabilities Capabilities => RagDriverCapabilities.WithTagsAndProjectionControls;

        public RagCollectionOptions DefaultCollection { get; } = new();

        public List<RagUpsertRequest> UpsertRequests { get; } = [];

        public ValueTask EnsureCollectionAsync(
            RagCollectionOptions? collection = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            collection?.Validate();
            return ValueTask.CompletedTask;
        }

        public ValueTask UpsertAsync(
            RagUpsertRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            request.Validate();
            UpsertRequests.Add(request);
            return ValueTask.CompletedTask;
        }

        public ValueTask DeleteAsync(
            RagDeleteRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            request.Validate();
            return ValueTask.CompletedTask;
        }

        public ValueTask DeleteByFilterAsync(
            RagDeleteByFilterRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            request.Validate();
            return ValueTask.CompletedTask;
        }

        public ValueTask<RagPayloadIndexResult> EnsurePayloadIndexAsync(
            RagPayloadIndexRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            request.Validate();
            return ValueTask.FromResult(new RagPayloadIndexResult
            {
                CollectionName = request.CollectionName,
                FieldName = request.FieldName,
                IndexKind = request.IndexKind,
                Status = RagPayloadIndexStatus.Ensured
            });
        }

        public ValueTask<IReadOnlyList<RagSearchResult>> SearchAsync(
            RagSearchRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            request.Validate();
            return ValueTask.FromResult<IReadOnlyList<RagSearchResult>>([]);
        }
    }
}

using CanDoItAll.AgentFramework.Core;
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
}

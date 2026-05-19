using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support.CognitiveMemory;

namespace CanDoItAll.Tests.Unit;

public sealed class CognitiveMemoryTaxonomyTests
{
    [Fact]
    public void TaxonomyValidator_RequiresSourceLinksAndEvidenceAnchors()
    {
        var validator = new CognitiveMemoryTaxonomyValidator(new CognitiveMemoryRecordValidator());
        var record = CreateRecord();

        var missing = validator.ValidateMemoryRecord(record, [], []);
        var valid = validator.ValidateMemoryRecord(record, [CreateSourceLink(record)], [CognitiveMemoryEvidenceAnchorId.New()]);

        Assert.True(missing.IsFailure);
        Assert.Contains(missing.Errors, error => error.Code == "cognitive-memory-source-link-required");
        Assert.Contains(missing.Errors, error => error.Code == "cognitive-memory-evidence-anchor-required");
        Assert.True(valid.IsSuccess);
    }

    [Fact]
    public void TaxonomyValidator_RejectsSelfRelationsAndContextSeparatedSameAs()
    {
        var validator = new CognitiveMemoryTaxonomyValidator(new CognitiveMemoryRecordValidator());
        var sourceId = CognitiveMemoryRecordId.New();
        var targetId = CognitiveMemoryRecordId.New();

        var self = validator.ValidateRelationDraft(new CognitiveMemoryRelationDraft(
            Guid.NewGuid(),
            sourceId,
            sourceId,
            CognitiveMemoryRelationKind.Supports,
            [CognitiveMemoryEvidenceAnchorId.New()],
            [],
            new CognitiveMemoryAlgorithmVersion("taxonomy-v1")));
        var collapsedContext = validator.ValidateRelationDraft(new CognitiveMemoryRelationDraft(
            Guid.NewGuid(),
            sourceId,
            targetId,
            CognitiveMemoryRelationKind.SameAs,
            [CognitiveMemoryEvidenceAnchorId.New()],
            [CognitiveMemoryContextBoundaryPolicy.RelatedNotSubstitutable],
            new CognitiveMemoryAlgorithmVersion("taxonomy-v1")));
        var relatedButSeparated = validator.ValidateRelationDraft(new CognitiveMemoryRelationDraft(
            Guid.NewGuid(),
            sourceId,
            targetId,
            CognitiveMemoryRelationKind.SemanticallyRelatedButContextSeparated,
            [CognitiveMemoryEvidenceAnchorId.New()],
            [CognitiveMemoryContextBoundaryPolicy.RelatedNotSubstitutable],
            new CognitiveMemoryAlgorithmVersion("taxonomy-v1")));

        Assert.Contains(self.Errors, error => error.Code == "cognitive-memory-relation-self-reference");
        Assert.Contains(collapsedContext.Errors, error => error.Code == "cognitive-memory-relation-context-separated-same-as");
        Assert.True(relatedButSeparated.IsSuccess);
    }

    [Fact]
    public async Task ProjectionLifecycle_ProjectAsync_BuildsStableProjectionPayloadAndCallsAdapter()
    {
        var adapter = new RecordingProjectionAdapter();
        var service = CreateLifecycleService(adapter, dimensions: 3);
        var record = CreateRecord();
        var evidenceAnchorId = CognitiveMemoryEvidenceAnchorId.New();
        var request = CreateLifecycleRequest(record, [CreateSourceLink(record)], [evidenceAnchorId]);

        var first = await service.ProjectAsync(request);
        var second = await service.ProjectAsync(request);

        Assert.Equal(CognitiveMemoryProjectionStatus.Projected, first.ProjectionRecord.Status);
        Assert.Equal(CognitiveMemoryProjectionLifecycleDecisionKind.Project, first.Decision.DecisionKind);
        Assert.Equal(first.ProjectionRecord.PointId, second.ProjectionRecord.PointId);
        Assert.Equal(first.ProjectionRecord.SourceHash, second.ProjectionRecord.SourceHash);
        Assert.Equal(first.ProjectionRecord.PayloadHash, second.ProjectionRecord.PayloadHash);
        Assert.Equal(3, first.ProjectionRecord.VectorDimensions);
        Assert.Equal("fake-rag", first.ProjectionRecord.TargetProviderName);

        Assert.Equal(2, adapter.ProjectRequests.Count);
        var write = adapter.ProjectRequests[0];
        var entry = Assert.Single(write.Entries);
        Assert.Equal(record.Id, entry.MemoryRecordId.Value);
        Assert.Equal("workbench", entry.SourceSystem);
        Assert.Equal("node-1", entry.SourceItemKey);
        Assert.NotNull(entry.EvidenceAnchorIds);
        Assert.Equal([evidenceAnchorId.Value.ToString("D")], entry.EvidenceAnchorIds.Select(id => id.Value.ToString("D")).ToArray());
        Assert.NotNull(entry.Metadata);
        Assert.Equal(first.ProjectionRecord.Id.ToString("D"), entry.Metadata["projectionRecordId"]);
    }

    [Fact]
    public async Task ProjectionLifecycle_ProjectFailure_ReturnsFailedProjectionWithoutMutatingMemoryRecord()
    {
        var adapter = new RecordingProjectionAdapter(throwOnProject: true);
        var service = CreateLifecycleService(adapter, dimensions: 3);
        var record = CreateRecord();
        var request = CreateLifecycleRequest(record, [CreateSourceLink(record)], [CognitiveMemoryEvidenceAnchorId.New()]);

        var result = await service.ProjectAsync(request);

        Assert.Equal(CognitiveMemoryProjectionLifecycleDecisionKind.Failed, result.Decision.DecisionKind);
        Assert.Equal(CognitiveMemoryProjectionStatus.Failed, result.ProjectionRecord.Status);
        Assert.True(result.ProjectionRecord.RebuildRequired);
        Assert.Equal(nameof(InvalidOperationException), result.ProjectionRecord.FailureCode);
        Assert.Null(result.ProjectionWriteRequest);
        Assert.Equal(1, record.SourceEvidenceCount);
        Assert.Equal(1, record.EvidenceAnchorCount);
    }

    [Fact]
    public void ProjectionLifecycle_EvaluatesMissingStaleAndTombstonedProjectionStates()
    {
        var adapter = new RecordingProjectionAdapter();
        var service = CreateLifecycleService(adapter, dimensions: 3);
        var sourceHash = CognitiveMemoryHash.FromUtf8("source-v1");
        var payloadHash = CognitiveMemoryHash.FromUtf8("payload-v1");
        var current = CreateProjectionRecord(sourceHash, payloadHash);

        var missing = service.EvaluateLifecycle(new CognitiveMemoryProjectionLifecycleEvaluationRequest(
            null,
            sourceHash,
            payloadHash,
            new CognitiveMemoryProjectionProfileId("projection-v1"),
            new CognitiveMemoryEmbeddingProfileId("embedding-v1"),
            new CognitiveMemoryPayloadSchemaVersion("projection-payload-v1"),
            new CognitiveMemoryAlgorithmVersion("taxonomy-v1"),
            SourceTombstoned: false));
        var stale = service.EvaluateLifecycle(new CognitiveMemoryProjectionLifecycleEvaluationRequest(
            current,
            CognitiveMemoryHash.FromUtf8("source-v2"),
            payloadHash,
            new CognitiveMemoryProjectionProfileId("projection-v1"),
            new CognitiveMemoryEmbeddingProfileId("embedding-v1"),
            new CognitiveMemoryPayloadSchemaVersion("projection-payload-v1"),
            new CognitiveMemoryAlgorithmVersion("taxonomy-v1"),
            SourceTombstoned: false));
        var tombstoned = service.EvaluateLifecycle(new CognitiveMemoryProjectionLifecycleEvaluationRequest(
            current,
            sourceHash,
            payloadHash,
            new CognitiveMemoryProjectionProfileId("projection-v1"),
            new CognitiveMemoryEmbeddingProfileId("embedding-v1"),
            new CognitiveMemoryPayloadSchemaVersion("projection-payload-v1"),
            new CognitiveMemoryAlgorithmVersion("taxonomy-v1"),
            SourceTombstoned: true));

        Assert.Equal(CognitiveMemoryProjectionLifecycleDecisionKind.Project, missing.DecisionKind);
        Assert.Equal(CognitiveMemoryProjectionStaleReason.MissingProjection, missing.StaleReason);
        Assert.Equal(CognitiveMemoryProjectionLifecycleDecisionKind.Rebuild, stale.DecisionKind);
        Assert.Equal(CognitiveMemoryProjectionStaleReason.SourceHashChanged, stale.StaleReason);
        Assert.Equal(CognitiveMemoryProjectionLifecycleDecisionKind.Delete, tombstoned.DecisionKind);
        Assert.Equal(CognitiveMemoryProjectionStaleReason.SourceTombstoned, tombstoned.StaleReason);
    }

    [Fact]
    public void TaxonomyRecords_UseScoreGeometryTraceIdsInsteadOfFinalRankFields()
    {
        var recordProperties = typeof(CognitiveMemoryRecord).GetProperties().Select(property => property.Name).ToList();
        var relationProperties = typeof(CognitiveMemoryRelationRecord).GetProperties().Select(property => property.Name).ToList();

        Assert.Contains(nameof(CognitiveMemoryRecord.ConfidenceScoreEvaluationTraceId), recordProperties);
        Assert.Contains(nameof(CognitiveMemoryRecord.ActivationScoreEvaluationTraceId), recordProperties);
        Assert.Contains(nameof(CognitiveMemoryRelationRecord.RelationScoreEvaluationTraceId), relationProperties);
        Assert.DoesNotContain("FinalRank", recordProperties);
        Assert.DoesNotContain("FinalScore", relationProperties);
    }

    private static CognitiveMemoryProjectionLifecycleService CreateLifecycleService(
        RecordingProjectionAdapter adapter,
        int dimensions)
        => new(
            new FakeCognitiveMemoryEmbeddingProvider(dimensions),
            adapter,
            new CognitiveMemoryTaxonomyValidator(new CognitiveMemoryRecordValidator()),
            new FixedClock(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CognitiveMemoryProjectionLifecycleService>.Instance);

    private static CognitiveMemoryProjectionLifecycleRequest CreateLifecycleRequest(
        CognitiveMemoryRecord record,
        IReadOnlyList<CognitiveMemorySourceLinkRecord> sourceLinks,
        IReadOnlyList<CognitiveMemoryEvidenceAnchorId> evidenceAnchorIds)
        => new(
            new CognitiveMemoryProjectionCollectionName("cm-project-semantic"),
            CognitiveMemoryProjectionStoreKind.GenericRag,
            "fake-rag",
            "workbench",
            "node-1",
            record,
            sourceLinks,
            new CognitiveMemoryClaimProjectionPayload(
                new CognitiveMemoryPayloadSchemaVersion("projection-payload-v1"),
                CognitiveMemoryProjectionPayloadSchemaKind.ClaimContainer,
                new CognitiveMemoryRecordId(record.Id),
                [CognitiveMemoryClaimId.New()],
                [CognitiveMemoryContextFrameId.New()],
                [CognitiveMemoryEntityId.New()],
                [CognitiveMemoryBeliefStateKind.Supported],
                [CognitiveMemoryContextBoundaryPolicy.RelatedNotSubstitutable],
                CognitiveMemoryScoreProjectionBucket.StrongAccept),
            evidenceAnchorIds,
            CognitiveMemoryProjectionKind.VectorCollection,
            new CognitiveMemoryProjectionProfileId("projection-v1"),
            new CognitiveMemoryEmbeddingProfileId("embedding-v1"),
            new CognitiveMemoryPayloadSchemaVersion("projection-payload-v1"),
            new CognitiveMemoryAlgorithmVersion("taxonomy-v1"),
            new CognitiveMemoryProcessingBudget(1, 4096, TimeSpan.FromSeconds(5)),
            ExpectedVectorDimensions: 3,
            Tags: ["taxonomy"]);

    private static CognitiveMemoryRecord CreateRecord()
        => new()
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            ProjectId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Kind = CognitiveMemoryRecordKind.Semantic,
            Origin = CognitiveMemoryRecordOrigin.SourceDerived,
            Title = "Docker contexts are distinct",
            SummaryText = "Local Docker simulations must not substitute for production deployment evidence.",
            CanonicalText = "Docker local, CI, test, and production contexts are related but context separated.",
            TopicKey = "docker.contexts",
            ValidationState = CognitiveMemoryValidationState.Approved,
            StabilityState = CognitiveMemoryStabilityState.Active,
            CreatedInMode = CognitiveMemoryOperationMode.Observe,
            AlgorithmVersion = "taxonomy-v1",
            ContentHash = CognitiveMemoryHash.FromUtf8("docker-contexts").Value,
            SourceEvidenceCount = 1,
            EvidenceAnchorCount = 1,
            AccessLevel = CognitiveMemoryAccessLevel.Project,
            RiskLevel = CognitiveMemoryRiskLevel.Low,
            CreatedAtUtc = DateTimeOffset.UnixEpoch,
            UpdatedAtUtc = DateTimeOffset.UnixEpoch
        };

    private static CognitiveMemorySourceLinkRecord CreateSourceLink(CognitiveMemoryRecord record)
        => new()
        {
            MemoryRecordId = record.Id,
            SourceManifestId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            SourceItemId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            EvidenceRole = CognitiveMemoryEvidenceRole.PrimarySource,
            QuoteHash = CognitiveMemoryHash.FromUtf8("quote").Value,
            Summary = "Workbench source node.",
            CreatedAtUtc = DateTimeOffset.UnixEpoch
        };

    private static CognitiveMemoryProjectionRecord CreateProjectionRecord(
        CognitiveMemoryHash sourceHash,
        CognitiveMemoryHash payloadHash)
        => new()
        {
            ProjectId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            MemoryRecordId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            ProjectionStoreKind = CognitiveMemoryProjectionStoreKind.GenericRag,
            ProjectionKind = CognitiveMemoryProjectionKind.VectorCollection,
            TargetProviderName = "fake-rag",
            CollectionName = "cm-project-semantic",
            PointId = "point-1",
            ProjectionProfileId = "projection-v1",
            EmbeddingProfileId = "embedding-v1",
            ProjectionSchemaVersion = "projection-payload-v1",
            AlgorithmVersion = "taxonomy-v1",
            VectorDimensions = 3,
            SourceHash = sourceHash.Value,
            PayloadHash = payloadHash.Value,
            Status = CognitiveMemoryProjectionStatus.Projected,
            LastProjectedAtUtc = DateTimeOffset.UnixEpoch
        };

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
    }

    private sealed class RecordingProjectionAdapter(bool throwOnProject = false) : ICognitiveMemoryProjectionAdapter
    {
        public CognitiveMemoryProjectionAdapterCapabilities Capabilities { get; } = new(
            "fake-rag",
            SupportsFilters: true,
            SupportsPayloadIndexes: true,
            SupportsDeleteByFilter: true,
            SupportsNamedVectors: false);

        public List<CognitiveMemoryProjectionWriteRequest> ProjectRequests { get; } = [];

        public ValueTask EnsureCollectionAsync(
            CognitiveMemoryProjectionCollectionRequest request,
            CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask<IReadOnlyList<CognitiveMemoryProjectionPayloadIndexResult>> EnsurePayloadIndexesAsync(
            CognitiveMemoryProjectionPayloadIndexRequest request,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyList<CognitiveMemoryProjectionPayloadIndexResult>>([]);

        public ValueTask ProjectAsync(
            CognitiveMemoryProjectionWriteRequest request,
            CancellationToken cancellationToken = default)
        {
            if (throwOnProject)
            {
                throw new InvalidOperationException("fake projection outage");
            }

            ProjectRequests.Add(request);
            return ValueTask.CompletedTask;
        }

        public ValueTask<CognitiveMemoryProjectionSearchResult> SearchAsync(
            CognitiveMemoryProjectionSearchRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask<CognitiveMemoryProjectionDeleteResult> DeleteBySourceAsync(
            CognitiveMemoryProjectionDeleteBySourceRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}

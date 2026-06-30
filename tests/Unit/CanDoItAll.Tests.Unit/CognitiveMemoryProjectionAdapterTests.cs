using CanDoItAll.AgentFramework.Rag.Driver.Abstractions;
using CanDoItAll.AgentFramework.Rag.Driver.Models;
using CanDoItAll.AgentFramework.SemanticCompletion.Driver.Embeddings;
using CanDoItAll.AgentFramework.SemanticCompletion.Driver.Semantics;
using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class CognitiveMemoryProjectionAdapterTests
{
    [Fact]
    public async Task SemanticEmbeddingAdapter_RejectsProfileMismatchInsteadOfFallback()
    {
        var generator = new FakeEmbeddingGenerator("actual-profile");
        var adapter = new SemanticCompletionCognitiveMemoryEmbeddingProvider(generator, new FixedClock());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await adapter.EmbedAsync(new CognitiveMemoryEmbeddingRequest(
                new CognitiveMemoryEmbeddingProfileId("expected-profile"),
                "recall project deployment evidence",
                Budget())));

        Assert.Contains("Semantic embedding profile mismatch", exception.Message, StringComparison.Ordinal);
        Assert.Contains("expected-profile", exception.Message, StringComparison.Ordinal);
        Assert.Contains("actual-profile", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SemanticEmbeddingAdapter_MapsExactProfileAndCopiesVector()
    {
        var sourceVector = new[] { 0.25f, 0.5f, 0.75f };
        var generator = new FakeEmbeddingGenerator("cm-embedding-v1", sourceVector);
        var adapter = new SemanticCompletionCognitiveMemoryEmbeddingProvider(generator, new FixedClock());

        var result = await adapter.EmbedAsync(new CognitiveMemoryEmbeddingRequest(
            new CognitiveMemoryEmbeddingProfileId("cm-embedding-v1"),
            "recall project deployment evidence",
            Budget()));

        Assert.Equal(new CognitiveMemoryEmbeddingProfileId("cm-embedding-v1"), result.EmbeddingProfileId);
        Assert.Equal(sourceVector, result.Vector.ToArrayForAdapterBoundary());
        Assert.NotSame(sourceVector, result.Vector.ToArrayForAdapterBoundary());
        Assert.Contains("semantic-completion", result.ProviderTrace, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SemanticClassifierAdapter_MapsProviderDecisionAndMatches()
    {
        var classifier = new FakeSemanticClassifier<RecallIntent>(new SemanticClassificationResult<RecallIntent>(
            RecallIntent.ProjectEvidence,
            SemanticClassificationDecision.Accepted,
            0.91f,
            0.22f,
            "project-evidence",
            "deployment evidence",
            [new SemanticClassificationMatch<RecallIntent>(RecallIntent.ProjectEvidence, "project-evidence", "deployment evidence", 0.91f)],
            []));
        var adapter = new SemanticCompletionCognitiveMemoryClassifier<RecallIntent>(classifier, new FixedClock());

        var result = await adapter.ClassifyAsync(new CognitiveMemorySemanticClassificationRequest(
            "show deployment evidence",
            Budget()));

        Assert.Equal(RecallIntent.ProjectEvidence, result.Label);
        Assert.Equal(CognitiveMemorySemanticClassificationDecision.Accepted, result.Decision);
        Assert.Single(result.Matches);
        Assert.Equal("project-evidence", classifier.LastRequest?.Metadata?["intentScope"]);
    }

    [Fact]
    public async Task RagProjectionAdapter_RejectsPayloadIndexesWhenProviderDoesNotSupportThem()
    {
        var rag = new FakeRagDriver(RagDriverCapabilities.None);
        var adapter = new RagCognitiveMemoryProjectionAdapter(rag);

        await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await adapter.EnsurePayloadIndexesAsync(new CognitiveMemoryProjectionPayloadIndexRequest(
                CollectionName(),
                [new CognitiveMemoryProjectionPayloadIndexSpec(CognitiveMemoryProjectionPayloadField.MemoryRecordId, CognitiveMemoryProjectionPayloadIndexKind.Keyword)])));
    }

    [Fact]
    public async Task RagProjectionAdapter_EnsuresTypedPayloadIndexes()
    {
        var rag = new FakeRagDriver(RagDriverCapabilities.WithTagsAndProjectionControls);
        var adapter = new RagCognitiveMemoryProjectionAdapter(rag);

        var results = await adapter.EnsurePayloadIndexesAsync(new CognitiveMemoryProjectionPayloadIndexRequest(
            CollectionName(),
            [
                new CognitiveMemoryProjectionPayloadIndexSpec(CognitiveMemoryProjectionPayloadField.MemoryRecordId, CognitiveMemoryProjectionPayloadIndexKind.Keyword),
                new CognitiveMemoryProjectionPayloadIndexSpec(CognitiveMemoryProjectionPayloadField.UpdatedAtUtc, CognitiveMemoryProjectionPayloadIndexKind.DateTime)
            ]));

        Assert.Equal(2, rag.PayloadIndexRequests.Count);
        Assert.Equal("memoryRecordId", rag.PayloadIndexRequests[0].FieldName);
        Assert.Equal(RagPayloadIndexKind.Keyword, rag.PayloadIndexRequests[0].IndexKind);
        Assert.All(results, result => Assert.Equal(CognitiveMemoryProjectionPayloadIndexStatus.Ensured, result.Status));
    }

    [Fact]
    public async Task RagProjectionAdapter_ProjectRequiresEvidenceAnchors()
    {
        var rag = new FakeRagDriver(RagDriverCapabilities.WithTagsAndProjectionControls);
        var adapter = new RagCognitiveMemoryProjectionAdapter(rag);

        var entry = ProjectionEntry(evidenceAnchorIds: []);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await adapter.ProjectAsync(new CognitiveMemoryProjectionWriteRequest(
                CollectionName(),
                [entry],
                ExpectedVectorDimensions: 3)));
    }

    [Fact]
    public async Task RagProjectionAdapter_ProjectBuildsSourceEvidenceProjectionMetadata()
    {
        var rag = new FakeRagDriver(RagDriverCapabilities.WithTagsAndProjectionControls);
        var adapter = new RagCognitiveMemoryProjectionAdapter(rag);
        var sourceVector = new[] { 0.25f, 0.5f, 0.75f };
        var entry = ProjectionEntry(vector: new CognitiveMemoryVector(sourceVector));

        await adapter.ProjectAsync(new CognitiveMemoryProjectionWriteRequest(
            CollectionName(),
            [entry],
            ExpectedVectorDimensions: sourceVector.Length));

        var upsert = Assert.Single(rag.UpsertRequests);
        var knowledge = Assert.Single(upsert.Entries);
        Assert.Equal("cm-projection-point-1", knowledge.Id);
        Assert.NotNull(knowledge.Vector);
        var projectedVector = knowledge.Vector;
        Assert.NotSame(sourceVector, projectedVector);
        Assert.Equal(sourceVector, projectedVector);
        Assert.Equal(entry.ProjectId?.ToString("D"), knowledge.Metadata["projectId"]);
        Assert.Equal(entry.MemoryRecordId.Value.ToString("D"), knowledge.Metadata["memoryRecordId"]);
        Assert.Equal(entry.PayloadHash.Value, knowledge.Metadata["payloadHash"]);
        Assert.Equal(entry.SourceHash.Value, knowledge.Metadata["sourceHash"]);
        Assert.Equal(entry.AccessLevel.ToString(), knowledge.Metadata["accessLevel"]);

        var claimIds = Assert.IsType<string[]>(knowledge.Metadata["claimId"]);
        var contextFrameIds = Assert.IsType<string[]>(knowledge.Metadata["contextFrameId"]);
        var evidenceAnchorIds = Assert.IsType<string[]>(knowledge.Metadata["evidenceAnchorId"]);
        Assert.Single(claimIds);
        Assert.Single(contextFrameIds);
        Assert.Single(evidenceAnchorIds);
    }

    [Fact]
    public async Task RagProjectionAdapter_DeleteBySourceRejectsBroadDeleteAndUsesTypedFilter()
    {
        var rag = new FakeRagDriver(RagDriverCapabilities.WithTagsAndProjectionControls);
        var adapter = new RagCognitiveMemoryProjectionAdapter(rag);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await adapter.DeleteBySourceAsync(new CognitiveMemoryProjectionDeleteBySourceRequest(
                CollectionName(),
                ProjectId: Guid.NewGuid(),
                SourceSystem: "workbench")));

        var sourceHash = CognitiveMemoryHash.FromUtf8("source-item-1");
        await adapter.DeleteBySourceAsync(new CognitiveMemoryProjectionDeleteBySourceRequest(
            CollectionName(),
            ProjectId: Guid.NewGuid(),
            SourceSystem: "workbench",
            SourceItemKeys: ["node-1"],
            SourceHashes: [sourceHash]));

        var request = Assert.Single(rag.DeleteByFilterRequests);
        var filterGroup = Assert.IsType<RagFilterGroup>(request.Filter);
        Assert.Equal(RagFilterGroupOperator.All, filterGroup.Operator);
        Assert.Contains(filterGroup.Filters, filter => filter is RagFilterCondition condition &&
                                                       condition.FieldName == "sourceSystem" &&
                                                       condition.Operator == RagFilterOperator.Equal);
    }

    [Fact]
    public async Task RagProjectionAdapter_SearchRequiresProviderFilterSupport()
    {
        var rag = new FakeRagDriver(RagDriverCapabilities.None);
        var adapter = new RagCognitiveMemoryProjectionAdapter(rag);

        await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await adapter.SearchAsync(new CognitiveMemoryProjectionSearchRequest(
                CollectionName(),
                new CognitiveMemoryProjectionProfileId("cm-projection-v1"),
                "deployment evidence",
                new CognitiveMemoryVector(new[] { 1f, 0f, 0f }),
                new CognitiveMemoryPageRequest(take: 5),
                Filter: new CognitiveMemoryProjectionFilter(maximumAccessLevel: CognitiveMemoryAccessLevel.Project))));
    }

    [Fact]
    public async Task RagProjectionAdapter_SearchBuildsTypedFilterAndRequiresCanonicalMetadata()
    {
        var rag = new FakeRagDriver(RagDriverCapabilities.WithTagsAndProjectionControls);
        var adapter = new RagCognitiveMemoryProjectionAdapter(rag);
        var validEntry = ProjectionEntry();
        rag.SearchResults = [new RagSearchResult
        {
            Knowledge = new RagKnowledgeEntry
            {
                Id = validEntry.PointId.Value,
                Text = validEntry.ProjectionText,
                Metadata = new Dictionary<string, object?>
                {
                    ["memoryRecordId"] = validEntry.MemoryRecordId.Value.ToString("D"),
                    ["payloadHash"] = validEntry.PayloadHash.Value
                }
            },
            Score = 0.82
        }];

        var result = await adapter.SearchAsync(new CognitiveMemoryProjectionSearchRequest(
            CollectionName(),
            new CognitiveMemoryProjectionProfileId("cm-projection-v1"),
            "deployment evidence",
            new CognitiveMemoryVector(new[] { 1f, 0f, 0f }),
            new CognitiveMemoryPageRequest(take: 5),
            Filter: new CognitiveMemoryProjectionFilter(
                projectId: validEntry.ProjectId,
                memoryKinds: [CognitiveMemoryRecordKind.Semantic],
                projectionKinds: [CognitiveMemoryProjectionKind.VectorCollection],
                validationStates: [CognitiveMemoryValidationState.Approved],
                maximumAccessLevel: CognitiveMemoryAccessLevel.Project,
                sourceSystem: "workbench")));

        Assert.Single(result.Hits);
        var search = Assert.Single(rag.SearchRequests);
        var filterGroup = Assert.IsType<RagFilterGroup>(search.Filter);
        var accessFilter = filterGroup.Filters.OfType<RagFilterCondition>()
            .Single(condition => condition.FieldName == "accessLevel");
        Assert.Equal(RagFilterOperator.In, accessFilter.Operator);
        Assert.Equal(["Public", "Project"], accessFilter.Values.Select(value => value.StringValue ?? string.Empty).ToArray());

        rag.SearchResults = [new RagSearchResult
        {
            Knowledge = new RagKnowledgeEntry
            {
                Id = "corrupt-result",
                Text = "missing canonical metadata"
            },
            Score = 0.4
        }];

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await adapter.SearchAsync(new CognitiveMemoryProjectionSearchRequest(
                CollectionName(),
                new CognitiveMemoryProjectionProfileId("cm-projection-v1"),
                "deployment evidence",
                null,
                new CognitiveMemoryPageRequest(take: 5))));
    }

    private static CognitiveMemoryProjectionEntry ProjectionEntry(
        CognitiveMemoryVector? vector = null,
        IReadOnlyList<CognitiveMemoryEvidenceAnchorId>? evidenceAnchorIds = null)
    {
        var memoryRecordId = CognitiveMemoryRecordId.New();
        return new CognitiveMemoryProjectionEntry(
            new CognitiveMemoryProjectionPointId("cm-projection-point-1"),
            Guid.NewGuid(),
            memoryRecordId,
            CognitiveMemoryRecordKind.Semantic,
            CognitiveMemoryProjectionKind.VectorCollection,
            new CognitiveMemoryProjectionProfileId("cm-projection-v1"),
            new CognitiveMemoryEmbeddingProfileId("cm-embedding-v1"),
            "Docker production deployment evidence.",
            vector ?? new CognitiveMemoryVector(new[] { 0.25f, 0.5f, 0.75f }),
            new CognitiveMemoryClaimProjectionPayload(
                new CognitiveMemoryPayloadSchemaVersion("projection-v1"),
                CognitiveMemoryProjectionPayloadSchemaKind.ClaimContainer,
                memoryRecordId,
                [CognitiveMemoryClaimId.New()],
                [CognitiveMemoryContextFrameId.New()],
                [CognitiveMemoryEntityId.New()],
                [CognitiveMemoryBeliefStateKind.Supported],
                [CognitiveMemoryContextBoundaryPolicy.RelatedNotSubstitutable],
                CognitiveMemoryScoreProjectionBucket.StrongAccept),
            CognitiveMemoryHash.FromUtf8("source-item-1"),
            CognitiveMemoryHash.FromUtf8("projection-payload-1"),
            CognitiveMemoryAccessLevel.Project,
            CognitiveMemoryRedactionState.Safe,
            CognitiveMemoryValidationState.Approved,
            "workbench",
            "node-1",
            DateTimeOffset.UnixEpoch,
            evidenceAnchorIds ?? [CognitiveMemoryEvidenceAnchorId.New()],
            new Dictionary<string, string>
            {
                ["customMetadata"] = "custom-value"
            },
            ["claim", "project"]);
    }

    private static CognitiveMemoryProjectionCollectionName CollectionName()
        => new("cognitive-memory-test");

    private static CognitiveMemoryProcessingBudget Budget()
        => new(maxItemCount: 1, maxByteCount: 1024, timeout: TimeSpan.FromSeconds(5));

    private enum RecallIntent
    {
        ProjectEvidence = 0
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
    }

    private sealed class FakeEmbeddingGenerator(
        string profileId,
        float[]? vector = null) : IAgentTextEmbeddingGenerator
    {
        public ValueTask<AgentTextEmbedding> GenerateAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var values = vector ?? [0.1f, 0.2f, 0.3f];
            return ValueTask.FromResult(new AgentTextEmbedding(
                text,
                values,
                new AgentTextEmbeddingProfile
                {
                    ProviderName = "fake-semantic",
                    ModelId = "fake-model",
                    ProfileId = profileId,
                    Dimension = values.Length,
                    IsNormalized = true
                }));
        }
    }

    private sealed class FakeSemanticClassifier<TLabel>(
        SemanticClassificationResult<TLabel> result) : ISemanticClassifier<TLabel>
        where TLabel : struct, Enum
    {
        public SemanticClassificationRequest? LastRequest { get; private set; }

        public ValueTask<SemanticClassificationResult<TLabel>> ClassifyAsync(
            SemanticClassificationRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequest = request with
            {
                Metadata = new Dictionary<string, string>
                {
                    ["intentScope"] = "project-evidence"
                }
            };
            return ValueTask.FromResult(result);
        }
    }

    private sealed class FakeRagDriver(RagDriverCapabilities capabilities) : IRagDriver
    {
        public string ProviderName => "fake-rag";

        public RagDriverCapabilities Capabilities { get; } = capabilities;

        public RagCollectionOptions DefaultCollection { get; } = new();

        public List<RagPayloadIndexRequest> PayloadIndexRequests { get; } = [];

        public List<RagUpsertRequest> UpsertRequests { get; } = [];

        public List<RagDeleteByFilterRequest> DeleteByFilterRequests { get; } = [];

        public List<RagSearchRequest> SearchRequests { get; } = [];

        public IReadOnlyList<RagSearchResult> SearchResults { get; set; } = [];

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
            DeleteByFilterRequests.Add(request);
            return ValueTask.CompletedTask;
        }

        public ValueTask<RagPayloadIndexResult> EnsurePayloadIndexAsync(
            RagPayloadIndexRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            request.Validate();
            PayloadIndexRequests.Add(request);
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
            SearchRequests.Add(request);
            return ValueTask.FromResult(SearchResults);
        }
    }
}

using System.Text;
using CanDoItAll.AgentFramework.Rag.Driver.Abstractions;
using CanDoItAll.AgentFramework.Rag.Driver.Models;
using CanDoItAll.AgentFramework.SemanticCompletion.Driver.Embeddings;
using CanDoItAll.AgentFramework.SemanticCompletion.Driver.Semantics;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class SemanticCompletionCognitiveMemoryEmbeddingProvider(
    IAgentTextEmbeddingGenerator embeddingGenerator,
    IClock clock) : ICognitiveMemoryEmbeddingProvider
{
    public async ValueTask<CognitiveMemoryEmbeddingResult> EmbedAsync(
        CognitiveMemoryEmbeddingRequest request,
        CancellationToken cancellationToken = default)
    {
        EnforceBudget(request.Budget, request.Input, clock, cancellationToken);

        var embedding = await embeddingGenerator.GenerateAsync(request.Input, cancellationToken);
        var actualProfileId = new CognitiveMemoryEmbeddingProfileId(embedding.Profile.ProfileId);
        if (actualProfileId != request.EmbeddingProfileId)
        {
            throw new InvalidOperationException(
                $"Semantic embedding profile mismatch. Requested={request.EmbeddingProfileId} Actual={actualProfileId} Provider={embedding.Profile.ProviderName} Model={embedding.Profile.ModelId}.");
        }

        return new CognitiveMemoryEmbeddingResult(
            request.EmbeddingProfileId,
            CognitiveMemoryHash.FromUtf8($"{request.EmbeddingProfileId}:{request.Input}"),
            new CognitiveMemoryVector(embedding.Vector),
            $"semantic-completion:{embedding.Profile.ProviderName}:{embedding.Profile.ModelId}:{embedding.Dimension}");
    }

    private static void EnforceBudget(
        CognitiveMemoryProcessingBudget budget,
        string text,
        IClock clock,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var tracker = new CognitiveMemoryBudgetTracker(budget, clock.GetUtcNow());
        var decision = tracker.TryAccept(Encoding.UTF8.GetByteCount(text), clock.GetUtcNow(), cancellationToken);
        if (!decision.Accepted)
        {
            throw new InvalidOperationException($"SemanticCompletion embedding request exceeded the {decision.Limit} budget.");
        }
    }
}

public sealed class SemanticCompletionCognitiveMemoryRanker(
    ISemanticTextRanker ranker,
    IClock clock) : ICognitiveMemorySemanticRanker
{
    public async ValueTask<CognitiveMemorySemanticRankResult> RankAsync(
        CognitiveMemorySemanticRankRequest request,
        CancellationToken cancellationToken = default)
    {
        EnforceBudget(request.Budget, request.Text, clock, cancellationToken);
        var matches = await ranker.RankAsync(request.Text, request.Page.Take, cancellationToken);
        return new CognitiveMemorySemanticRankResult(
            matches.Select(match => new CognitiveMemorySemanticTextMatch(
                    match.Key,
                    match.Text,
                    match.Score,
                    match.Metadata ?? new Dictionary<string, string>(StringComparer.Ordinal)))
                .ToList(),
            $"semantic-completion-ranker:{matches.Count}");
    }

    private static void EnforceBudget(
        CognitiveMemoryProcessingBudget budget,
        string text,
        IClock clock,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var tracker = new CognitiveMemoryBudgetTracker(budget, clock.GetUtcNow());
        var decision = tracker.TryAccept(Encoding.UTF8.GetByteCount(text), clock.GetUtcNow(), cancellationToken);
        if (!decision.Accepted)
        {
            throw new InvalidOperationException($"SemanticCompletion rank request exceeded the {decision.Limit} budget.");
        }
    }
}

public sealed class SemanticCompletionCognitiveMemoryClassifier<TLabel>(
    ISemanticClassifier<TLabel> classifier,
    IClock clock) : ICognitiveMemorySemanticClassifier<TLabel>
    where TLabel : struct, Enum
{
    public async ValueTask<CognitiveMemorySemanticClassificationResult<TLabel>> ClassifyAsync(
        CognitiveMemorySemanticClassificationRequest request,
        CancellationToken cancellationToken = default)
    {
        EnforceBudget(request.Budget, request.Text, clock, cancellationToken);
        var result = await classifier.ClassifyAsync(
            new SemanticClassificationRequest(request.Text, request.Metadata),
            cancellationToken);

        return new CognitiveMemorySemanticClassificationResult<TLabel>(
            result.Label,
            MapDecision(result.Decision),
            result.Score,
            result.Margin,
            result.MatchedIntentKey,
            result.MatchedPhrase,
            result.Matches
                .Select(match => new CognitiveMemorySemanticClassificationMatch<TLabel>(
                    match.Label,
                    match.IntentKey,
                    match.Phrase,
                    match.Score))
                .ToList(),
            result.GuardHits,
            $"semantic-completion-classifier:{typeof(TLabel).Name}:{result.Decision}");
    }

    private static CognitiveMemorySemanticClassificationDecision MapDecision(SemanticClassificationDecision decision)
        => decision switch
        {
            SemanticClassificationDecision.Rejected => CognitiveMemorySemanticClassificationDecision.Rejected,
            SemanticClassificationDecision.WeakMatch => CognitiveMemorySemanticClassificationDecision.WeakMatch,
            SemanticClassificationDecision.Accepted => CognitiveMemorySemanticClassificationDecision.Accepted,
            _ => throw new ArgumentOutOfRangeException(nameof(decision), decision, "Unsupported semantic classification decision.")
        };

    private static void EnforceBudget(
        CognitiveMemoryProcessingBudget budget,
        string text,
        IClock clock,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var tracker = new CognitiveMemoryBudgetTracker(budget, clock.GetUtcNow());
        var decision = tracker.TryAccept(Encoding.UTF8.GetByteCount(text), clock.GetUtcNow(), cancellationToken);
        if (!decision.Accepted)
        {
            throw new InvalidOperationException($"SemanticCompletion classification request exceeded the {decision.Limit} budget.");
        }
    }
}

public sealed class RagCognitiveMemoryProjectionAdapter(IRagDriver ragDriver) : ICognitiveMemoryProjectionAdapter
{
    public CognitiveMemoryProjectionAdapterCapabilities Capabilities => new(
        ragDriver.ProviderName,
        ragDriver.Capabilities.SupportsFilters,
        ragDriver.Capabilities.SupportsPayloadIndexes,
        ragDriver.Capabilities.SupportsDeleteByFilter,
        ragDriver.Capabilities.SupportsNamedVectors);

    public async ValueTask EnsureCollectionAsync(
        CognitiveMemoryProjectionCollectionRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateCollectionRequest(request);
        await ragDriver.EnsureCollectionAsync(
            new RagCollectionOptions
            {
                CollectionName = request.CollectionName.Value,
                VectorSize = request.VectorDimensions
            },
            cancellationToken);
    }

    public async ValueTask<IReadOnlyList<CognitiveMemoryProjectionPayloadIndexResult>> EnsurePayloadIndexesAsync(
        CognitiveMemoryProjectionPayloadIndexRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!ragDriver.Capabilities.SupportsPayloadIndexes)
        {
            throw new NotSupportedException($"RAG provider '{ragDriver.ProviderName}' does not support payload indexes.");
        }

        if (request.Indexes.Count == 0)
        {
            throw new ArgumentException("At least one projection payload index is required.", nameof(request));
        }

        var results = new List<CognitiveMemoryProjectionPayloadIndexResult>(request.Indexes.Count);
        foreach (var index in request.Indexes)
        {
            var result = await ragDriver.EnsurePayloadIndexAsync(
                new RagPayloadIndexRequest
                {
                    CollectionName = request.CollectionName.Value,
                    FieldName = CognitiveMemoryProjectionPayloadFieldNames.Resolve(index.Field),
                    IndexKind = MapIndexKind(index.IndexKind)
                },
                cancellationToken);

            results.Add(new CognitiveMemoryProjectionPayloadIndexResult(
                index.Field,
                index.IndexKind,
                MapIndexStatus(result.Status)));
        }

        return results;
    }

    public async ValueTask ProjectAsync(
        CognitiveMemoryProjectionWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Entries.Count == 0)
        {
            throw new ArgumentException("At least one projection entry is required.", nameof(request));
        }

        var entries = request.Entries.Select(entry => BuildRagEntry(entry, request.ExpectedVectorDimensions)).ToList();
        await ragDriver.UpsertAsync(
            new RagUpsertRequest
            {
                CollectionName = request.CollectionName.Value,
                Entries = entries
            },
            cancellationToken);
    }

    public async ValueTask<CognitiveMemoryProjectionSearchResult> SearchAsync(
        CognitiveMemoryProjectionSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.QueryText);
        if (request.Filter is { IsEmpty: false } && !ragDriver.Capabilities.SupportsFilters)
        {
            throw new NotSupportedException($"RAG provider '{ragDriver.ProviderName}' does not support typed payload filters.");
        }

        var results = await ragDriver.SearchAsync(
            new RagSearchRequest
            {
                CollectionName = request.CollectionName.Value,
                QueryText = request.QueryText,
                Vector = request.QueryVector?.ToArrayForAdapterBoundary(),
                Limit = request.Page.Take,
                MinScore = request.MinScore,
                Filter = request.Filter is null || request.Filter.IsEmpty ? null : BuildFilter(request.Filter)
            },
            cancellationToken);

        return new CognitiveMemoryProjectionSearchResult(
            request.ProjectionProfileId,
            results.Select(BuildSearchHit).ToList(),
            $"rag:{ragDriver.ProviderName}:search:{results.Count}");
    }

    public async ValueTask<CognitiveMemoryProjectionDeleteResult> DeleteBySourceAsync(
        CognitiveMemoryProjectionDeleteBySourceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!ragDriver.Capabilities.SupportsDeleteByFilter)
        {
            throw new NotSupportedException($"RAG provider '{ragDriver.ProviderName}' does not support delete-by-filter cleanup.");
        }

        var filter = BuildDeleteBySourceFilter(request);
        await ragDriver.DeleteByFilterAsync(
            new RagDeleteByFilterRequest
            {
                CollectionName = request.CollectionName.Value,
                Filter = filter
            },
            cancellationToken);

        return new CognitiveMemoryProjectionDeleteResult($"rag:{ragDriver.ProviderName}:delete-by-source");
    }

    private static void ValidateCollectionRequest(CognitiveMemoryProjectionCollectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.VectorDimensions <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.VectorDimensions, "Projection vector dimensions must be positive.");
        }
    }

    private static RagKnowledgeEntry BuildRagEntry(
        CognitiveMemoryProjectionEntry entry,
        int? expectedVectorDimensions)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.ProjectionText);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.SourceSystem);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.SourceItemKey);
        CognitiveMemoryProjectionPayloadValidator.Validate(entry.ClaimPayload).ThrowIfInvalid();
        if (entry.ClaimPayload.MemoryRecordId != entry.MemoryRecordId)
        {
            throw new InvalidOperationException("Projection entry memory record id must match the typed claim projection payload.");
        }

        if (entry.EvidenceAnchorIds is null || entry.EvidenceAnchorIds.Count == 0)
        {
            throw new InvalidOperationException("Projection entry requires evidence anchor ids before it can be sent to vector projection.");
        }

        if (expectedVectorDimensions is { } expected && entry.Vector.Length != expected)
        {
            throw new ArgumentException($"Projection vector length {entry.Vector.Length} does not match expected dimensions {expected}.", nameof(entry));
        }

        return new RagKnowledgeEntry
        {
            Id = entry.PointId.Value,
            Text = entry.ProjectionText,
            Vector = entry.Vector.ToArrayForAdapterBoundary(),
            Tags = entry.Tags ?? [],
            Metadata = BuildMetadata(entry)
        };
    }

    private static Dictionary<string, object?> BuildMetadata(CognitiveMemoryProjectionEntry entry)
    {
        var payload = entry.ClaimPayload;
        var metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [CognitiveMemoryProjectionPayloadFieldNames.SchemaVersion] = payload.SchemaVersion.Value,
            [CognitiveMemoryProjectionPayloadFieldNames.SchemaKind] = payload.SchemaKind.ToString(),
            [CognitiveMemoryProjectionPayloadFieldNames.ProjectId] = entry.ProjectId?.ToString("D"),
            [CognitiveMemoryProjectionPayloadFieldNames.MemoryRecordId] = entry.MemoryRecordId.Value.ToString("D"),
            [CognitiveMemoryProjectionPayloadFieldNames.MemoryKind] = entry.MemoryKind.ToString(),
            [CognitiveMemoryProjectionPayloadFieldNames.ProjectionKind] = entry.ProjectionKind.ToString(),
            [CognitiveMemoryProjectionPayloadFieldNames.SourceSystem] = entry.SourceSystem,
            [CognitiveMemoryProjectionPayloadFieldNames.SourceItemKey] = entry.SourceItemKey,
            [CognitiveMemoryProjectionPayloadFieldNames.SourceHash] = entry.SourceHash.Value,
            [CognitiveMemoryProjectionPayloadFieldNames.PayloadHash] = entry.PayloadHash.Value,
            [CognitiveMemoryProjectionPayloadFieldNames.AccessLevel] = entry.AccessLevel.ToString(),
            [CognitiveMemoryProjectionPayloadFieldNames.RedactionState] = entry.RedactionState.ToString(),
            [CognitiveMemoryProjectionPayloadFieldNames.ValidationState] = entry.ValidationState.ToString(),
            [CognitiveMemoryProjectionPayloadFieldNames.ClaimId] = payload.ClaimIds.Select(id => id.Value.ToString("D")).ToArray(),
            [CognitiveMemoryProjectionPayloadFieldNames.ContextFrameId] = payload.ContextFrameIds.Select(id => id.Value.ToString("D")).ToArray(),
            [CognitiveMemoryProjectionPayloadFieldNames.EvidenceAnchorId] = entry.EvidenceAnchorIds?.Select(id => id.Value.ToString("D")).ToArray() ?? [],
            [CognitiveMemoryProjectionPayloadFieldNames.EntityId] = payload.EntityIds.Select(id => id.Value.ToString("D")).ToArray(),
            [CognitiveMemoryProjectionPayloadFieldNames.BeliefState] = payload.BeliefStates.Select(state => state.ToString()).ToArray(),
            [CognitiveMemoryProjectionPayloadFieldNames.EmbeddingProfileId] = entry.EmbeddingProfileId.Value,
            [CognitiveMemoryProjectionPayloadFieldNames.ProjectionProfileId] = entry.ProjectionProfileId.Value,
            [CognitiveMemoryProjectionPayloadFieldNames.UpdatedAtUtc] = entry.UpdatedAtUtc
        };

        if (entry.Metadata is null)
        {
            return metadata;
        }

        foreach (var (key, value) in entry.Metadata)
        {
            if (!metadata.ContainsKey(key))
            {
                metadata[key] = value;
            }
        }

        return metadata;
    }

    private static CognitiveMemoryProjectionSearchHit BuildSearchHit(RagSearchResult result)
    {
        var metadata = result.Knowledge.Metadata;
        var memoryRecordId = ReadGuid(metadata, CognitiveMemoryProjectionPayloadFieldNames.MemoryRecordId);
        var payloadHash = ReadHash(metadata, CognitiveMemoryProjectionPayloadFieldNames.PayloadHash);
        return new CognitiveMemoryProjectionSearchHit(
            new CognitiveMemoryProjectionPointId(result.Knowledge.Id),
            new CognitiveMemoryRecordId(memoryRecordId),
            payloadHash,
            result.Score,
            metadata);
    }

    private static Guid ReadGuid(
        IReadOnlyDictionary<string, object?> metadata,
        string fieldName)
    {
        if (!metadata.TryGetValue(fieldName, out var value) ||
            value is not string text ||
            !Guid.TryParseExact(text, "D", out var parsed))
        {
            throw new InvalidOperationException($"Projection search result is missing required GUID payload field '{fieldName}'.");
        }

        return parsed;
    }

    private static CognitiveMemoryHash ReadHash(
        IReadOnlyDictionary<string, object?> metadata,
        string fieldName)
    {
        if (!metadata.TryGetValue(fieldName, out var value) ||
            value is not string text)
        {
            throw new InvalidOperationException($"Projection search result is missing required hash payload field '{fieldName}'.");
        }

        return new CognitiveMemoryHash(CognitiveMemoryHashAlgorithm.Sha256, text);
    }

    private static RagFilter BuildFilter(CognitiveMemoryProjectionFilter filter)
    {
        var filters = new List<RagFilter>();
        if (filter.ProjectId is Guid projectId)
        {
            filters.Add(RagFilterCondition.Equal(
                CognitiveMemoryProjectionPayloadFieldNames.ProjectId,
                projectId.ToString("D")));
        }

        if (filter.MemoryKinds.Count > 0)
        {
            filters.Add(RagFilterCondition.In(
                CognitiveMemoryProjectionPayloadFieldNames.MemoryKind,
                filter.MemoryKinds.Select(kind => RagFilterValue.FromString(kind.ToString())).ToArray()));
        }

        if (filter.ProjectionKinds.Count > 0)
        {
            filters.Add(RagFilterCondition.In(
                CognitiveMemoryProjectionPayloadFieldNames.ProjectionKind,
                filter.ProjectionKinds.Select(kind => RagFilterValue.FromString(kind.ToString())).ToArray()));
        }

        if (filter.ValidationStates.Count > 0)
        {
            filters.Add(RagFilterCondition.In(
                CognitiveMemoryProjectionPayloadFieldNames.ValidationState,
                filter.ValidationStates.Select(state => RagFilterValue.FromString(state.ToString())).ToArray()));
        }

        if (filter.MaximumAccessLevel is CognitiveMemoryAccessLevel accessLevel)
        {
            filters.Add(RagFilterCondition.In(
                CognitiveMemoryProjectionPayloadFieldNames.AccessLevel,
                GetAllowedAccessLevels(accessLevel)
                    .Select(level => RagFilterValue.FromString(level.ToString()))
                    .ToArray()));
        }

        if (!string.IsNullOrWhiteSpace(filter.SourceSystem))
        {
            filters.Add(RagFilterCondition.Equal(CognitiveMemoryProjectionPayloadFieldNames.SourceSystem, filter.SourceSystem));
        }

        if (!string.IsNullOrWhiteSpace(filter.SourceItemKey))
        {
            filters.Add(RagFilterCondition.Equal(CognitiveMemoryProjectionPayloadFieldNames.SourceItemKey, filter.SourceItemKey));
        }

        if (filter.SourceHash is CognitiveMemoryHash sourceHash)
        {
            filters.Add(RagFilterCondition.Equal(CognitiveMemoryProjectionPayloadFieldNames.SourceHash, sourceHash.Value));
        }

        if (filter.PayloadHash is CognitiveMemoryHash payloadHash)
        {
            filters.Add(RagFilterCondition.Equal(CognitiveMemoryProjectionPayloadFieldNames.PayloadHash, payloadHash.Value));
        }

        return filters.Count == 1 ? filters[0] : RagFilterGroup.All(filters.ToArray());
    }

    private static RagFilter BuildDeleteBySourceFilter(CognitiveMemoryProjectionDeleteBySourceRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourceSystem);
        var sourceItemKeys = request.SourceItemKeys?.Where(key => !string.IsNullOrWhiteSpace(key)).Distinct(StringComparer.Ordinal).ToArray() ?? [];
        var sourceHashes = request.SourceHashes?.Distinct().ToArray() ?? [];
        if (sourceItemKeys.Length == 0 && sourceHashes.Length == 0)
        {
            throw new ArgumentException("Delete-by-source requires at least one source item key or source hash.", nameof(request));
        }

        var filters = new List<RagFilter>
        {
            RagFilterCondition.Equal(CognitiveMemoryProjectionPayloadFieldNames.SourceSystem, request.SourceSystem)
        };
        if (request.ProjectId is Guid projectId)
        {
            filters.Add(RagFilterCondition.Equal(CognitiveMemoryProjectionPayloadFieldNames.ProjectId, projectId.ToString("D")));
        }

        var selectors = new List<RagFilter>();
        if (sourceItemKeys.Length > 0)
        {
            selectors.Add(RagFilterCondition.In(
                CognitiveMemoryProjectionPayloadFieldNames.SourceItemKey,
                sourceItemKeys.Select(RagFilterValue.FromString).ToArray()));
        }

        if (sourceHashes.Length > 0)
        {
            selectors.Add(RagFilterCondition.In(
                CognitiveMemoryProjectionPayloadFieldNames.SourceHash,
                sourceHashes.Select(hash => RagFilterValue.FromString(hash.Value)).ToArray()));
        }

        filters.Add(selectors.Count == 1 ? selectors[0] : RagFilterGroup.Any(selectors.ToArray()));
        return RagFilterGroup.All(filters.ToArray());
    }

    private static RagPayloadIndexKind MapIndexKind(CognitiveMemoryProjectionPayloadIndexKind indexKind)
        => indexKind switch
        {
            CognitiveMemoryProjectionPayloadIndexKind.Keyword => RagPayloadIndexKind.Keyword,
            CognitiveMemoryProjectionPayloadIndexKind.Integer => RagPayloadIndexKind.Integer,
            CognitiveMemoryProjectionPayloadIndexKind.Float => RagPayloadIndexKind.Float,
            CognitiveMemoryProjectionPayloadIndexKind.Boolean => RagPayloadIndexKind.Boolean,
            CognitiveMemoryProjectionPayloadIndexKind.DateTime => RagPayloadIndexKind.DateTime,
            CognitiveMemoryProjectionPayloadIndexKind.Text => RagPayloadIndexKind.Text,
            CognitiveMemoryProjectionPayloadIndexKind.Uuid => RagPayloadIndexKind.Uuid,
            _ => throw new ArgumentOutOfRangeException(nameof(indexKind), indexKind, "Unsupported cognitive memory projection payload index kind.")
        };

    private static CognitiveMemoryProjectionPayloadIndexStatus MapIndexStatus(RagPayloadIndexStatus status)
        => status switch
        {
            RagPayloadIndexStatus.Ensured => CognitiveMemoryProjectionPayloadIndexStatus.Ensured,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported RAG payload index status.")
        };

    private static IReadOnlyList<CognitiveMemoryAccessLevel> GetAllowedAccessLevels(CognitiveMemoryAccessLevel maximumAccessLevel)
        => maximumAccessLevel switch
        {
            CognitiveMemoryAccessLevel.Public => [CognitiveMemoryAccessLevel.Public],
            CognitiveMemoryAccessLevel.Project => [CognitiveMemoryAccessLevel.Public, CognitiveMemoryAccessLevel.Project],
            CognitiveMemoryAccessLevel.Restricted => [CognitiveMemoryAccessLevel.Public, CognitiveMemoryAccessLevel.Project, CognitiveMemoryAccessLevel.Restricted],
            _ => throw new ArgumentOutOfRangeException(nameof(maximumAccessLevel), maximumAccessLevel, "Unsupported cognitive memory access level.")
        };
}

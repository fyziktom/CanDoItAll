using System.Text;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemoryTaxonomyValidator(
    ICognitiveMemoryRecordValidator recordValidator) : ICognitiveMemoryTaxonomyValidator
{
    public Result ValidateMemoryRecord(
        CognitiveMemoryRecord record,
        IReadOnlyList<CognitiveMemorySourceLinkRecord> sourceLinks,
        IReadOnlyList<CognitiveMemoryEvidenceAnchorId> evidenceAnchorIds)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(sourceLinks);
        ArgumentNullException.ThrowIfNull(evidenceAnchorIds);

        var errors = recordValidator.ValidateForPersistence(record).Errors.ToList();
        if (sourceLinks.Count == 0)
        {
            errors.Add(Error.Validation(
                "Canonical cognitive memory records require at least one source link.",
                "cognitive-memory-source-link-required"));
        }

        if (evidenceAnchorIds.Count == 0)
        {
            errors.Add(Error.Validation(
                "Canonical cognitive memory records require at least one evidence anchor.",
                "cognitive-memory-evidence-anchor-required"));
        }

        if (sourceLinks.Any(link => link.MemoryRecordId != record.Id))
        {
            errors.Add(Error.Validation(
                "Every source link must reference the canonical memory record being validated.",
                "cognitive-memory-source-link-record-mismatch"));
        }

        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(errors);
    }

    public Result ValidateRelationDraft(CognitiveMemoryRelationDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var errors = new List<Error>();
        if (draft.SourceMemoryRecordId == draft.TargetMemoryRecordId)
        {
            errors.Add(Error.Validation(
                "Cognitive memory relations cannot point a record to itself.",
                "cognitive-memory-relation-self-reference"));
        }

        if (draft.EvidenceAnchorIds.Count == 0)
        {
            errors.Add(Error.Validation(
                "Cognitive memory relations require source evidence anchors.",
                "cognitive-memory-relation-evidence-required"));
        }

        if (draft.RelationKind == CognitiveMemoryRelationKind.SameAs &&
            draft.ContextBoundaryPolicies.Contains(CognitiveMemoryContextBoundaryPolicy.RelatedNotSubstitutable))
        {
            errors.Add(Error.Validation(
                "Context-separated records must not be collapsed into SameAs relations.",
                "cognitive-memory-relation-context-separated-same-as"));
        }

        if (string.IsNullOrWhiteSpace(draft.AlgorithmVersion.Value))
        {
            errors.Add(Error.Validation(
                "Cognitive memory relations require an algorithm version.",
                "cognitive-memory-relation-algorithm-version-required"));
        }

        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(errors);
    }
}

public sealed class CognitiveMemoryProjectionLifecycleService(
    ICognitiveMemoryEmbeddingProvider embeddingProvider,
    ICognitiveMemoryProjectionAdapter projectionAdapter,
    ICognitiveMemoryTaxonomyValidator taxonomyValidator,
    IClock clock,
    ILogger<CognitiveMemoryProjectionLifecycleService> logger) : ICognitiveMemoryProjectionLifecycleService
{
    public CognitiveMemoryProjectionLifecycleDecision EvaluateLifecycle(CognitiveMemoryProjectionLifecycleEvaluationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.SourceTombstoned)
        {
            return new CognitiveMemoryProjectionLifecycleDecision(
                CognitiveMemoryProjectionLifecycleDecisionKind.Delete,
                CognitiveMemoryProjectionStaleReason.SourceTombstoned,
                "Source tombstone requires deleting the projection point.");
        }

        if (request.ExistingProjection is null)
        {
            return new CognitiveMemoryProjectionLifecycleDecision(
                CognitiveMemoryProjectionLifecycleDecisionKind.Project,
                CognitiveMemoryProjectionStaleReason.MissingProjection,
                "Projection record does not exist.");
        }

        if (request.ExistingProjection.Status == CognitiveMemoryProjectionStatus.Failed)
        {
            return new CognitiveMemoryProjectionLifecycleDecision(
                CognitiveMemoryProjectionLifecycleDecisionKind.Rebuild,
                CognitiveMemoryProjectionStaleReason.PreviousFailure,
                "Previous projection attempt failed.");
        }

        if (request.ExistingProjection.SourceHash != request.CurrentSourceHash.Value)
        {
            return Rebuild(CognitiveMemoryProjectionStaleReason.SourceHashChanged, "Source hash changed.");
        }

        if (request.ExistingProjection.PayloadHash != request.CurrentPayloadHash.Value)
        {
            return Rebuild(CognitiveMemoryProjectionStaleReason.PayloadHashChanged, "Projection payload hash changed.");
        }

        if (!string.Equals(request.ExistingProjection.ProjectionProfileId, request.ProjectionProfileId.Value, StringComparison.Ordinal))
        {
            return Rebuild(CognitiveMemoryProjectionStaleReason.ProjectionProfileChanged, "Projection profile changed.");
        }

        if (!string.Equals(request.ExistingProjection.EmbeddingProfileId, request.EmbeddingProfileId.Value, StringComparison.Ordinal))
        {
            return Rebuild(CognitiveMemoryProjectionStaleReason.EmbeddingProfileChanged, "Embedding profile changed.");
        }

        if (!string.Equals(request.ExistingProjection.ProjectionSchemaVersion, request.ProjectionSchemaVersion.Value, StringComparison.Ordinal))
        {
            return Rebuild(CognitiveMemoryProjectionStaleReason.ProjectionSchemaChanged, "Projection schema version changed.");
        }

        if (!string.Equals(request.ExistingProjection.AlgorithmVersion, request.AlgorithmVersion.Value, StringComparison.Ordinal))
        {
            return Rebuild(CognitiveMemoryProjectionStaleReason.AlgorithmVersionChanged, "Projection algorithm version changed.");
        }

        return new CognitiveMemoryProjectionLifecycleDecision(
            CognitiveMemoryProjectionLifecycleDecisionKind.NoChange,
            CognitiveMemoryProjectionStaleReason.None,
            "Existing projection is current.");

        static CognitiveMemoryProjectionLifecycleDecision Rebuild(
            CognitiveMemoryProjectionStaleReason reason,
            string message)
            => new(CognitiveMemoryProjectionLifecycleDecisionKind.Rebuild, reason, message);
    }

    public async ValueTask<CognitiveMemoryProjectionLifecycleResult> ProjectAsync(
        CognitiveMemoryProjectionLifecycleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var validation = taxonomyValidator.ValidateMemoryRecord(request.MemoryRecord, request.SourceLinks, request.EvidenceAnchorIds);
        if (validation.IsFailure)
        {
            throw new InvalidOperationException($"Cognitive memory projection request is invalid: {string.Join(", ", validation.Errors.Select(error => error.Code))}.");
        }

        var requestedProviderName = CognitiveMemoryGuard.EnsureText(request.TargetProviderName, nameof(request.TargetProviderName));
        var sourceSystem = CognitiveMemoryGuard.EnsureText(request.SourceSystem, nameof(request.SourceSystem));
        var sourceItemKey = CognitiveMemoryGuard.EnsureText(request.SourceItemKey, nameof(request.SourceItemKey));
        if (!string.Equals(requestedProviderName, projectionAdapter.Capabilities.ProviderName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Projection target provider mismatch. Requested={requestedProviderName} Actual={projectionAdapter.Capabilities.ProviderName}.");
        }

        var nowUtc = clock.GetUtcNow();
        var projectionText = BuildProjectionText(request.MemoryRecord);
        var sourceHash = ComputeSourceHash(request.MemoryRecord, request.SourceLinks, request.EvidenceAnchorIds);
        var payloadHash = ComputePayloadHash(request, sourceHash);
        var pointId = BuildPointId(request.MemoryRecord.Id, request.ProjectionKind, request.ProjectionProfileId);
        var embedding = await embeddingProvider.EmbedAsync(new CognitiveMemoryEmbeddingRequest(
            request.EmbeddingProfileId,
            projectionText,
            request.Budget), cancellationToken);

        var projectionRecord = BuildProjectionRecord(
            request,
            pointId,
            sourceHash,
            payloadHash,
            embedding.Vector.Length,
            nowUtc);

        var metadata = BuildProjectionMetadata(request.Metadata, projectionRecord.Id);
        var writeRequest = new CognitiveMemoryProjectionWriteRequest(
            request.CollectionName,
            [
                new CognitiveMemoryProjectionEntry(
                    pointId,
                    request.MemoryRecord.ProjectId,
                    new CognitiveMemoryRecordId(request.MemoryRecord.Id),
                    request.MemoryRecord.Kind,
                    request.ProjectionKind,
                    request.ProjectionProfileId,
                    request.EmbeddingProfileId,
                    projectionText,
                    embedding.Vector,
                    request.ClaimPayload,
                    sourceHash,
                    payloadHash,
                    request.MemoryRecord.AccessLevel,
                    CognitiveMemoryRedactionState.Safe,
                    request.MemoryRecord.ValidationState,
                    sourceSystem,
                    sourceItemKey,
                    nowUtc,
                    request.EvidenceAnchorIds,
                    metadata,
                    request.Tags)
            ],
            request.ExpectedVectorDimensions);

        try
        {
            await projectionAdapter.EnsureCollectionAsync(
                new CognitiveMemoryProjectionCollectionRequest(
                    request.CollectionName,
                    request.ExpectedVectorDimensions ?? embedding.Vector.Length),
                cancellationToken);
            await projectionAdapter.ProjectAsync(writeRequest, cancellationToken);
            projectionRecord.Status = CognitiveMemoryProjectionStatus.Projected;
            projectionRecord.LastProjectedAtUtc = nowUtc;
            projectionRecord.RebuildRequired = false;
            return new CognitiveMemoryProjectionLifecycleResult(
                new CognitiveMemoryProjectionLifecycleDecision(
                    CognitiveMemoryProjectionLifecycleDecisionKind.Project,
                    CognitiveMemoryProjectionStaleReason.MissingProjection,
                    "Projection point was built from durable memory and projected through the adapter."),
                projectionRecord,
                writeRequest,
                $"projection:{projectionAdapter.Capabilities.ProviderName}:projected");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Cognitive memory projection failed for MemoryRecordId={MemoryRecordId} ProjectionProfileId={ProjectionProfileId} Provider={Provider}.",
                request.MemoryRecord.Id,
                request.ProjectionProfileId.Value,
                projectionAdapter.Capabilities.ProviderName);

            projectionRecord.Status = CognitiveMemoryProjectionStatus.Failed;
            projectionRecord.RebuildRequired = true;
            projectionRecord.StaleReason = CognitiveMemoryProjectionStaleReason.PreviousFailure;
            projectionRecord.FailureCode = exception.GetType().Name;
            projectionRecord.FailureMessage = exception.Message;
            return new CognitiveMemoryProjectionLifecycleResult(
                new CognitiveMemoryProjectionLifecycleDecision(
                    CognitiveMemoryProjectionLifecycleDecisionKind.Failed,
                    CognitiveMemoryProjectionStaleReason.PreviousFailure,
                    "Projection adapter failed and durable memory was left unchanged."),
                projectionRecord,
                null,
                $"projection:{projectionAdapter.Capabilities.ProviderName}:failed:{exception.GetType().Name}");
        }
    }

    private static string BuildProjectionText(CognitiveMemoryRecord record)
    {
        var builder = new StringBuilder();
        builder.AppendLine(record.Title.Trim());
        if (!string.IsNullOrWhiteSpace(record.SummaryText))
        {
            builder.AppendLine(record.SummaryText.Trim());
        }

        builder.Append(record.CanonicalText.Trim());
        return builder.ToString();
    }

    private static CognitiveMemoryHash ComputeSourceHash(
        CognitiveMemoryRecord record,
        IReadOnlyList<CognitiveMemorySourceLinkRecord> sourceLinks,
        IReadOnlyList<CognitiveMemoryEvidenceAnchorId> evidenceAnchorIds)
    {
        var builder = new StringBuilder();
        builder.AppendLine(record.ContentHash);
        foreach (var link in sourceLinks.OrderBy(link => link.SourceItemId).ThenBy(link => link.EvidenceRole))
        {
            builder.Append(link.SourceManifestId.ToString("D")).Append('|')
                .Append(link.SourceItemId.ToString("D")).Append('|')
                .Append(link.EvidenceRole).Append('|')
                .Append(link.QuoteHash ?? string.Empty).AppendLine();
        }

        foreach (var evidenceAnchorId in evidenceAnchorIds.OrderBy(id => id.Value))
        {
            builder.Append("anchor|").AppendLine(evidenceAnchorId.Value.ToString("D"));
        }

        return CognitiveMemoryHash.FromUtf8(builder.ToString());
    }

    private static CognitiveMemoryHash ComputePayloadHash(
        CognitiveMemoryProjectionLifecycleRequest request,
        CognitiveMemoryHash sourceHash)
    {
        var payload = request.ClaimPayload;
        var builder = new StringBuilder();
        builder.AppendLine(sourceHash.Value);
        builder.AppendLine(request.MemoryRecord.Id.ToString("D"));
        builder.AppendLine(request.MemoryRecord.Kind.ToString());
        builder.AppendLine(request.ProjectionKind.ToString());
        builder.AppendLine(request.ProjectionProfileId.Value);
        builder.AppendLine(request.EmbeddingProfileId.Value);
        builder.AppendLine(request.ProjectionSchemaVersion.Value);
        builder.AppendLine(request.AlgorithmVersion.Value);
        builder.AppendLine(payload.SchemaVersion.Value);
        builder.AppendLine(payload.SchemaKind.ToString());
        foreach (var claimId in payload.ClaimIds.OrderBy(id => id.Value))
        {
            builder.Append("claim|").AppendLine(claimId.Value.ToString("D"));
        }

        foreach (var contextFrameId in payload.ContextFrameIds.OrderBy(id => id.Value))
        {
            builder.Append("context|").AppendLine(contextFrameId.Value.ToString("D"));
        }

        foreach (var evidenceAnchorId in request.EvidenceAnchorIds.OrderBy(id => id.Value))
        {
            builder.Append("evidence|").AppendLine(evidenceAnchorId.Value.ToString("D"));
        }

        return CognitiveMemoryHash.FromUtf8(builder.ToString());
    }

    private static CognitiveMemoryProjectionPointId BuildPointId(
        Guid memoryRecordId,
        CognitiveMemoryProjectionKind projectionKind,
        CognitiveMemoryProjectionProfileId projectionProfileId)
        => new($"{memoryRecordId:D}:{projectionKind}:{projectionProfileId.Value}");

    private static CognitiveMemoryProjectionRecord BuildProjectionRecord(
        CognitiveMemoryProjectionLifecycleRequest request,
        CognitiveMemoryProjectionPointId pointId,
        CognitiveMemoryHash sourceHash,
        CognitiveMemoryHash payloadHash,
        int vectorDimensions,
        DateTimeOffset nowUtc)
        => new()
        {
            ProjectId = request.MemoryRecord.ProjectId,
            MemoryRecordId = request.MemoryRecord.Id,
            ProjectionStoreKind = request.ProjectionStoreKind,
            ProjectionKind = request.ProjectionKind,
            TargetProviderName = request.TargetProviderName,
            CollectionName = request.CollectionName.Value,
            PointId = pointId.Value,
            ProjectionProfileId = request.ProjectionProfileId.Value,
            EmbeddingProfileId = request.EmbeddingProfileId.Value,
            ProjectionSchemaVersion = request.ProjectionSchemaVersion.Value,
            AlgorithmVersion = request.AlgorithmVersion.Value,
            VectorDimensions = vectorDimensions,
            SourceHashAlgorithm = sourceHash.Algorithm,
            SourceHash = sourceHash.Value,
            PayloadHashAlgorithm = payloadHash.Algorithm,
            PayloadHash = payloadHash.Value,
            Status = CognitiveMemoryProjectionStatus.Pending,
            StaleReason = CognitiveMemoryProjectionStaleReason.None,
            RebuildRequired = false,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            ConcurrencyToken = Guid.NewGuid()
        };

    private static IReadOnlyDictionary<string, string> BuildProjectionMetadata(
        IReadOnlyDictionary<string, string>? requestMetadata,
        Guid projectionRecordId)
    {
        var metadata = requestMetadata is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(requestMetadata, StringComparer.Ordinal);

        if (!metadata.TryGetValue("projectionRecordId", out var currentValue) ||
            string.Equals(currentValue, "missing", StringComparison.OrdinalIgnoreCase))
        {
            metadata["projectionRecordId"] = projectionRecordId.ToString("D");
        }

        return metadata;
    }
}

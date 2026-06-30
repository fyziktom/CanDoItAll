using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemoryProjectionRebuildService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ICognitiveMemoryProjectionLifecycleService projectionLifecycleService,
    IClock clock,
    ILogger<CognitiveMemoryProjectionRebuildService> logger,
    IOptions<CognitiveMemoryProjectionOptions>? projectionOptions = null) : ICognitiveMemoryProjectionRebuildService
{
    private const string AlgorithmVersion = "projection-rebuild-runner-v1";
    private const string DefaultProjectionSchemaVersion = "projection-payload-v1";
    private const string DefaultSourceSystem = "durable-memory";
    private const string DefaultSourceItemKey = "unknown-source-item";
    private const int MaximumTake = 500;
    private readonly CognitiveMemoryProjectionOptions projectionOptions = projectionOptions?.Value ?? new CognitiveMemoryProjectionOptions();

    public async ValueTask<CognitiveMemoryProjectionRebuildResult> RebuildAsync(
        CognitiveMemoryProjectionRebuildRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var take = request.Take is > 0 and <= MaximumTake
            ? request.Take
            : throw new ArgumentOutOfRangeException(nameof(request.Take), $"Take must be between 1 and {MaximumTake}.");
        var actorId = CognitiveMemoryGuard.EnsureText(request.ActorId, nameof(request.ActorId));
        var missingProjectionDefaults = request.ProjectMissingRecords
            ? ResolveProjectionDefaults(request)
            : null;
        var effectiveCollectionName = request.CollectionName ?? missingProjectionDefaults?.CollectionName;

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var nowUtc = clock.GetUtcNow();
        var run = new CognitiveMemoryRunRecord
        {
            ProjectId = request.ProjectId,
            RunKind = CognitiveMemoryRunKind.Projection,
            Status = CognitiveMemoryRunStatus.Running,
            OperationMode = CognitiveMemoryOperationMode.Project,
            IdempotencyKey = BuildRunIdempotencyKey(request, actorId, nowUtc),
            InputHash = BuildRequestHash(request, actorId),
            AlgorithmVersion = AlgorithmVersion,
            Cursor = string.Empty,
            StartedAtUtc = nowUtc,
            ConcurrencyToken = Guid.NewGuid()
        };
        dbContext.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);

        var projections = await BuildProjectionQuery(dbContext, request.ProjectId, effectiveCollectionName)
            .OrderBy(projection => projection.UpdatedAtUtc)
            .ThenBy(projection => projection.Id)
            .Take(take)
            .ToListAsync(cancellationToken);

        var items = new List<CognitiveMemoryProjectionRebuildItemResult>(projections.Count);
        var warnings = new List<string>();
        var projectedCount = 0;
        var failedCount = 0;
        var skippedCount = 0;
        var selectedCount = projections.Count;

        foreach (var projection in projections)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var preparation = await TryBuildLifecycleRequestAsync(dbContext, projection, cancellationToken);
            if (preparation.Request is null)
            {
                skippedCount++;
                var warning = preparation.Warning ?? $"Projection {projection.Id:D} is missing durable rebuild inputs.";
                warnings.Add(warning);
                items.Add(new CognitiveMemoryProjectionRebuildItemResult(
                    projection.Id,
                    projection.MemoryRecordId,
                    CognitiveMemoryProjectionLifecycleDecisionKind.NoChange,
                    projection.Status,
                    "projection-rebuild:skipped",
                    warning));
                continue;
            }

            try
            {
                var result = await projectionLifecycleService.ProjectAsync(preparation.Request, cancellationToken);
                ApplyProjectionResult(projection, result.ProjectionRecord, nowUtc);
                items.Add(new CognitiveMemoryProjectionRebuildItemResult(
                    projection.Id,
                    projection.MemoryRecordId,
                    result.Decision.DecisionKind,
                    projection.Status,
                    result.ProviderTrace,
                    projection.Status == CognitiveMemoryProjectionStatus.Failed ? projection.FailureMessage : null));

                if (projection.Status == CognitiveMemoryProjectionStatus.Projected)
                {
                    projectedCount++;
                }
                else if (projection.Status == CognitiveMemoryProjectionStatus.Failed)
                {
                    failedCount++;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failedCount++;
                projection.Status = CognitiveMemoryProjectionStatus.Failed;
                projection.RebuildRequired = true;
                projection.StaleReason = CognitiveMemoryProjectionStaleReason.PreviousFailure;
                projection.FailureCode = exception.GetType().Name;
                projection.FailureMessage = exception.Message;
                projection.UpdatedAtUtc = nowUtc;
                projection.ConcurrencyToken = Guid.NewGuid();
                warnings.Add($"Projection {projection.Id:D} failed: {exception.Message}");
                items.Add(new CognitiveMemoryProjectionRebuildItemResult(
                    projection.Id,
                    projection.MemoryRecordId,
                    CognitiveMemoryProjectionLifecycleDecisionKind.Failed,
                    projection.Status,
                    $"projection-rebuild:failed:{exception.GetType().Name}",
                    exception.Message));

                logger.LogWarning(
                    exception,
                    "Cognitive memory projection rebuild failed. ProjectionId={ProjectionId} MemoryRecordId={MemoryRecordId}",
                    projection.Id,
                    projection.MemoryRecordId);
            }
        }

        if (missingProjectionDefaults is not null && projections.Count < take)
        {
            var missingRecords = await BuildMissingProjectionRecordQuery(
                    dbContext,
                    request.ProjectId,
                    missingProjectionDefaults,
                    projections.Select(projection => projection.MemoryRecordId).ToArray())
                .OrderBy(record => record.UpdatedAtUtc)
                .ThenBy(record => record.Id)
                .Take(take - projections.Count)
                .ToListAsync(cancellationToken);

            selectedCount += missingRecords.Count;
            foreach (var record in missingRecords)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var preparation = await TryBuildLifecycleRequestAsync(
                    dbContext,
                    record,
                    CreateMissingProjectionBuildOptions(record, missingProjectionDefaults),
                    cancellationToken);
                if (preparation.Request is null)
                {
                    skippedCount++;
                    var warning = preparation.Warning ?? $"Memory record {record.Id:D} is missing durable projection inputs.";
                    warnings.Add(warning);
                    items.Add(new CognitiveMemoryProjectionRebuildItemResult(
                        Guid.Empty,
                        record.Id,
                        CognitiveMemoryProjectionLifecycleDecisionKind.NoChange,
                        CognitiveMemoryProjectionStatus.RebuildRequired,
                        "projection-rebuild:missing-skipped",
                        warning));
                    continue;
                }

                try
                {
                    var result = await projectionLifecycleService.ProjectAsync(preparation.Request, cancellationToken);
                    dbContext.Add(result.ProjectionRecord);
                    items.Add(new CognitiveMemoryProjectionRebuildItemResult(
                        result.ProjectionRecord.Id,
                        record.Id,
                        result.Decision.DecisionKind,
                        result.ProjectionRecord.Status,
                        result.ProviderTrace,
                        result.ProjectionRecord.Status == CognitiveMemoryProjectionStatus.Failed ? result.ProjectionRecord.FailureMessage : null));

                    if (result.ProjectionRecord.Status == CognitiveMemoryProjectionStatus.Projected)
                    {
                        projectedCount++;
                    }
                    else if (result.ProjectionRecord.Status == CognitiveMemoryProjectionStatus.Failed)
                    {
                        failedCount++;
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    failedCount++;
                    warnings.Add($"Memory record {record.Id:D} projection failed: {exception.Message}");
                    items.Add(new CognitiveMemoryProjectionRebuildItemResult(
                        Guid.Empty,
                        record.Id,
                        CognitiveMemoryProjectionLifecycleDecisionKind.Failed,
                        CognitiveMemoryProjectionStatus.Failed,
                        $"projection-rebuild:missing-failed:{exception.GetType().Name}",
                        exception.Message));

                    logger.LogWarning(
                        exception,
                        "Cognitive memory missing projection build failed. MemoryRecordId={MemoryRecordId}",
                        record.Id);
                }
            }
        }

        run.Status = failedCount == 0 ? CognitiveMemoryRunStatus.Succeeded : CognitiveMemoryRunStatus.Blocked;
        run.CompletedAtUtc = clock.GetUtcNow();
        run.FailureCode = failedCount == 0 ? string.Empty : "ProjectionRebuildFailures";
        run.FailureMessage = failedCount == 0 ? string.Empty : $"{failedCount} projection rebuild item(s) failed.";
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CognitiveMemoryProjectionRebuildResult(
            run.Id,
            run.Status,
            selectedCount,
            projectedCount,
            failedCount,
            skippedCount,
            items,
            warnings);
    }

    private static IQueryable<CognitiveMemoryProjectionRecord> BuildProjectionQuery(
        AppDbContext dbContext,
        Guid? projectId,
        CognitiveMemoryProjectionCollectionName? collectionName)
    {
        var query = dbContext.Set<CognitiveMemoryProjectionRecord>()
            .Where(projection =>
                projection.RebuildRequired ||
                projection.Status == CognitiveMemoryProjectionStatus.RebuildRequired ||
                projection.Status == CognitiveMemoryProjectionStatus.Failed);

        if (projectId.HasValue)
        {
            query = query.Where(projection => projection.ProjectId == projectId.Value);
        }

        if (collectionName is { } effectiveCollectionName)
        {
            query = query.Where(projection => projection.CollectionName == effectiveCollectionName.Value);
        }

        return query;
    }

    private static IQueryable<CognitiveMemoryRecord> BuildMissingProjectionRecordQuery(
        AppDbContext dbContext,
        Guid? projectId,
        ProjectionDefaults defaults,
        IReadOnlyList<Guid> excludedMemoryRecordIds)
    {
        var matchingProjectionRecordIds = dbContext.Set<CognitiveMemoryProjectionRecord>()
            .Where(projection =>
                projection.ProjectionStoreKind == defaults.ProjectionStoreKind &&
                projection.ProjectionKind == CognitiveMemoryProjectionKind.VectorCollection &&
                projection.CollectionName == defaults.CollectionName.Value &&
                projection.ProjectionProfileId == defaults.ProjectionProfileId.Value &&
                projection.EmbeddingProfileId == defaults.EmbeddingProfileId.Value)
            .Select(projection => projection.MemoryRecordId);
        var linkedRecordIds = dbContext.Set<CognitiveMemorySourceLinkRecord>()
            .Select(link => link.MemoryRecordId);
        var claimedRecordIds = dbContext.Set<CognitiveMemoryClaimRecord>()
            .Where(claim => claim.MemoryRecordId.HasValue && claim.PrimaryContextFrameId.HasValue)
            .Select(claim => claim.MemoryRecordId!.Value);
        var evidencedRecordIds = dbContext.Set<CognitiveMemoryRecordEvidenceAnchorRecord>()
            .Select(link => link.MemoryRecordId);
        var entityContextFrameIds = dbContext.Set<CognitiveMemoryEntityRecord>()
            .Where(entity => entity.PrimaryContextFrameId.HasValue)
            .Select(entity => entity.PrimaryContextFrameId!.Value);

        var query = dbContext.Set<CognitiveMemoryRecord>()
            .Where(record =>
                record.ValidationState == CognitiveMemoryValidationState.MachineGenerated ||
                record.ValidationState == CognitiveMemoryValidationState.HumanReviewed ||
                record.ValidationState == CognitiveMemoryValidationState.Approved)
            .Where(record =>
                record.PrimaryContextFrameId.HasValue &&
                linkedRecordIds.Contains(record.Id) &&
                claimedRecordIds.Contains(record.Id) &&
                evidencedRecordIds.Contains(record.Id) &&
                entityContextFrameIds.Contains(record.PrimaryContextFrameId.Value) &&
                !matchingProjectionRecordIds.Contains(record.Id));

        if (projectId.HasValue)
        {
            query = query.Where(record => record.ProjectId == projectId.Value);
        }

        if (excludedMemoryRecordIds.Count > 0)
        {
            query = query.Where(record => !excludedMemoryRecordIds.Contains(record.Id));
        }

        return query;
    }

    private static async Task<ProjectionRebuildPreparation> TryBuildLifecycleRequestAsync(
        AppDbContext dbContext,
        CognitiveMemoryProjectionRecord projection,
        CancellationToken cancellationToken)
    {
        var record = await dbContext.Set<CognitiveMemoryRecord>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == projection.MemoryRecordId, cancellationToken);
        if (record is null)
        {
            return ProjectionRebuildPreparation.Skip($"Projection {projection.Id:D} references missing memory record {projection.MemoryRecordId:D}.");
        }

        return await TryBuildLifecycleRequestAsync(
            dbContext,
            record,
            new ProjectionBuildOptions(
                new CognitiveMemoryProjectionCollectionName(projection.CollectionName),
                projection.ProjectionStoreKind,
                projection.TargetProviderName,
                projection.ProjectionKind,
                new CognitiveMemoryProjectionProfileId(projection.ProjectionProfileId),
                new CognitiveMemoryEmbeddingProfileId(projection.EmbeddingProfileId),
                new CognitiveMemoryPayloadSchemaVersion(projection.ProjectionSchemaVersion),
                new CognitiveMemoryAlgorithmVersion(string.IsNullOrWhiteSpace(projection.AlgorithmVersion) ? AlgorithmVersion : projection.AlgorithmVersion),
                projection.VectorDimensions > 0 ? projection.VectorDimensions : null,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["projectionRecordId"] = projection.Id.ToString("D"),
                    ["rebuildRequired"] = projection.RebuildRequired.ToString()
                },
                ["projection-rebuild"]),
            cancellationToken);
    }

    private static async Task<ProjectionRebuildPreparation> TryBuildLifecycleRequestAsync(
        AppDbContext dbContext,
        CognitiveMemoryRecord record,
        ProjectionBuildOptions options,
        CancellationToken cancellationToken)
    {
        var sourceLinks = await dbContext.Set<CognitiveMemorySourceLinkRecord>()
            .AsNoTracking()
            .Where(link => link.MemoryRecordId == record.Id)
            .OrderBy(link => link.EvidenceRole)
            .ThenBy(link => link.Id)
            .ToListAsync(cancellationToken);
        if (sourceLinks.Count == 0)
        {
            return ProjectionRebuildPreparation.Skip($"Memory record {record.Id:D} has no source links for projection rebuild.");
        }

        var sourceItemIds = sourceLinks.Select(link => link.SourceItemId).Distinct().ToArray();
        var sourceItems = await dbContext.Set<CognitiveMemorySourceItemRecord>()
            .AsNoTracking()
            .Where(item => sourceItemIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var primarySourceLink = sourceLinks.First();
        sourceItems.TryGetValue(primarySourceLink.SourceItemId, out var primarySourceItem);

        var evidenceAnchorIds = await dbContext.Set<CognitiveMemoryRecordEvidenceAnchorRecord>()
            .AsNoTracking()
            .Where(link => link.MemoryRecordId == record.Id)
            .Select(link => link.EvidenceAnchorId)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (evidenceAnchorIds.Count == 0)
        {
            evidenceAnchorIds = await dbContext.Set<CognitiveMemoryEvidenceAnchorRecord>()
                .AsNoTracking()
                .Where(anchor => anchor.SourceItemId.HasValue && sourceItemIds.Contains(anchor.SourceItemId.Value))
                .Select(anchor => anchor.Id)
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        if (evidenceAnchorIds.Count == 0)
        {
            return ProjectionRebuildPreparation.Skip($"Memory record {record.Id:D} has no evidence anchors for projection rebuild.");
        }

        var claims = await dbContext.Set<CognitiveMemoryClaimRecord>()
            .AsNoTracking()
            .Where(claim => claim.MemoryRecordId == record.Id)
            .OrderBy(claim => claim.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        if (claims.Count == 0)
        {
            return ProjectionRebuildPreparation.Skip($"Memory record {record.Id:D} has no claims for projection rebuild.");
        }

        var contextFrameIds = claims
            .Select(claim => claim.PrimaryContextFrameId)
            .Append(record.PrimaryContextFrameId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();
        if (contextFrameIds.Length == 0)
        {
            return ProjectionRebuildPreparation.Skip($"Memory record {record.Id:D} has no context frame for projection rebuild.");
        }

        var projectId = record.ProjectId;
        var entityIds = await dbContext.Set<CognitiveMemoryEntityRecord>()
            .AsNoTracking()
            .Where(entity =>
                entity.PrimaryContextFrameId.HasValue &&
                contextFrameIds.Contains(entity.PrimaryContextFrameId.Value) &&
                (entity.ProjectId == projectId || entity.ProjectId == null))
            .Select(entity => entity.Id)
            .Distinct()
            .ToListAsync(cancellationToken);

        var contextBoundaryPolicies = await dbContext.Set<CognitiveMemoryContextBoundaryRecord>()
            .AsNoTracking()
            .Where(boundary =>
                (boundary.ProjectId == projectId || boundary.ProjectId == null) &&
                (contextFrameIds.Contains(boundary.SourceContextFrameId) ||
                 contextFrameIds.Contains(boundary.TargetContextFrameId)))
            .Select(boundary => boundary.BoundaryPolicy)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (entityIds.Count == 0 && contextBoundaryPolicies.Count == 0)
        {
            return ProjectionRebuildPreparation.Skip($"Memory record {record.Id:D} has no entity or context-boundary metadata for projection rebuild.");
        }

        var payload = new CognitiveMemoryClaimProjectionPayload(
            options.ProjectionSchemaVersion,
            CognitiveMemoryProjectionPayloadSchemaKind.ClaimContainer,
            new CognitiveMemoryRecordId(record.Id),
            claims.Select(claim => new CognitiveMemoryClaimId(claim.Id)).ToArray(),
            contextFrameIds.Select(id => new CognitiveMemoryContextFrameId(id)).ToArray(),
            entityIds.Select(id => new CognitiveMemoryEntityId(id)).ToArray(),
            claims.Select(claim => claim.CurrentBeliefState).Distinct().ToArray(),
            contextBoundaryPolicies.ToArray(),
            record.ConfidenceBucket);

        var request = new CognitiveMemoryProjectionLifecycleRequest(
            options.CollectionName,
            options.ProjectionStoreKind,
            options.TargetProviderName,
            primarySourceItem?.SourceSystem ?? DefaultSourceSystem,
            primarySourceItem?.SourceItemKey ?? DefaultSourceItemKey,
            record,
            sourceLinks,
            payload,
            evidenceAnchorIds.Select(id => new CognitiveMemoryEvidenceAnchorId(id)).ToArray(),
            options.ProjectionKind,
            options.ProjectionProfileId,
            options.EmbeddingProfileId,
            options.ProjectionSchemaVersion,
            options.AlgorithmVersion,
            new CognitiveMemoryProcessingBudget(1, 64_000, TimeSpan.FromSeconds(30)),
            options.ExpectedVectorDimensions,
            options.Metadata,
            options.Tags);
        return new ProjectionRebuildPreparation(request, null);
    }

    private static void ApplyProjectionResult(
        CognitiveMemoryProjectionRecord projection,
        CognitiveMemoryProjectionRecord result,
        DateTimeOffset nowUtc)
    {
        projection.TargetProviderName = result.TargetProviderName;
        projection.CollectionName = result.CollectionName;
        projection.PointId = result.PointId;
        projection.SourceHashAlgorithm = result.SourceHashAlgorithm;
        projection.SourceHash = result.SourceHash;
        projection.PayloadHashAlgorithm = result.PayloadHashAlgorithm;
        projection.PayloadHash = result.PayloadHash;
        projection.VectorDimensions = result.VectorDimensions;
        projection.Status = result.Status;
        projection.StaleReason = result.StaleReason;
        projection.RebuildRequired = result.RebuildRequired;
        projection.FailureCode = result.FailureCode;
        projection.FailureMessage = result.FailureMessage;
        projection.LastProjectedAtUtc = result.LastProjectedAtUtc;
        projection.UpdatedAtUtc = nowUtc;
        projection.ConcurrencyToken = Guid.NewGuid();
    }

    private static string BuildRunIdempotencyKey(
        CognitiveMemoryProjectionRebuildRequest request,
        string actorId,
        DateTimeOffset nowUtc)
        => $"projection-rebuild:{request.ProjectId?.ToString("D") ?? "global"}:{actorId}:{nowUtc:yyyyMMddHHmmssfffffff}";

    private static string BuildRequestHash(CognitiveMemoryProjectionRebuildRequest request, string actorId)
        => CognitiveMemoryHash.FromUtf8(string.Join(
            "|",
            request.ProjectId?.ToString("D") ?? string.Empty,
            request.Take.ToString(),
            actorId,
            request.CollectionName?.Value ?? string.Empty,
            request.ProjectMissingRecords.ToString(),
            request.ProjectionProfileId?.Value ?? string.Empty,
            request.EmbeddingProfileId?.Value ?? string.Empty,
            request.TargetProviderName ?? string.Empty,
            request.ProjectionStoreKind?.ToString() ?? string.Empty,
            request.ExpectedVectorDimensions?.ToString() ?? string.Empty)).Value;

    private ProjectionDefaults ResolveProjectionDefaults(CognitiveMemoryProjectionRebuildRequest request)
    {
        var collectionName = request.CollectionName?.Value ?? projectionOptions.CollectionName;
        var projectionProfileId = request.ProjectionProfileId?.Value ?? projectionOptions.ProjectionProfileId;
        var embeddingProfileId = request.EmbeddingProfileId?.Value ?? projectionOptions.EmbeddingProfileId;
        var targetProviderName = request.TargetProviderName ?? projectionOptions.TargetProviderName;
        if (string.IsNullOrWhiteSpace(collectionName) ||
            string.IsNullOrWhiteSpace(projectionProfileId) ||
            string.IsNullOrWhiteSpace(embeddingProfileId) ||
            string.IsNullOrWhiteSpace(targetProviderName))
        {
            throw new InvalidOperationException(
                "Projecting missing cognitive-memory records requires collectionName, projectionProfileId, embeddingProfileId, and targetProviderName either in the request or configured projection defaults.");
        }

        var vectorDimensions = request.ExpectedVectorDimensions ?? projectionOptions.VectorDimensions;
        if (vectorDimensions is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.ExpectedVectorDimensions), "Vector dimensions must be positive when supplied.");
        }

        return new ProjectionDefaults(
            new CognitiveMemoryProjectionCollectionName(collectionName),
            new CognitiveMemoryProjectionProfileId(projectionProfileId),
            new CognitiveMemoryEmbeddingProfileId(embeddingProfileId),
            CognitiveMemoryGuard.EnsureText(targetProviderName, nameof(request.TargetProviderName)),
            request.ProjectionStoreKind ?? projectionOptions.ProjectionStoreKind,
            vectorDimensions);
    }

    private static ProjectionBuildOptions CreateMissingProjectionBuildOptions(
        CognitiveMemoryRecord record,
        ProjectionDefaults defaults)
    {
        var algorithmVersion = string.IsNullOrWhiteSpace(record.AlgorithmVersion)
            ? AlgorithmVersion
            : record.AlgorithmVersion.Trim();

        return new ProjectionBuildOptions(
            defaults.CollectionName,
            defaults.ProjectionStoreKind,
            defaults.TargetProviderName,
            CognitiveMemoryProjectionKind.VectorCollection,
            defaults.ProjectionProfileId,
            defaults.EmbeddingProfileId,
            new CognitiveMemoryPayloadSchemaVersion(DefaultProjectionSchemaVersion),
            new CognitiveMemoryAlgorithmVersion(algorithmVersion),
            defaults.ExpectedVectorDimensions,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["projectionRecordId"] = "missing",
                ["rebuildRequired"] = bool.TrueString
            },
            ["projection-rebuild", "projection-missing"]);
    }

    private sealed record ProjectionRebuildPreparation(
        CognitiveMemoryProjectionLifecycleRequest? Request,
        string? Warning)
    {
        public static ProjectionRebuildPreparation Skip(string warning) => new(null, warning);
    }

    private sealed record ProjectionDefaults(
        CognitiveMemoryProjectionCollectionName CollectionName,
        CognitiveMemoryProjectionProfileId ProjectionProfileId,
        CognitiveMemoryEmbeddingProfileId EmbeddingProfileId,
        string TargetProviderName,
        CognitiveMemoryProjectionStoreKind ProjectionStoreKind,
        int? ExpectedVectorDimensions);

    private sealed record ProjectionBuildOptions(
        CognitiveMemoryProjectionCollectionName CollectionName,
        CognitiveMemoryProjectionStoreKind ProjectionStoreKind,
        string TargetProviderName,
        CognitiveMemoryProjectionKind ProjectionKind,
        CognitiveMemoryProjectionProfileId ProjectionProfileId,
        CognitiveMemoryEmbeddingProfileId EmbeddingProfileId,
        CognitiveMemoryPayloadSchemaVersion ProjectionSchemaVersion,
        CognitiveMemoryAlgorithmVersion AlgorithmVersion,
        int? ExpectedVectorDimensions,
        IReadOnlyDictionary<string, string> Metadata,
        IReadOnlyList<string> Tags);
}

using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemoryProjectionRebuildService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ICognitiveMemoryProjectionLifecycleService projectionLifecycleService,
    IClock clock,
    ILogger<CognitiveMemoryProjectionRebuildService> logger) : ICognitiveMemoryProjectionRebuildService
{
    private const string AlgorithmVersion = "projection-rebuild-runner-v1";
    private const string DefaultSourceSystem = "durable-memory";
    private const string DefaultSourceItemKey = "unknown-source-item";
    private const int MaximumTake = 500;

    public async ValueTask<CognitiveMemoryProjectionRebuildResult> RebuildAsync(
        CognitiveMemoryProjectionRebuildRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var take = request.Take is > 0 and <= MaximumTake
            ? request.Take
            : throw new ArgumentOutOfRangeException(nameof(request.Take), $"Take must be between 1 and {MaximumTake}.");
        var actorId = CognitiveMemoryGuard.EnsureText(request.ActorId, nameof(request.ActorId));

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

        var projections = await BuildProjectionQuery(dbContext, request)
            .OrderBy(projection => projection.UpdatedAtUtc)
            .ThenBy(projection => projection.Id)
            .Take(take)
            .ToListAsync(cancellationToken);

        var items = new List<CognitiveMemoryProjectionRebuildItemResult>(projections.Count);
        var warnings = new List<string>();
        var projectedCount = 0;
        var failedCount = 0;
        var skippedCount = 0;

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

        run.Status = failedCount == 0 ? CognitiveMemoryRunStatus.Succeeded : CognitiveMemoryRunStatus.Blocked;
        run.CompletedAtUtc = clock.GetUtcNow();
        run.FailureCode = failedCount == 0 ? string.Empty : "ProjectionRebuildFailures";
        run.FailureMessage = failedCount == 0 ? string.Empty : $"{failedCount} projection rebuild item(s) failed.";
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CognitiveMemoryProjectionRebuildResult(
            run.Id,
            run.Status,
            projections.Count,
            projectedCount,
            failedCount,
            skippedCount,
            items,
            warnings);
    }

    private static IQueryable<CognitiveMemoryProjectionRecord> BuildProjectionQuery(
        AppDbContext dbContext,
        CognitiveMemoryProjectionRebuildRequest request)
    {
        var query = dbContext.Set<CognitiveMemoryProjectionRecord>()
            .Where(projection =>
                projection.RebuildRequired ||
                projection.Status == CognitiveMemoryProjectionStatus.RebuildRequired ||
                projection.Status == CognitiveMemoryProjectionStatus.Failed);

        if (request.ProjectId.HasValue)
        {
            query = query.Where(projection => projection.ProjectId == request.ProjectId.Value);
        }

        if (request.CollectionName is { } collectionName)
        {
            query = query.Where(projection => projection.CollectionName == collectionName.Value);
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

        var payload = new CognitiveMemoryClaimProjectionPayload(
            new CognitiveMemoryPayloadSchemaVersion(projection.ProjectionSchemaVersion),
            CognitiveMemoryProjectionPayloadSchemaKind.ClaimContainer,
            new CognitiveMemoryRecordId(record.Id),
            claims.Select(claim => new CognitiveMemoryClaimId(claim.Id)).ToArray(),
            contextFrameIds.Select(id => new CognitiveMemoryContextFrameId(id)).ToArray(),
            [],
            claims.Select(claim => claim.CurrentBeliefState).Distinct().ToArray(),
            [],
            record.ConfidenceBucket);

        var request = new CognitiveMemoryProjectionLifecycleRequest(
            new CognitiveMemoryProjectionCollectionName(projection.CollectionName),
            projection.ProjectionStoreKind,
            projection.TargetProviderName,
            primarySourceItem?.SourceSystem ?? DefaultSourceSystem,
            primarySourceItem?.SourceItemKey ?? DefaultSourceItemKey,
            record,
            sourceLinks,
            payload,
            evidenceAnchorIds.Select(id => new CognitiveMemoryEvidenceAnchorId(id)).ToArray(),
            projection.ProjectionKind,
            new CognitiveMemoryProjectionProfileId(projection.ProjectionProfileId),
            new CognitiveMemoryEmbeddingProfileId(projection.EmbeddingProfileId),
            new CognitiveMemoryPayloadSchemaVersion(projection.ProjectionSchemaVersion),
            new CognitiveMemoryAlgorithmVersion(string.IsNullOrWhiteSpace(projection.AlgorithmVersion) ? AlgorithmVersion : projection.AlgorithmVersion),
            new CognitiveMemoryProcessingBudget(1, 64_000, TimeSpan.FromSeconds(30)),
            projection.VectorDimensions > 0 ? projection.VectorDimensions : null,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["projectionRecordId"] = projection.Id.ToString("D"),
                ["rebuildRequired"] = projection.RebuildRequired.ToString()
            },
            ["projection-rebuild"]);
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
            request.CollectionName?.Value ?? string.Empty)).Value;

    private sealed record ProjectionRebuildPreparation(
        CognitiveMemoryProjectionLifecycleRequest? Request,
        string? Warning)
    {
        public static ProjectionRebuildPreparation Skip(string warning) => new(null, warning);
    }
}

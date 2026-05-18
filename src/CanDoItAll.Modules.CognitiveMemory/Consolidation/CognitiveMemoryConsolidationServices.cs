using System.Text;
using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemoryConsolidationEngine(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ICognitiveMemoryMutationAuthority mutationAuthority,
    ICognitiveMemoryConsolidationCandidateApplicator candidateApplicator,
    ICognitiveMemoryScoreGeometryDriver scoreGeometryDriver,
    IClock clock,
    ILogger<CognitiveMemoryConsolidationEngine> logger) : ICognitiveMemoryConsolidationEngine
{
    private const string AlgorithmVersion = "consolidation-v1";
    private const string ConsolidationCursorSource = "CognitiveMemorySourceItems";
    private const string WorkbenchProjectStructureSourceSystem = "WorkbenchProjectStructure";
    private const string ProjectNodeSourceItemType = "ProjectNode";
    private const string ProjectLinkSourceItemType = "ProjectLink";
    private const string ProjectFileNodeMarker = "Object type: File";
    private const int MaxEvidenceAnchorsPerCandidate = 8;
    private static readonly IReadOnlyList<CognitiveMemoryScoreShapeSnapshot> CandidateShapes = BuildCandidateShapes();

    public async ValueTask<CognitiveMemoryConsolidationRunResult> RunAsync(
        CognitiveMemoryConsolidationRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);

        var budget = request.Budget ?? CognitiveMemoryConsolidationBudget.Default;
        var storedIdempotencyKey = CreateStoredIdempotencyKey(request.IdempotencyKey);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var existingRun = await dbContext.Set<CognitiveMemoryConsolidationRunRecord>()
            .AsNoTracking()
            .FirstOrDefaultAsync(run => run.IdempotencyKey == storedIdempotencyKey, cancellationToken);
        if (existingRun is not null)
        {
            return await CreateReplayResultAsync(dbContext, existingRun, cancellationToken);
        }

        var now = clock.GetUtcNow();
        var activeLease = await FindActiveLeaseAsync(dbContext, request, now, cancellationToken);
        var runId = CognitiveMemoryConsolidationRunId.New();
        if (activeLease is not null)
        {
            return await CreateBlockedLeaseRunAsync(
                dbContext,
                runId,
                request,
                storedIdempotencyKey,
                activeLease,
                now,
                cancellationToken);
        }

        var inputHash = CreateInputHash(request, budget);
        var run = new CognitiveMemoryRunRecord
        {
            Id = runId.Value,
            ProjectId = request.ProjectId,
            RunKind = CognitiveMemoryRunKind.Consolidation,
            Status = CognitiveMemoryRunStatus.Running,
            OperationMode = CognitiveMemoryOperationMode.Consolidate,
            IdempotencyKey = storedIdempotencyKey,
            InputHash = inputHash.Value,
            AlgorithmVersion = AlgorithmVersion,
            Cursor = request.Cursor ?? string.Empty,
            StartedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        var consolidationRun = new CognitiveMemoryConsolidationRunRecord
        {
            Id = runId.Value,
            ProjectId = request.ProjectId,
            Mode = request.Mode,
            TriggerKind = request.TriggerKind,
            Status = CognitiveMemoryRunStatus.Running,
            ProfileName = request.Profile.Name.Trim(),
            IdempotencyKey = storedIdempotencyKey,
            InputHash = inputHash.Value,
            OutputHash = string.Empty,
            AlgorithmVersion = AlgorithmVersion,
            Cursor = request.Cursor ?? string.Empty,
            LeaseOwnerId = request.PolicyContext.ActorId.Trim(),
            LeaseExpiresAtUtc = now.Add(budget.LeaseDuration),
            StartedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };

        dbContext.Add(run);
        dbContext.Add(consolidationRun);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            return await ExecuteRunAsync(dbContext, request, budget, run, consolidationRun, now, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                exception,
                "Cognitive memory consolidation failed. ProjectId={ProjectId} Mode={Mode} Trigger={TriggerKind} RunId={RunId}",
                request.ProjectId,
                request.Mode,
                request.TriggerKind,
                runId.Value);

            var failedAt = clock.GetUtcNow();
            run.Status = CognitiveMemoryRunStatus.Failed;
            run.CompletedAtUtc = failedAt;
            run.FailureCode = "UnhandledConsolidationFailure";
            run.FailureMessage = exception.Message;
            consolidationRun.Status = CognitiveMemoryRunStatus.Failed;
            consolidationRun.CompletedAtUtc = failedAt;
            consolidationRun.FailureCode = "UnhandledConsolidationFailure";
            consolidationRun.FailureMessage = exception.Message;
            await dbContext.SaveChangesAsync(cancellationToken);

            return new CognitiveMemoryConsolidationRunResult(
                new CognitiveMemoryConsolidationRunId(consolidationRun.Id),
                CognitiveMemoryRunStatus.Failed,
                consolidationRun.SourceItemsScanned,
                consolidationRun.CandidatesCreated,
                consolidationRun.MutationCommandsSubmitted,
                consolidationRun.ReviewItemsCreated,
                consolidationRun.ProjectionInvalidations,
                null,
                null,
                [$"Consolidation failed: {exception.GetType().Name}."]);
        }
    }

    private async Task<CognitiveMemoryConsolidationRunResult> ExecuteRunAsync(
        AppDbContext dbContext,
        CognitiveMemoryConsolidationRunRequest request,
        CognitiveMemoryConsolidationBudget budget,
        CognitiveMemoryRunRecord run,
        CognitiveMemoryConsolidationRunRecord consolidationRun,
        DateTimeOffset startedAtUtc,
        CancellationToken cancellationToken)
    {
        var sourceItems = request.Profile.ProcessSourceItems
            ? await LoadSourceItemsAsync(dbContext, request, budget, cancellationToken)
            : [];
        var candidateLimit = Math.Min(budget.CandidateLimit, request.Profile.MaxItems);
        var warnings = new List<string>(capacity: 2);
        var createdCandidateIds = new List<Guid>(candidateLimit);
        var createdSourceItemIds = new List<Guid>(candidateLimit);
        var rejectedCandidates = 0;
        var reviewItemsCreated = 0;
        var mutationCommandsSubmitted = 0;

        if (request.Profile.CreateHumanReviewItems)
        {
            reviewItemsCreated += await BackfillMissingReviewItemsAsync(
                dbContext,
                request,
                budget,
                consolidationRun.Id,
                startedAtUtc,
                cancellationToken);
        }

        foreach (var sourceItem in sourceItems)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (createdCandidateIds.Count >= candidateLimit)
            {
                warnings.Add("Consolidation candidate budget stopped source processing before all source items were evaluated.");
                break;
            }

            var candidateKind = ResolveCandidateKind(request, sourceItem);
            if (await CandidateAlreadyProcessedAsync(dbContext, request.ProjectId, sourceItem, candidateKind, cancellationToken))
            {
                continue;
            }

            var evidenceAnchorIds = await LoadEvidenceAnchorIdsAsync(dbContext, sourceItem.Id, cancellationToken);
            var primaryEvidenceAnchorId = evidenceAnchorIds.Count > 0 ? evidenceAnchorIds[0] : (Guid?)null;
            var scoreTrace = await EvaluateCandidateAsync(
                dbContext,
                consolidationRun.Id,
                sourceItem,
                evidenceAnchorIds,
                candidateKind,
                startedAtUtc,
                cancellationToken);
            var payload = CreatePayload(sourceItem, primaryEvidenceAnchorId, null, null, candidateKind, budget);
            var outputHash = CognitiveMemoryHash.FromUtf8(payload.Summary);
            var mutationResult = await mutationAuthority.SubmitAsync(
                CreateMutationCommand(request, sourceItem, evidenceAnchorIds, candidateKind, payload),
                cancellationToken);
            mutationCommandsSubmitted++;

            Guid? reviewItemId = null;
            if (mutationResult.ReviewRequired && request.Profile.CreateHumanReviewItems && reviewItemsCreated < budget.ReviewItemLimit)
            {
                reviewItemId = Guid.NewGuid();
                reviewItemsCreated++;
                dbContext.Add(new CognitiveMemoryReviewItemRecord
                {
                    Id = reviewItemId.Value,
                    ProjectId = request.ProjectId,
                    ReviewKind = ResolveReviewKind(candidateKind),
                    Status = CognitiveMemoryReviewStatus.Pending,
                    SubjectKind = CognitiveMemoryReviewSubjectKind.Run,
                    SubjectId = consolidationRun.Id,
                    RiskLevel = ResolveRiskLevel(sourceItem),
                    ReasonCode = "ConsolidationCandidateReview",
                    ReasonText = mutationResult.ReviewReason ?? "Generated consolidation candidate requires review before authoritative memory changes.",
                    SourceEvidenceCount = evidenceAnchorIds.Count,
                    CreatedAtUtc = startedAtUtc,
                    ConcurrencyToken = Guid.NewGuid()
                });
            }

            var resolvedPayload = payload with
            {
                MutationCommandId = mutationResult.CommandId,
                ReviewItemId = reviewItemId
            };
            var candidateStatus = ResolveCandidateStatus(mutationResult);
            if (candidateStatus == CognitiveMemoryConsolidationCandidateStatus.Rejected)
            {
                rejectedCandidates++;
                warnings.Add($"Candidate for source item '{sourceItem.SourceItemKey}' was rejected by mutation authority: {mutationResult.ReviewReason ?? "no reason supplied"}.");
            }

            var candidate = new CognitiveMemoryConsolidationCandidateRecord
            {
                Id = CognitiveMemoryConsolidationCandidateId.New().Value,
                RunId = consolidationRun.Id,
                ProjectId = request.ProjectId,
                CandidateKind = candidateKind,
                Status = candidateStatus,
                SourceItemId = sourceItem.Id,
                EvidenceAnchorId = primaryEvidenceAnchorId,
                MutationCommandId = mutationResult.CommandId,
                ReviewItemId = reviewItemId,
                ScoreEvaluationTraceId = scoreTrace.Id.Value,
                ScoreBucket = scoreTrace.ScalarProjection?.Bucket ?? CognitiveMemoryScoreProjectionBucket.Unknown,
                DisplayPriorityProjection = scoreTrace.ScalarProjection?.DisplayScore,
                SourceContentHash = sourceItem.ContentHash,
                OutputHash = outputHash.Value,
                AlgorithmVersion = AlgorithmVersion,
                ReasonCode = candidateStatus == CognitiveMemoryConsolidationCandidateStatus.Rejected ? "MutationRejected" : "GeneratedCandidate",
                ReasonText = CreateCandidateReason(candidateKind, sourceItem, mutationResult),
                PayloadJson = JsonSerializer.Serialize(
                    resolvedPayload,
                    CognitiveMemoryJsonSerializerContext.Default.CognitiveMemoryConsolidationCandidatePayload),
                CreatedAtUtc = startedAtUtc,
                ConcurrencyToken = Guid.NewGuid()
            };
            dbContext.Add(candidate);
            if (candidate.Status == CognitiveMemoryConsolidationCandidateStatus.MutationSubmitted)
            {
                _ = await candidateApplicator.ApplyAsync(
                    dbContext,
                    candidate,
                    resolvedPayload,
                    CognitiveMemoryValidationState.MachineGenerated,
                    CognitiveMemoryStabilityState.Experimental,
                    request.PolicyContext.ActorId,
                    startedAtUtc,
                    cancellationToken);
            }

            createdCandidateIds.Add(candidate.Id);
            createdSourceItemIds.Add(sourceItem.Id);
        }

        var projectionInvalidations = rejectedCandidates == 0 && request.Profile.RebuildProjections
            ? await MarkProjectionInvalidationsAsync(dbContext, createdSourceItemIds, startedAtUtc, cancellationToken)
            : 0;
        var completedAt = clock.GetUtcNow();
        var nextCursor = rejectedCandidates == 0
            ? ResolveNextCursor(request, sourceItems)
            : string.Empty;

        if (rejectedCandidates == 0)
        {
            await UpdateCursorAsync(
                dbContext,
                request,
                consolidationRun.Id,
                nextCursor,
                sourceItems,
                completedAt,
                cancellationToken);
        }

        var status = rejectedCandidates == 0
            ? CognitiveMemoryRunStatus.Succeeded
            : CognitiveMemoryRunStatus.Blocked;
        var report = new CognitiveMemoryConsolidationReportPayload(
            consolidationRun.Id,
            request.ProjectId,
            request.Mode,
            request.TriggerKind,
            request.Profile.Name,
            sourceItems.Count,
            createdCandidateIds.Count,
            mutationCommandsSubmitted,
            reviewItemsCreated,
            projectionInvalidations,
            warnings);
        var reportJson = JsonSerializer.Serialize(
            report,
            CognitiveMemoryJsonSerializerContext.Default.CognitiveMemoryConsolidationReportPayload);
        var reportHash = CognitiveMemoryHash.FromUtf8(reportJson);

        run.Status = status;
        run.CompletedAtUtc = completedAt;
        run.Cursor = nextCursor;
        run.FailureCode = status == CognitiveMemoryRunStatus.Blocked ? "RejectedConsolidationCandidates" : string.Empty;
        run.FailureMessage = status == CognitiveMemoryRunStatus.Blocked
            ? "One or more generated consolidation candidates were rejected; cursor was not advanced."
            : string.Empty;
        consolidationRun.Status = status;
        consolidationRun.SourceItemsScanned = sourceItems.Count;
        consolidationRun.CandidatesCreated = createdCandidateIds.Count;
        consolidationRun.MutationCommandsSubmitted = mutationCommandsSubmitted;
        consolidationRun.ReviewItemsCreated = reviewItemsCreated;
        consolidationRun.ProjectionInvalidations = projectionInvalidations;
        consolidationRun.NextCursor = nextCursor;
        consolidationRun.OutputHash = reportHash.Value;
        consolidationRun.CompletedAtUtc = completedAt;
        consolidationRun.FailureCode = run.FailureCode;
        consolidationRun.FailureMessage = run.FailureMessage;
        dbContext.Add(new CognitiveMemoryConsolidationReportRecord
        {
            Id = Guid.NewGuid(),
            RunId = consolidationRun.Id,
            ProjectId = request.ProjectId,
            ReportHash = reportHash.Value,
            ReportJson = reportJson,
            CreatedAtUtc = completedAt
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return new CognitiveMemoryConsolidationRunResult(
            new CognitiveMemoryConsolidationRunId(consolidationRun.Id),
            status,
            sourceItems.Count,
            createdCandidateIds.Count,
            mutationCommandsSubmitted,
            reviewItemsCreated,
            projectionInvalidations,
            string.IsNullOrWhiteSpace(nextCursor) ? null : nextCursor,
            reportHash.Value,
            warnings);
    }

    private static async Task<int> BackfillMissingReviewItemsAsync(
        AppDbContext dbContext,
        CognitiveMemoryConsolidationRunRequest request,
        CognitiveMemoryConsolidationBudget budget,
        Guid consolidationRunId,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken)
    {
        if (budget.ReviewItemLimit == 0)
        {
            return 0;
        }

        var candidates = (await dbContext.Set<CognitiveMemoryConsolidationCandidateRecord>()
            .Where(candidate =>
                candidate.ProjectId == request.ProjectId &&
                candidate.Status == CognitiveMemoryConsolidationCandidateStatus.ReviewRequired &&
                candidate.ReviewItemId == null)
            .ToListAsync(cancellationToken))
            .OrderBy(candidate => candidate.CreatedAtUtc)
            .Take(budget.ReviewItemLimit)
            .ToList();
        if (candidates.Count == 0)
        {
            return 0;
        }

        var sourceItemIds = candidates
            .Where(candidate => candidate.SourceItemId is not null)
            .Select(candidate => candidate.SourceItemId!.Value)
            .Distinct()
            .ToArray();
        var sourceItemsById = sourceItemIds.Length == 0
            ? new Dictionary<Guid, CognitiveMemorySourceItemRecord>()
            : await dbContext.Set<CognitiveMemorySourceItemRecord>()
                .AsNoTracking()
                .Where(sourceItem => sourceItemIds.Contains(sourceItem.Id))
                .ToDictionaryAsync(sourceItem => sourceItem.Id, cancellationToken);

        foreach (var candidate in candidates)
        {
            var reviewItemId = Guid.NewGuid();
            var sourceItem = candidate.SourceItemId is { } sourceItemId &&
                             sourceItemsById.TryGetValue(sourceItemId, out var resolvedSourceItem)
                ? resolvedSourceItem
                : null;
            dbContext.Add(new CognitiveMemoryReviewItemRecord
            {
                Id = reviewItemId,
                ProjectId = request.ProjectId,
                ReviewKind = ResolveReviewKind(candidate.CandidateKind),
                Status = CognitiveMemoryReviewStatus.Pending,
                SubjectKind = CognitiveMemoryReviewSubjectKind.Run,
                SubjectId = consolidationRunId,
                RiskLevel = sourceItem is null ? CognitiveMemoryRiskLevel.Low : ResolveRiskLevel(sourceItem.AccessLevel, sourceItem.RedactionState),
                ReasonCode = "ConsolidationCandidateReviewBackfill",
                ReasonText = "Previously generated consolidation candidate requires review before authoritative memory changes.",
                SourceEvidenceCount = candidate.EvidenceAnchorId is null ? 0 : 1,
                CreatedAtUtc = createdAtUtc,
                ConcurrencyToken = Guid.NewGuid()
            });
            candidate.ReviewItemId = reviewItemId;
            candidate.ConcurrencyToken = Guid.NewGuid();
        }

        return candidates.Count;
    }

    private static void ValidateRequest(CognitiveMemoryConsolidationRunRequest request)
    {
        ArgumentNullException.ThrowIfNull(request.Profile);
        ArgumentNullException.ThrowIfNull(request.PolicyContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Profile.Name);
        if (request.Profile.MaxItems <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Consolidation profile max items must be positive.");
        }

        if (request.ProjectId is { } projectId &&
            request.PolicyContext.ProjectId is { } policyProjectId &&
            projectId != policyProjectId)
        {
            throw new InvalidOperationException("Consolidation project id must match the policy context project id.");
        }
    }

    private static async Task<CognitiveMemoryConsolidationRunResult> CreateReplayResultAsync(
        AppDbContext dbContext,
        CognitiveMemoryConsolidationRunRecord existingRun,
        CancellationToken cancellationToken)
    {
        var reportHash = await dbContext.Set<CognitiveMemoryConsolidationReportRecord>()
            .AsNoTracking()
            .Where(report => report.RunId == existingRun.Id)
            .Select(report => report.ReportHash)
            .FirstOrDefaultAsync(cancellationToken);

        return new CognitiveMemoryConsolidationRunResult(
            new CognitiveMemoryConsolidationRunId(existingRun.Id),
            existingRun.Status,
            existingRun.SourceItemsScanned,
            existingRun.CandidatesCreated,
            existingRun.MutationCommandsSubmitted,
            existingRun.ReviewItemsCreated,
            existingRun.ProjectionInvalidations,
            string.IsNullOrWhiteSpace(existingRun.NextCursor) ? null : existingRun.NextCursor,
            string.IsNullOrWhiteSpace(reportHash) ? null : reportHash,
            ["Idempotent replay returned the original consolidation run without creating additional candidates."]);
    }

    private static async Task<CognitiveMemoryConsolidationRunRecord?> FindActiveLeaseAsync(
        AppDbContext dbContext,
        CognitiveMemoryConsolidationRunRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var activeRuns = await dbContext.Set<CognitiveMemoryConsolidationRunRecord>()
            .AsNoTracking()
            .Where(run => run.ProjectId == request.ProjectId &&
                          run.Mode == request.Mode &&
                          run.Status == CognitiveMemoryRunStatus.Running)
            .ToListAsync(cancellationToken);
        return activeRuns.FirstOrDefault(run => run.LeaseExpiresAtUtc > now);
    }

    private static async Task<CognitiveMemoryConsolidationRunResult> CreateBlockedLeaseRunAsync(
        AppDbContext dbContext,
        CognitiveMemoryConsolidationRunId runId,
        CognitiveMemoryConsolidationRunRequest request,
        string storedIdempotencyKey,
        CognitiveMemoryConsolidationRunRecord activeLease,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var inputHash = CreateInputHash(request, request.Budget ?? CognitiveMemoryConsolidationBudget.Default);
        var message = $"Active consolidation lease '{activeLease.Id:D}' is valid until {activeLease.LeaseExpiresAtUtc:O}.";
        dbContext.Add(new CognitiveMemoryRunRecord
        {
            Id = runId.Value,
            ProjectId = request.ProjectId,
            RunKind = CognitiveMemoryRunKind.Consolidation,
            Status = CognitiveMemoryRunStatus.Blocked,
            OperationMode = CognitiveMemoryOperationMode.Consolidate,
            IdempotencyKey = storedIdempotencyKey,
            InputHash = inputHash.Value,
            AlgorithmVersion = AlgorithmVersion,
            Cursor = request.Cursor ?? string.Empty,
            StartedAtUtc = now,
            CompletedAtUtc = now,
            FailureCode = "ActiveLease",
            FailureMessage = message,
            ConcurrencyToken = Guid.NewGuid()
        });
        dbContext.Add(new CognitiveMemoryConsolidationRunRecord
        {
            Id = runId.Value,
            ProjectId = request.ProjectId,
            Mode = request.Mode,
            TriggerKind = request.TriggerKind,
            Status = CognitiveMemoryRunStatus.Blocked,
            ProfileName = request.Profile.Name.Trim(),
            IdempotencyKey = storedIdempotencyKey,
            InputHash = inputHash.Value,
            AlgorithmVersion = AlgorithmVersion,
            Cursor = request.Cursor ?? string.Empty,
            LeaseOwnerId = request.PolicyContext.ActorId.Trim(),
            LeaseExpiresAtUtc = now,
            FailureCode = "ActiveLease",
            FailureMessage = message,
            StartedAtUtc = now,
            CompletedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return new CognitiveMemoryConsolidationRunResult(
            runId,
            CognitiveMemoryRunStatus.Blocked,
            0,
            0,
            0,
            0,
            0,
            null,
            null,
            [message]);
    }

    private static async Task<IReadOnlyList<SourceItemSnapshot>> LoadSourceItemsAsync(
        AppDbContext dbContext,
        CognitiveMemoryConsolidationRunRequest request,
        CognitiveMemoryConsolidationBudget budget,
        CancellationToken cancellationToken)
    {
        var take = Math.Min(budget.SourceItemLimit, request.Profile.MaxItems);
        var query = dbContext.Set<CognitiveMemorySourceItemRecord>()
            .AsNoTracking();
        if (request.ProjectId is { } projectId)
        {
            query = query.Where(item => item.ProjectId == projectId);
        }

        query = query.Where(item =>
            (item.AccessLevel <= request.PolicyContext.AccessLevel ||
             item.AccessLevel == CognitiveMemoryAccessLevel.Restricted && request.PolicyContext.AllowRestrictedContent) &&
            !(item.SourceSystem == WorkbenchProjectStructureSourceSystem && item.SourceItemType == ProjectLinkSourceItemType) &&
            !(item.SourceSystem == WorkbenchProjectStructureSourceSystem &&
              item.SourceItemType == ProjectNodeSourceItemType &&
              item.ContentText.Contains(ProjectFileNodeMarker)));
        var processedCandidates = dbContext.Set<CognitiveMemoryConsolidationCandidateRecord>()
            .AsNoTracking();
        query = query.Where(item => !processedCandidates.Any(candidate =>
            candidate.ProjectId == item.ProjectId &&
            candidate.SourceItemId == item.Id &&
            candidate.SourceContentHash == item.ContentHash &&
            candidate.AlgorithmVersion == AlgorithmVersion));

        return await query
            .OrderBy(item => item.SourceSystem == WorkbenchProjectStructureSourceSystem ? 0 : 1)
            .ThenBy(item => item.SourceItemType == ProjectNodeSourceItemType ? 0 : 1)
            .ThenBy(item => item.ContentText.Length)
            .ThenBy(item => item.Id)
            .Take(take)
            .Select(item => new SourceItemSnapshot(
                item.Id,
                item.ProjectId,
                item.SourceManifestId,
                item.SourceSystem,
                item.SourceItemKey,
                item.SourceItemType,
                item.Title,
                item.ContentText,
                item.Locator,
                item.ContentHash,
                item.RedactionState,
                item.AccessLevel,
                item.ObservedAtUtc))
            .ToListAsync(cancellationToken);
    }

    private static async Task<bool> CandidateAlreadyProcessedAsync(
        AppDbContext dbContext,
        Guid? projectId,
        SourceItemSnapshot sourceItem,
        CognitiveMemoryConsolidationCandidateKind candidateKind,
        CancellationToken cancellationToken)
        => await dbContext.Set<CognitiveMemoryConsolidationCandidateRecord>()
            .AsNoTracking()
            .AnyAsync(candidate =>
                candidate.ProjectId == projectId &&
                candidate.SourceItemId == sourceItem.Id &&
                candidate.CandidateKind == candidateKind &&
                candidate.SourceContentHash == sourceItem.ContentHash &&
                candidate.AlgorithmVersion == AlgorithmVersion,
                cancellationToken);

    private static async Task<List<Guid>> LoadEvidenceAnchorIdsAsync(
        AppDbContext dbContext,
        Guid sourceItemId,
        CancellationToken cancellationToken)
        => await dbContext.Set<CognitiveMemoryEvidenceAnchorRecord>()
            .AsNoTracking()
            .Where(anchor => anchor.SourceItemId == sourceItemId)
            .OrderBy(anchor => anchor.Id)
            .Take(MaxEvidenceAnchorsPerCandidate)
            .Select(anchor => anchor.Id)
            .ToListAsync(cancellationToken);

    private async Task<CognitiveMemoryScoreEvaluationTrace> EvaluateCandidateAsync(
        AppDbContext dbContext,
        Guid runId,
        SourceItemSnapshot sourceItem,
        IReadOnlyList<Guid> evidenceAnchorIds,
        CognitiveMemoryConsolidationCandidateKind candidateKind,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var evidenceRefs = BuildScoreEvidenceRefs(sourceItem, evidenceAnchorIds, now);
        var components = new List<CognitiveMemoryScoreComponent>
        {
            Component(CognitiveMemoryScoreDimensionKind.SourceSufficiency, evidenceAnchorIds.Count > 0 ? 1 : 0, 1, evidenceRefs),
            Component(CognitiveMemoryScoreDimensionKind.EvidenceStrength, Math.Clamp(evidenceAnchorIds.Count / 2d, 0, 1), 1, evidenceRefs),
            Component(CognitiveMemoryScoreDimensionKind.SourceQuality, ResolveSourceQuality(sourceItem), 1, evidenceRefs),
            Component(CognitiveMemoryScoreDimensionKind.RiskImpact, ResolveRiskImpact(sourceItem, candidateKind), 1, evidenceRefs),
            Component(CognitiveMemoryScoreDimensionKind.RedactionPressure, ResolveRedactionPressure(sourceItem), 1, evidenceRefs),
            Component(CognitiveMemoryScoreDimensionKind.ContradictionPressure, candidateKind == CognitiveMemoryConsolidationCandidateKind.Contradiction ? 0.85 : 0.05, 1, evidenceRefs),
            Component(CognitiveMemoryScoreDimensionKind.TemporalRecency, 0.7, 0.5, evidenceRefs)
        };
        if (candidateKind == CognitiveMemoryConsolidationCandidateKind.Procedure)
        {
            components.Add(Component(CognitiveMemoryScoreDimensionKind.ProcedureMaturity, 0.25, 0.7, evidenceRefs));
        }

        var vector = new CognitiveMemoryScoreVectorSnapshot(
            CognitiveMemoryScoreSpaceKind.ConsolidationCandidate,
            CognitiveMemoryScoreSpaceRegistry.CurrentSchemaVersion,
            CognitiveMemoryScoreSpaceRegistry.CurrentNormalizationProfile,
            components,
            CognitiveMemoryScoreSpaceRegistry.CurrentAlgorithmVersion,
            now,
            CognitiveMemoryHash.FromUtf8($"{runId:D}:{sourceItem.Id:D}:{sourceItem.ContentHash}:{candidateKind}"));
        var trace = await scoreGeometryDriver.EvaluateAsync(
            new CognitiveMemoryScoreEvaluationRequest(
                sourceItem.ProjectId,
                CognitiveMemoryScoreOwnerKind.Run,
                runId,
                CognitiveMemoryScoreSpaceKind.ConsolidationCandidate,
                CognitiveMemoryScoreSpaceRegistry.CurrentSchemaVersion,
                [vector],
                CandidateShapes),
            cancellationToken);
        await CognitiveMemoryScoreTracePersistence.AddIfMissingAsync(dbContext, trace, now, cancellationToken);
        return trace;
    }

    private static IReadOnlyList<CognitiveMemoryScoreEvidenceRef> BuildScoreEvidenceRefs(
        SourceItemSnapshot sourceItem,
        IReadOnlyList<Guid> evidenceAnchorIds,
        DateTimeOffset now)
    {
        var refs = new List<CognitiveMemoryScoreEvidenceRef>(evidenceAnchorIds.Count + 1)
        {
            new(CognitiveMemoryScoreEvidenceKind.SourceItem, sourceItem.Id, 1, now)
        };
        foreach (var evidenceAnchorId in evidenceAnchorIds)
        {
            refs.Add(new CognitiveMemoryScoreEvidenceRef(
                CognitiveMemoryScoreEvidenceKind.EvidenceAnchor,
                evidenceAnchorId,
                1,
                now));
        }

        return refs;
    }

    private static IReadOnlyList<CognitiveMemoryScoreShapeSnapshot> BuildCandidateShapes()
    {
        var schema = CognitiveMemoryScoreSpaceRegistry.CurrentSchemaVersion;
        var algorithm = CognitiveMemoryScoreSpaceRegistry.CurrentAlgorithmVersion;
        return
        [
            Shape(CognitiveMemoryScoreProjectionBucket.NeedsReview, "Consolidation candidate lacks source evidence and cannot advance cursor.",
            [
                Lower(CognitiveMemoryScoreDimensionKind.SourceSufficiency, 0.5)
            ]),
            Shape(CognitiveMemoryScoreProjectionBucket.NeedsReview, "Consolidation candidate has risk or redaction pressure and needs review.",
            [
                Higher(CognitiveMemoryScoreDimensionKind.RiskImpact, 0.75)
            ]),
            Shape(CognitiveMemoryScoreProjectionBucket.NeedsReview, "Consolidation candidate indicates contradiction pressure.",
            [
                Higher(CognitiveMemoryScoreDimensionKind.ContradictionPressure, 0.75)
            ])
        ];

        CognitiveMemoryScoreShapeSnapshot Shape(
            CognitiveMemoryScoreProjectionBucket bucket,
            string explanation,
            IReadOnlyList<CognitiveMemoryScoreShapeComponent> components)
            => new(
                CognitiveMemoryScoreShapeKind.ThresholdEnvelope,
                CognitiveMemoryScoreSpaceKind.ConsolidationCandidate,
                schema,
                components,
                radius: null,
                bucket,
                explanation,
                [],
                algorithm);
    }

    private static CognitiveMemoryMutationCommand CreateMutationCommand(
        CognitiveMemoryConsolidationRunRequest request,
        SourceItemSnapshot sourceItem,
        IReadOnlyList<Guid> evidenceAnchorIds,
        CognitiveMemoryConsolidationCandidateKind candidateKind,
        CognitiveMemoryConsolidationCandidatePayload payload)
    {
        var payloadJson = JsonSerializer.Serialize(
            payload,
            CognitiveMemoryJsonSerializerContext.Default.CognitiveMemoryConsolidationCandidatePayload);
        return new CognitiveMemoryMutationCommand(
            request.ProjectId,
            CognitiveMemoryMutationCommandKind.RecordEvidence,
            CognitiveMemoryActorKind.System,
            request.PolicyContext.ActorId,
            new CognitiveMemoryIdempotencyKey(CreateCandidateIdempotencyKey(sourceItem, candidateKind)),
            [],
            [],
            evidenceAnchorIds,
            payloadJson,
            ExpectedVersionToken: null,
            RequiresHumanReview: request.Profile.CreateHumanReviewItems,
            new Dictionary<string, string>
            {
                ["consolidationMode"] = request.Mode.ToString(),
                ["triggerKind"] = request.TriggerKind.ToString(),
                ["algorithmVersion"] = AlgorithmVersion
            });
    }

    private static CognitiveMemoryConsolidationCandidatePayload CreatePayload(
        SourceItemSnapshot sourceItem,
        Guid? evidenceAnchorId,
        Guid? mutationCommandId,
        Guid? reviewItemId,
        CognitiveMemoryConsolidationCandidateKind candidateKind,
        CognitiveMemoryConsolidationBudget budget)
        => new(
            candidateKind,
            sourceItem.Id,
            evidenceAnchorId,
            mutationCommandId,
            reviewItemId,
            sourceItem.SourceSystem,
            sourceItem.SourceItemType,
            sourceItem.Title,
            TrimForPayload(sourceItem.ContentText, budget.MaxSourceCharacters),
            sourceItem.ContentHash,
            $"Consolidation classified source item '{sourceItem.SourceItemKey}' as {candidateKind}.");

    private static async Task<int> MarkProjectionInvalidationsAsync(
        AppDbContext dbContext,
        IReadOnlyList<Guid> sourceItemIds,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (sourceItemIds.Count == 0)
        {
            return 0;
        }

        var memoryRecordIds = await dbContext.Set<CognitiveMemorySourceLinkRecord>()
            .AsNoTracking()
            .Where(link => sourceItemIds.Contains(link.SourceItemId))
            .Select(link => link.MemoryRecordId)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (memoryRecordIds.Count == 0)
        {
            return 0;
        }

        return await dbContext.Set<CognitiveMemoryProjectionRecord>()
            .Where(projection => memoryRecordIds.Contains(projection.MemoryRecordId))
            .ExecuteUpdateAsync(
                updates => updates
                    .SetProperty(projection => projection.RebuildRequired, true)
                    .SetProperty(projection => projection.Status, CognitiveMemoryProjectionStatus.RebuildRequired)
                    .SetProperty(projection => projection.StaleReason, CognitiveMemoryProjectionStaleReason.SourceHashChanged)
                    .SetProperty(projection => projection.UpdatedAtUtc, now)
                    .SetProperty(projection => projection.FailureCode, string.Empty)
                    .SetProperty(projection => projection.FailureMessage, string.Empty),
                cancellationToken);
    }

    private static async Task UpdateCursorAsync(
        AppDbContext dbContext,
        CognitiveMemoryConsolidationRunRequest request,
        Guid runId,
        string nextCursor,
        IReadOnlyList<SourceItemSnapshot> sourceItems,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var sourceHash = CreateSourceHash(sourceItems);
        var cursor = await dbContext.Set<CognitiveMemoryConsolidationCursorRecord>()
            .FirstOrDefaultAsync(
                item => item.ProjectId == request.ProjectId &&
                        item.Mode == request.Mode &&
                        item.SourceSystem == ConsolidationCursorSource,
                cancellationToken);
        if (cursor is null)
        {
            cursor = new CognitiveMemoryConsolidationCursorRecord
            {
                Id = Guid.NewGuid(),
                ProjectId = request.ProjectId,
                Mode = request.Mode,
                SourceSystem = ConsolidationCursorSource,
                ConcurrencyToken = Guid.NewGuid()
            };
            dbContext.Add(cursor);
        }

        cursor.Cursor = nextCursor;
        cursor.LastSourceHash = sourceHash.Value;
        cursor.LastRunId = runId;
        cursor.UpdatedAtUtc = now;
    }

    private static string ResolveNextCursor(
        CognitiveMemoryConsolidationRunRequest request,
        IReadOnlyList<SourceItemSnapshot> sourceItems)
        => sourceItems.LastOrDefault() is { } lastSource
            ? lastSource.Id.ToString("D")
            : request.Cursor ?? string.Empty;

    private static CognitiveMemoryConsolidationCandidateKind ResolveCandidateKind(
        CognitiveMemoryConsolidationRunRequest request,
        SourceItemSnapshot sourceItem)
    {
        if (request.Mode == CognitiveMemoryConsolidationMode.ContradictionReview ||
            ContainsOrdinal(sourceItem.ContentText, "contradict") ||
            ContainsOrdinal(sourceItem.ContentText, "conflict"))
        {
            return CognitiveMemoryConsolidationCandidateKind.Contradiction;
        }

        if (request.Profile.ExtractProcedures &&
            (ContainsOrdinal(sourceItem.SourceItemType, "step") ||
             ContainsOrdinal(sourceItem.ContentText, "procedure") ||
             ContainsOrdinal(sourceItem.ContentText, "runbook")))
        {
            return CognitiveMemoryConsolidationCandidateKind.Procedure;
        }

        if (ContainsOrdinal(sourceItem.SourceItemType, "decision") ||
            ContainsOrdinal(sourceItem.ContentText, "decision") ||
            ContainsOrdinal(sourceItem.ContentText, "approved"))
        {
            return CognitiveMemoryConsolidationCandidateKind.Decision;
        }

        if (ContainsOrdinal(sourceItem.SourceSystem, "ProcessRuntime") ||
            ContainsOrdinal(sourceItem.SourceSystem, "WorkflowRuntime") ||
            ContainsOrdinal(sourceItem.SourceItemType, "run"))
        {
            return CognitiveMemoryConsolidationCandidateKind.Episode;
        }

        return CognitiveMemoryConsolidationCandidateKind.Reflection;
    }

    private static CognitiveMemoryReviewKind ResolveReviewKind(CognitiveMemoryConsolidationCandidateKind candidateKind)
        => candidateKind switch
        {
            CognitiveMemoryConsolidationCandidateKind.Contradiction => CognitiveMemoryReviewKind.Contradiction,
            CognitiveMemoryConsolidationCandidateKind.ProjectionInvalidation => CognitiveMemoryReviewKind.ProjectionHealth,
            _ => CognitiveMemoryReviewKind.GeneratedMemory
        };

    private static CognitiveMemoryConsolidationCandidateStatus ResolveCandidateStatus(CognitiveMemoryMutationResult mutationResult)
    {
        if (!mutationResult.Accepted)
        {
            return CognitiveMemoryConsolidationCandidateStatus.Rejected;
        }

        return mutationResult.ReviewRequired
            ? CognitiveMemoryConsolidationCandidateStatus.ReviewRequired
            : CognitiveMemoryConsolidationCandidateStatus.MutationSubmitted;
    }

    private static CognitiveMemoryRiskLevel ResolveRiskLevel(SourceItemSnapshot sourceItem)
        => ResolveRiskLevel(sourceItem.AccessLevel, sourceItem.RedactionState);

    private static CognitiveMemoryRiskLevel ResolveRiskLevel(
        CognitiveMemoryAccessLevel accessLevel,
        CognitiveMemoryRedactionState redactionState)
        => accessLevel == CognitiveMemoryAccessLevel.Restricted ||
           redactionState is CognitiveMemoryRedactionState.Redacted or CognitiveMemoryRedactionState.Restricted
            ? CognitiveMemoryRiskLevel.High
            : CognitiveMemoryRiskLevel.Medium;

    private static double ResolveRiskImpact(
        SourceItemSnapshot sourceItem,
        CognitiveMemoryConsolidationCandidateKind candidateKind)
    {
        if (sourceItem.AccessLevel == CognitiveMemoryAccessLevel.Restricted ||
            sourceItem.RedactionState is CognitiveMemoryRedactionState.Redacted or CognitiveMemoryRedactionState.Restricted)
        {
            return 0.9;
        }

        return candidateKind == CognitiveMemoryConsolidationCandidateKind.Contradiction ? 0.7 : 0.3;
    }

    private static double ResolveRedactionPressure(SourceItemSnapshot sourceItem)
        => sourceItem.RedactionState switch
        {
            CognitiveMemoryRedactionState.Restricted => 0.95,
            CognitiveMemoryRedactionState.Redacted => 0.75,
            CognitiveMemoryRedactionState.Unclassified => 0.35,
            _ => 0.05
        };

    private static double ResolveSourceQuality(SourceItemSnapshot sourceItem)
        => sourceItem.RedactionState is CognitiveMemoryRedactionState.Safe ? 0.85 : 0.55;

    private static string CreateCandidateReason(
        CognitiveMemoryConsolidationCandidateKind candidateKind,
        SourceItemSnapshot sourceItem,
        CognitiveMemoryMutationResult mutationResult)
    {
        if (!mutationResult.Accepted)
        {
            return mutationResult.ReviewReason ?? "Mutation authority rejected the generated consolidation candidate.";
        }

        return mutationResult.ReviewRequired
            ? $"Generated {candidateKind} candidate from source '{sourceItem.SourceItemKey}' is pending human review."
            : $"Generated {candidateKind} candidate from source '{sourceItem.SourceItemKey}' was accepted for downstream handler processing.";
    }

    private static CognitiveMemoryScoreComponent Component(
        CognitiveMemoryScoreDimensionKind dimensionKind,
        double value,
        double confidence,
        IReadOnlyList<CognitiveMemoryScoreEvidenceRef> evidenceRefs)
        => new(
            dimensionKind,
            Math.Clamp(value, 0, 1),
            Math.Clamp(confidence, 0, 1),
            evidenceRefs);

    private static CognitiveMemoryScoreShapeComponent Higher(
        CognitiveMemoryScoreDimensionKind dimensionKind,
        double lowerBound)
        => new(dimensionKind, center: lowerBound, lowerBound, upperBound: null, weight: 1);

    private static CognitiveMemoryScoreShapeComponent Lower(
        CognitiveMemoryScoreDimensionKind dimensionKind,
        double upperBound)
        => new(dimensionKind, center: upperBound, lowerBound: null, upperBound, weight: 1);

    private static CognitiveMemoryHash CreateInputHash(
        CognitiveMemoryConsolidationRunRequest request,
        CognitiveMemoryConsolidationBudget budget)
    {
        var builder = new StringBuilder(capacity: 256);
        AppendHashSegment(builder, request.ProjectId?.ToString("D") ?? "global");
        AppendHashSegment(builder, request.Mode);
        AppendHashSegment(builder, request.TriggerKind);
        AppendHashSegment(builder, request.Profile.Name);
        AppendHashSegment(builder, request.Profile.ProcessSourceItems);
        AppendHashSegment(builder, request.Profile.DetectContradictions);
        AppendHashSegment(builder, request.Profile.ExtractProcedures);
        AppendHashSegment(builder, request.Profile.RebuildProjections);
        AppendHashSegment(builder, request.Profile.CreateHumanReviewItems);
        AppendHashSegment(builder, request.Profile.MaxItems);
        AppendHashSegment(builder, budget.SourceItemLimit);
        AppendHashSegment(builder, budget.CandidateLimit);
        AppendHashSegment(builder, budget.ReviewItemLimit);
        AppendHashSegment(builder, request.Cursor ?? string.Empty);
        AppendHashSegment(builder, string.Empty);
        AppendOptionsHashSegment(builder, request.Options ?? EmptyOptions);
        return CognitiveMemoryHash.FromUtf8(builder.ToString());
    }

    private static CognitiveMemoryHash CreateSourceHash(IReadOnlyList<SourceItemSnapshot> sourceItems)
    {
        var builder = new StringBuilder(sourceItems.Count * 100);
        foreach (var sourceItem in sourceItems)
        {
            AppendHashSegment(builder, sourceItem.Id.ToString("D"));
            builder.Append(':');
            builder.Append(sourceItem.ContentHash);
        }

        return CognitiveMemoryHash.FromUtf8(builder.ToString());
    }

    private static void AppendOptionsHashSegment(StringBuilder builder, IReadOnlyDictionary<string, string> options)
    {
        var separatorLength = builder.Length;
        foreach (var option in options.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (builder.Length != separatorLength)
            {
                builder.Append(';');
            }

            builder.Append(option.Key);
            builder.Append('=');
            builder.Append(option.Value);
        }
    }

    private static void AppendHashSegment<T>(StringBuilder builder, T value)
    {
        if (builder.Length > 0)
        {
            builder.Append('|');
        }

        builder.Append(value);
    }

    private static string CreateStoredIdempotencyKey(CognitiveMemoryIdempotencyKey idempotencyKey)
    {
        var raw = idempotencyKey.Value.Trim();
        return raw.Length <= 220
            ? $"consolidation:{raw}"
            : $"consolidation:{CognitiveMemoryHash.FromUtf8(raw).Value}";
    }

    private static string CreateCandidateIdempotencyKey(
        SourceItemSnapshot sourceItem,
        CognitiveMemoryConsolidationCandidateKind candidateKind)
        => $"consolidation-candidate:{sourceItem.Id:D}:{candidateKind}:{sourceItem.ContentHash[..16]}";

    private static string TrimForPayload(string value, int maxCharacters)
        => value.Length <= maxCharacters ? value : value[..maxCharacters];

    private static bool ContainsOrdinal(string value, string expected)
        => value.Contains(expected, StringComparison.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<string, string> EmptyOptions = new Dictionary<string, string>(0, StringComparer.Ordinal);

    private sealed record SourceItemSnapshot(
        Guid Id,
        Guid? ProjectId,
        Guid SourceManifestId,
        string SourceSystem,
        string SourceItemKey,
        string SourceItemType,
        string Title,
        string ContentText,
        string? Locator,
        string ContentHash,
        CognitiveMemoryRedactionState RedactionState,
        CognitiveMemoryAccessLevel AccessLevel,
        DateTimeOffset ObservedAtUtc);
}

using System.Text;
using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemoryTemporalReplayService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ICognitiveMemoryScoreGeometryDriver scoreGeometryDriver,
    IClock clock,
    ILogger<CognitiveMemoryTemporalReplayService> logger) : ICognitiveMemoryTemporalEpisodeService, ICognitiveMemoryReplayScheduler
{
    private const string AlgorithmVersion = "temporal-replay-v1";
    private const int MaxReplayTriggers = 32;
    private static readonly IReadOnlyList<CognitiveMemoryScoreShapeSnapshot> ReplayPriorityShapes = BuildReplayPriorityShapes();

    public async ValueTask<CognitiveMemoryTemporalEpisodeRecord> CreateEpisodeAsync(
        CognitiveMemoryTemporalEpisodeCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateEpisodeRequest(request);
        cancellationToken.ThrowIfCancellationRequested();

        var now = clock.GetUtcNow();
        var episode = new CognitiveMemoryTemporalEpisodeRecord
        {
            Id = CognitiveMemoryTemporalEpisodeId.New().Value,
            ProjectId = request.ProjectId,
            EpisodeKind = request.EpisodeKind,
            Goal = CognitiveMemoryGuard.EnsureText(request.Goal, nameof(request.Goal)),
            ExpectedOutcome = request.ExpectedOutcome.Trim(),
            ActualOutcome = request.ActualOutcome.Trim(),
            OutcomeSummary = CreateOutcomeSummary(request.ExpectedOutcome, request.ActualOutcome),
            StartedAtUtc = request.StartedAtUtc,
            EndedAtUtc = request.EndedAtUtc,
            LinkCount = request.Links?.Count ?? 0,
            AlgorithmVersion = AlgorithmVersion,
            MetadataJson = SerializeMetadata(request.Metadata),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.Add(episode);
        foreach (var link in request.Links ?? [])
        {
            dbContext.Add(CreateEpisodeLink(episode, link, now));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return episode;
    }

    public async ValueTask<CognitiveMemoryEpisodeStepRecord> AppendStepAsync(
        CognitiveMemoryEpisodeStepAppendRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateStepRequest(request);
        cancellationToken.ThrowIfCancellationRequested();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var episode = await dbContext.Set<CognitiveMemoryTemporalEpisodeRecord>()
            .FirstOrDefaultAsync(item => item.Id == request.EpisodeId.Value, cancellationToken)
            ?? throw new InvalidOperationException($"Temporal episode '{request.EpisodeId}' does not exist.");

        var lastSequence = await dbContext.Set<CognitiveMemoryEpisodeStepRecord>()
            .Where(step => step.EpisodeId == episode.Id)
            .OrderByDescending(step => step.SequenceIndex)
            .Select(step => (int?)step.SequenceIndex)
            .FirstOrDefaultAsync(cancellationToken) ?? 0;
        var sequenceIndex = request.SequenceIndex ?? lastSequence + 1;
        if (sequenceIndex != lastSequence + 1)
        {
            throw new InvalidOperationException($"Episode '{episode.Id:D}' expected next step sequence '{lastSequence + 1}', but received '{sequenceIndex}'.");
        }

        var inputEvidenceAnchorIds = DistinctEvidenceAnchorIds(request.InputEvidenceAnchorIds);
        var outputEvidenceAnchorIds = DistinctEvidenceAnchorIds(request.OutputEvidenceAnchorIds);
        await EnsureEvidenceAnchorsExistAsync(dbContext, episode.ProjectId, inputEvidenceAnchorIds, cancellationToken);
        await EnsureEvidenceAnchorsExistAsync(dbContext, episode.ProjectId, outputEvidenceAnchorIds, cancellationToken);

        var now = clock.GetUtcNow();
        var step = new CognitiveMemoryEpisodeStepRecord
        {
            Id = CognitiveMemoryEpisodeStepId.New().Value,
            EpisodeId = episode.Id,
            ProjectId = episode.ProjectId,
            SequenceIndex = sequenceIndex,
            OccurredAtUtc = request.OccurredAtUtc,
            ActorKind = request.ActorKind,
            ActorId = CognitiveMemoryGuard.EnsureText(request.ActorId, nameof(request.ActorId)),
            ActionKind = request.ActionKind,
            Summary = CognitiveMemoryGuard.EnsureText(request.Summary, nameof(request.Summary)),
            ToolOrPluginKey = request.ToolOrPluginKey.Trim(),
            Succeeded = request.Succeeded,
            ErrorCode = request.ErrorCode.Trim(),
            ErrorSummary = request.ErrorSummary.Trim(),
            MetadataJson = SerializeMetadata(request.Metadata),
            CreatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        dbContext.Add(step);
        AddStepEvidence(dbContext, step, inputEvidenceAnchorIds, CognitiveMemoryEpisodeStepEvidenceRole.Input, now);
        AddStepEvidence(dbContext, step, outputEvidenceAnchorIds, CognitiveMemoryEpisodeStepEvidenceRole.Output, now);

        episode.StepCount++;
        episode.FirstStepAtUtc ??= step.OccurredAtUtc;
        episode.LastStepAtUtc = step.OccurredAtUtc;
        episode.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return step;
    }

    public async ValueTask<CognitiveMemoryEpisodeCausalLinkRecord> AddCausalLinkAsync(
        CognitiveMemoryEpisodeCausalLinkRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateCausalLinkRequest(request);
        cancellationToken.ThrowIfCancellationRequested();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var episode = await dbContext.Set<CognitiveMemoryTemporalEpisodeRecord>()
            .FirstOrDefaultAsync(item => item.Id == request.EpisodeId.Value, cancellationToken)
            ?? throw new InvalidOperationException($"Temporal episode '{request.EpisodeId}' does not exist.");
        await EnsureStepBelongsToEpisodeAsync(dbContext, episode.Id, request.FromStepId, cancellationToken);
        await EnsureStepBelongsToEpisodeAsync(dbContext, episode.Id, request.ToStepId, cancellationToken);

        var now = clock.GetUtcNow();
        var link = new CognitiveMemoryEpisodeCausalLinkRecord
        {
            EpisodeId = episode.Id,
            ProjectId = episode.ProjectId,
            LinkKind = request.LinkKind,
            FromStepId = request.FromStepId?.Value,
            ToStepId = request.ToStepId?.Value,
            EvidenceAnchorId = request.EvidenceAnchorId?.Value,
            ClaimId = request.ClaimId?.Value,
            PredictionErrorId = request.PredictionErrorId?.Value,
            ProcedureSkillId = NormalizeOptional(request.ProcedureSkillId),
            Summary = CognitiveMemoryGuard.EnsureText(request.Summary, nameof(request.Summary)),
            CreatedAtUtc = now
        };
        dbContext.Add(link);
        episode.LinkCount++;
        episode.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return link;
    }

    public async ValueTask<CognitiveMemoryReplayPlanResult> PlanReplayJobsAsync(
        CognitiveMemoryReplayPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        CognitiveMemoryGuard.EnsureNonEmpty(request.ProjectId, nameof(request.ProjectId));
        ValidatePolicyTrace(request.ProjectId, request.PolicyContext);
        cancellationToken.ThrowIfCancellationRequested();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var jobs = new List<CognitiveMemoryReplayJobRecord>(request.Page.Take);
        var errors = await LoadReplayPredictionErrorsAsync(dbContext, request, cancellationToken);
        foreach (var error in errors)
        {
            if (jobs.Count >= request.Page.Take)
            {
                break;
            }

            var enqueueRequest = CreateReplayRequestFromPredictionError(request, error);
            jobs.Add(await EnqueueCoreAsync(dbContext, enqueueRequest, cancellationToken));
        }

        if (jobs.Count < request.Page.Take)
        {
            var signals = await LoadReplaySignalsAsync(dbContext, request, request.Page.Take - jobs.Count, cancellationToken);
            foreach (var signal in signals)
            {
                var enqueueRequest = CreateReplayRequestFromSignal(request, signal);
                jobs.Add(await EnqueueCoreAsync(dbContext, enqueueRequest, cancellationToken));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new CognitiveMemoryReplayPlanResult(jobs);
    }

    public async ValueTask<CognitiveMemoryReplayJobRecord> EnqueueAsync(
        CognitiveMemoryReplayEnqueueRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var job = await EnqueueCoreAsync(dbContext, request, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return job;
    }

    public async ValueTask<CognitiveMemoryReplayOutputRecord> RecordOutputAsync(
        CognitiveMemoryReplayOutputRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.OutputKind == CognitiveMemoryReplayOutputKind.Unknown)
        {
            throw new ArgumentException("Replay output kind must be explicit.", nameof(request));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var job = await dbContext.Set<CognitiveMemoryReplayJobRecord>()
            .FirstOrDefaultAsync(item => item.Id == request.ReplayJobId.Value, cancellationToken)
            ?? throw new InvalidOperationException($"Replay job '{request.ReplayJobId}' does not exist.");

        var payloadHash = CognitiveMemoryHash.FromUtf8(request.PayloadJson);
        var output = new CognitiveMemoryReplayOutputRecord
        {
            ReplayJobId = job.Id,
            ProjectId = job.ProjectId,
            OutputKind = request.OutputKind,
            Status = ResolveOutputStatus(request.OutputKind),
            Summary = CognitiveMemoryGuard.EnsureText(request.Summary, nameof(request.Summary)),
            PayloadHash = payloadHash.Value,
            PayloadJson = request.PayloadJson,
            ReviewItemId = request.ReviewItemId?.Value,
            MutationCommandId = request.MutationCommandId?.Value,
            ProjectionId = NormalizeOptional(request.ProjectionId),
            CreatedAtUtc = clock.GetUtcNow()
        };
        dbContext.Add(output);
        await dbContext.SaveChangesAsync(cancellationToken);
        return output;
    }

    public async ValueTask<CognitiveMemoryReplayWorkerResultValidation> SubmitWorkerResultAsync(
        CognitiveMemoryReplayWorkerResultSubmission submission,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submission);
        cancellationToken.ThrowIfCancellationRequested();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var job = await dbContext.Set<CognitiveMemoryReplayJobRecord>()
            .FirstOrDefaultAsync(item => item.Id == submission.ReplayJobId.Value, cancellationToken)
            ?? throw new InvalidOperationException($"Replay job '{submission.ReplayJobId}' does not exist.");

        var rejectionReason = ValidateWorkerSubmission(job, submission);
        var accepted = rejectionReason is null;
        var now = clock.GetUtcNow();
        var result = new CognitiveMemoryReplayWorkerResultRecord
        {
            ReplayJobId = job.Id,
            ProjectId = job.ProjectId,
            Status = accepted ? CognitiveMemoryReplayWorkerResultStatus.Accepted : CognitiveMemoryReplayWorkerResultStatus.Rejected,
            WorkerId = CognitiveMemoryGuard.EnsureText(submission.WorkerId, nameof(submission.WorkerId)),
            InputHash = CognitiveMemoryGuard.EnsureText(submission.InputHash, nameof(submission.InputHash)),
            OutputHash = CognitiveMemoryGuard.EnsureText(submission.OutputHash, nameof(submission.OutputHash)),
            AlgorithmVersion = CognitiveMemoryGuard.EnsureText(submission.AlgorithmVersion, nameof(submission.AlgorithmVersion)),
            SourceScopeKey = submission.SourceScopeKey.Trim(),
            PolicyProfileId = CognitiveMemoryGuard.EnsureText(submission.PolicyProfileId, nameof(submission.PolicyProfileId)),
            OutputSchema = CognitiveMemoryGuard.EnsureText(submission.OutputSchema, nameof(submission.OutputSchema)),
            ResultStorageReference = CognitiveMemoryGuard.EnsureText(submission.ResultStorageReference, nameof(submission.ResultStorageReference)),
            RejectionReason = rejectionReason ?? string.Empty,
            WarningsJson = JsonSerializer.Serialize(submission.Warnings?.ToArray() ?? [], CognitiveMemoryJsonSerializerContext.Default.StringArray),
            SubmittedAtUtc = now,
            AcceptedAtUtc = accepted ? now : null
        };
        dbContext.Add(result);
        if (!accepted)
        {
            logger.LogWarning(
                "Replay worker result rejected for job {ReplayJobId} from worker {WorkerId}: {RejectionReason}.",
                job.Id,
                submission.WorkerId,
                rejectionReason);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new CognitiveMemoryReplayWorkerResultValidation(result, accepted, rejectionReason);
    }

    private async Task<CognitiveMemoryReplayJobRecord> EnqueueCoreAsync(
        AppDbContext dbContext,
        CognitiveMemoryReplayEnqueueRequest request,
        CancellationToken cancellationToken)
    {
        ValidateEnqueueRequest(request);
        ValidatePolicyTrace(request.ProjectId, request.PolicyContext);
        var triggerSignalIds = await ValidateSignalsAsync(dbContext, request.ProjectId, request.TriggerSignalIds, cancellationToken);
        var predictionErrorIds = await ValidatePredictionErrorsAsync(dbContext, request.ProjectId, request.PredictionErrorIds, cancellationToken);
        var inputHash = CreateReplayInputHash(request);

        var existing = await dbContext.Set<CognitiveMemoryReplayJobRecord>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                job => job.ProjectId == request.ProjectId &&
                       job.JobKind == request.JobKind &&
                       job.InputHash == inputHash.Value,
                cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var now = clock.GetUtcNow();
        var jobId = CognitiveMemoryReplayJobId.New();
        var priorityTrace = await EvaluateReplayPriorityAsync(
            request,
            jobId.Value,
            triggerSignalIds,
            predictionErrorIds,
            now,
            cancellationToken);
        await CognitiveMemoryScoreTracePersistence.AddIfMissingAsync(dbContext, priorityTrace, now, cancellationToken);

        var job = new CognitiveMemoryReplayJobRecord
        {
            Id = jobId.Value,
            ProjectId = request.ProjectId,
            JobKind = request.JobKind,
            State = CognitiveMemoryReplayJobState.Ready,
            Reason = CognitiveMemoryGuard.EnsureText(request.Reason, nameof(request.Reason)),
            PriorityScoreEvaluationTraceId = priorityTrace.Id.Value,
            PriorityBucket = priorityTrace.ScalarProjection?.Bucket ?? CognitiveMemoryScoreProjectionBucket.Unknown,
            DisplayPriorityProjection = priorityTrace.ScalarProjection?.DisplayScore,
            QueuePriority = ToQueuePriority(priorityTrace.ScalarProjection?.DisplayScore),
            InputHash = inputHash.Value,
            ExpectedOutputSchema = CognitiveMemoryGuard.EnsureText(request.ExpectedOutputSchema, nameof(request.ExpectedOutputSchema)),
            AlgorithmVersion = AlgorithmVersion,
            PolicyProfileId = request.PolicyContext.PolicyProfileId.Value,
            SourceScopeKey = NormalizeSourceScope(request),
            ScheduledAtUtc = request.ScheduledAtUtc ?? now,
            MetadataJson = SerializeMetadata(request.Metadata),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        dbContext.Add(job);
        foreach (var target in request.Targets ?? [])
        {
            dbContext.Add(new CognitiveMemoryReplayJobTargetRecord
            {
                ReplayJobId = job.Id,
                ProjectId = job.ProjectId,
                TargetKind = target.TargetKind,
                TargetId = target.TargetId,
                TargetKey = target.TargetKey.Trim(),
                RequiredInputHash = target.RequiredInputHash.Trim(),
                Summary = target.Summary.Trim(),
                CreatedAtUtc = now
            });
        }

        foreach (var signalId in triggerSignalIds)
        {
            dbContext.Add(new CognitiveMemoryReplayJobSignalRecord
            {
                ReplayJobId = job.Id,
                ProjectId = job.ProjectId,
                CognitiveSignalId = signalId,
                CreatedAtUtc = now
            });
        }

        foreach (var predictionErrorId in predictionErrorIds)
        {
            dbContext.Add(new CognitiveMemoryReplayJobPredictionErrorRecord
            {
                ReplayJobId = job.Id,
                ProjectId = job.ProjectId,
                PredictionErrorId = predictionErrorId,
                CreatedAtUtc = now
            });
        }

        return job;
    }

    private async Task<CognitiveMemoryScoreEvaluationTrace> EvaluateReplayPriorityAsync(
        CognitiveMemoryReplayEnqueueRequest request,
        Guid jobId,
        IReadOnlyList<Guid> signalIds,
        IReadOnlyList<Guid> predictionErrorIds,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var evidenceRefs = new List<CognitiveMemoryScoreEvidenceRef>(signalIds.Count + predictionErrorIds.Count);
        foreach (var signalId in signalIds)
        {
            evidenceRefs.Add(new CognitiveMemoryScoreEvidenceRef(CognitiveMemoryScoreEvidenceKind.CognitiveSignal, signalId, 1, now));
        }

        foreach (var predictionErrorId in predictionErrorIds)
        {
            evidenceRefs.Add(new CognitiveMemoryScoreEvidenceRef(CognitiveMemoryScoreEvidenceKind.PredictionError, predictionErrorId, 1, now));
        }

        if (evidenceRefs.Count == 0)
        {
            evidenceRefs.Add(new CognitiveMemoryScoreEvidenceRef(CognitiveMemoryScoreEvidenceKind.ReplayJob, jobId, 1, now));
        }

        var components = BuildReplayPriorityComponents(request, signalIds.Count, predictionErrorIds.Count, evidenceRefs);
        var vector = new CognitiveMemoryScoreVectorSnapshot(
            CognitiveMemoryScoreSpaceKind.ReplayPriority,
            CognitiveMemoryScoreSpaceRegistry.CurrentSchemaVersion,
            CognitiveMemoryScoreSpaceRegistry.CurrentNormalizationProfile,
            components,
            CognitiveMemoryScoreSpaceRegistry.CurrentAlgorithmVersion,
            now,
            CognitiveMemoryHash.FromUtf8($"{request.ProjectId:D}:{request.JobKind}:{request.Reason}:{string.Join(",", signalIds)}:{string.Join(",", predictionErrorIds)}"));
        return await scoreGeometryDriver.EvaluateAsync(
            new CognitiveMemoryScoreEvaluationRequest(
                request.ProjectId,
                CognitiveMemoryScoreOwnerKind.ReplayJob,
                jobId,
                CognitiveMemoryScoreSpaceKind.ReplayPriority,
                CognitiveMemoryScoreSpaceRegistry.CurrentSchemaVersion,
                [vector],
                ReplayPriorityShapes),
            cancellationToken);
    }

    private static IReadOnlyList<CognitiveMemoryScoreComponent> BuildReplayPriorityComponents(
        CognitiveMemoryReplayEnqueueRequest request,
        int signalCount,
        int predictionErrorCount,
        IReadOnlyList<CognitiveMemoryScoreEvidenceRef> evidenceRefs)
    {
        var triggerPressure = Math.Clamp((signalCount + predictionErrorCount) / 3d, 0, 1);
        var risk = request.PolicyContext.RiskLevel switch
        {
            CognitiveMemoryRiskLevel.High => 0.95,
            CognitiveMemoryRiskLevel.Medium => 0.65,
            _ => request.JobKind is CognitiveMemoryReplayJobKind.ResolveContradiction or CognitiveMemoryReplayJobKind.ValidateProcedure ? 0.75 : 0.35
        };
        return
        [
            Component(CognitiveMemoryScoreDimensionKind.PredictionErrorMagnitude, predictionErrorCount > 0 ? Math.Clamp(predictionErrorCount / 2d, 0.35, 1) : 0.15, 1, evidenceRefs),
            Component(CognitiveMemoryScoreDimensionKind.RiskImpact, risk, 1, evidenceRefs),
            Component(CognitiveMemoryScoreDimensionKind.StalenessPressure, request.JobKind == CognitiveMemoryReplayJobKind.RefreshSourceAnchors ? 0.85 : 0.35, 0.8, evidenceRefs),
            Component(CognitiveMemoryScoreDimensionKind.Usefulness, request.JobKind == CognitiveMemoryReplayJobKind.SpacedRecall ? 0.65 : 0.75, 0.8, evidenceRefs),
            Component(CognitiveMemoryScoreDimensionKind.FailureRecurrence, triggerPressure, 0.9, evidenceRefs),
            Component(CognitiveMemoryScoreDimensionKind.ProcedureMaturity, request.JobKind == CognitiveMemoryReplayJobKind.ValidateProcedure ? 0.2 : 0.55, 0.7, evidenceRefs),
            Component(CognitiveMemoryScoreDimensionKind.RegressionFailure, request.JobKind == CognitiveMemoryReplayJobKind.ReplayProbeRegression ? 0.9 : 0.1, 0.8, evidenceRefs),
            Component(CognitiveMemoryScoreDimensionKind.WrongScopePressure, request.JobKind == CognitiveMemoryReplayJobKind.ContextBoundaryDrill ? 0.9 : 0.1, 0.8, evidenceRefs),
            Component(CognitiveMemoryScoreDimensionKind.ContradictionPressure, request.JobKind == CognitiveMemoryReplayJobKind.ResolveContradiction ? 0.9 : 0.1, 0.8, evidenceRefs)
        ];
    }

    private static IReadOnlyList<CognitiveMemoryScoreShapeSnapshot> BuildReplayPriorityShapes()
    {
        var schema = CognitiveMemoryScoreSpaceRegistry.CurrentSchemaVersion;
        var algorithm = CognitiveMemoryScoreSpaceRegistry.CurrentAlgorithmVersion;
        return
        [
            Shape(CognitiveMemoryScoreProjectionBucket.StrongAccept, "High-risk stale replay should be scheduled promptly.",
            [
                Higher(CognitiveMemoryScoreDimensionKind.RiskImpact, 0.75),
                Higher(CognitiveMemoryScoreDimensionKind.StalenessPressure, 0.75)
            ]),
            Shape(CognitiveMemoryScoreProjectionBucket.StrongAccept, "Repeated prediction error or regression failure should be replayed promptly.",
            [
                Higher(CognitiveMemoryScoreDimensionKind.PredictionErrorMagnitude, 0.65),
                Higher(CognitiveMemoryScoreDimensionKind.FailureRecurrence, 0.5)
            ]),
            Shape(CognitiveMemoryScoreProjectionBucket.StrongAccept, "Wrong-scope context boundary replay should be prioritized.",
            [
                Higher(CognitiveMemoryScoreDimensionKind.WrongScopePressure, 0.75)
            ]),
            Shape(CognitiveMemoryScoreProjectionBucket.NeedsReview, "Low source or trigger pressure replay remains queued at review priority.",
            [
                Lower(CognitiveMemoryScoreDimensionKind.PredictionErrorMagnitude, 0.2),
                Lower(CognitiveMemoryScoreDimensionKind.RiskImpact, 0.4)
            ])
        ];

        CognitiveMemoryScoreShapeSnapshot Shape(
            CognitiveMemoryScoreProjectionBucket bucket,
            string explanation,
            IReadOnlyList<CognitiveMemoryScoreShapeComponent> components)
            => new(
                CognitiveMemoryScoreShapeKind.ThresholdEnvelope,
                CognitiveMemoryScoreSpaceKind.ReplayPriority,
                schema,
                components,
                radius: null,
                bucket,
                explanation,
                [],
                algorithm);
    }

    private static CognitiveMemoryScoreShapeComponent Higher(
        CognitiveMemoryScoreDimensionKind dimensionKind,
        double lowerBound)
        => new(dimensionKind, center: lowerBound, lowerBound, upperBound: null, weight: 1);

    private static CognitiveMemoryScoreShapeComponent Lower(
        CognitiveMemoryScoreDimensionKind dimensionKind,
        double upperBound)
        => new(dimensionKind, center: upperBound, lowerBound: null, upperBound, weight: 1);

    private static CognitiveMemoryScoreComponent Component(
        CognitiveMemoryScoreDimensionKind dimensionKind,
        double value,
        double confidence,
        IReadOnlyList<CognitiveMemoryScoreEvidenceRef> evidenceRefs)
        => new(dimensionKind, Math.Clamp(value, 0, 1), Math.Clamp(confidence, 0, 1), evidenceRefs);

    private static async Task<IReadOnlyList<CognitiveMemoryPredictionErrorRecord>> LoadReplayPredictionErrorsAsync(
        AppDbContext dbContext,
        CognitiveMemoryReplayPlanRequest request,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<CognitiveMemoryPredictionErrorRecord>()
            .AsNoTracking()
            .Where(error => error.ProjectId == request.ProjectId);
        if (request.SinceUtc is { } sinceUtc)
        {
            query = query.Where(error => error.ObservedAtUtc >= sinceUtc);
        }

        return await query
            .OrderByDescending(error => error.DisplaySeverityProjection ?? 0)
            .ThenByDescending(error => error.ObservedAtUtc)
            .Take(Math.Min(request.Page.Take, MaxReplayTriggers))
            .ToListAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<CognitiveMemorySignalRecord>> LoadReplaySignalsAsync(
        AppDbContext dbContext,
        CognitiveMemoryReplayPlanRequest request,
        int take,
        CancellationToken cancellationToken)
    {
        var replaySignalIds = await dbContext.Set<CognitiveMemorySignalConsumerPolicyRecord>()
            .AsNoTracking()
            .Where(policy => policy.ProjectId == request.ProjectId && policy.ConsumerKind == CognitiveMemorySignalConsumerKind.ReplayScheduler)
            .Select(policy => policy.CognitiveSignalId)
            .Distinct()
            .Take(Math.Min(take * 4, MaxReplayTriggers))
            .ToListAsync(cancellationToken);
        var query = dbContext.Set<CognitiveMemorySignalRecord>()
            .AsNoTracking()
            .Where(signal => signal.ProjectId == request.ProjectId && replaySignalIds.Contains(signal.Id));
        if (request.SinceUtc is { } sinceUtc)
        {
            query = query.Where(signal => signal.ObservedAtUtc >= sinceUtc);
        }

        return await query
            .OrderByDescending(signal => signal.DisplayMagnitudeProjection ?? 0)
            .ThenByDescending(signal => signal.ObservedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    private static CognitiveMemoryReplayEnqueueRequest CreateReplayRequestFromPredictionError(
        CognitiveMemoryReplayPlanRequest request,
        CognitiveMemoryPredictionErrorRecord error)
    {
        var jobKind = error.ErrorKind switch
        {
            CognitiveMemoryPredictionErrorKind.WrongScope => CognitiveMemoryReplayJobKind.ContextBoundaryDrill,
            CognitiveMemoryPredictionErrorKind.OverconfidentIncorrect => CognitiveMemoryReplayJobKind.ReplayProbeRegression,
            CognitiveMemoryPredictionErrorKind.SourceInsufficient => CognitiveMemoryReplayJobKind.RefreshSourceAnchors,
            CognitiveMemoryPredictionErrorKind.StaleMemory => CognitiveMemoryReplayJobKind.RefreshSourceAnchors,
            CognitiveMemoryPredictionErrorKind.ContradictionObserved => CognitiveMemoryReplayJobKind.ResolveContradiction,
            CognitiveMemoryPredictionErrorKind.ProcedureFailed => CognitiveMemoryReplayJobKind.ValidateProcedure,
            _ => CognitiveMemoryReplayJobKind.RehearseClaim
        };
        return new CognitiveMemoryReplayEnqueueRequest(
            request.ProjectId,
            jobKind,
            $"Replay planned from prediction error '{error.Id:D}' ({error.ErrorKind}).",
            request.PolicyContext,
            Targets: CreateTargets(error),
            PredictionErrorIds: [new CognitiveMemoryPredictionErrorId(error.Id)]);
    }

    private static CognitiveMemoryReplayEnqueueRequest CreateReplayRequestFromSignal(
        CognitiveMemoryReplayPlanRequest request,
        CognitiveMemorySignalRecord signal)
    {
        var jobKind = signal.SignalKind switch
        {
            CognitiveMemorySignalKind.ContradictionPressure => CognitiveMemoryReplayJobKind.ResolveContradiction,
            CognitiveMemorySignalKind.StalenessPressure => CognitiveMemoryReplayJobKind.RefreshSourceAnchors,
            CognitiveMemorySignalKind.CalibrationRisk => CognitiveMemoryReplayJobKind.ReplayProbeRegression,
            CognitiveMemorySignalKind.KnownFailurePatternMatched => CognitiveMemoryReplayJobKind.ContextBoundaryDrill,
            _ => CognitiveMemoryReplayJobKind.SpacedRecall
        };
        return new CognitiveMemoryReplayEnqueueRequest(
            request.ProjectId,
            jobKind,
            $"Replay planned from signal '{signal.Id:D}' ({signal.SignalKind}).",
            request.PolicyContext,
            Targets: CreateTargets(signal),
            TriggerSignalIds: [new CognitiveMemorySignalId(signal.Id)]);
    }

    private static IReadOnlyList<CognitiveMemoryReplayJobTargetDraft> CreateTargets(CognitiveMemoryPredictionErrorRecord error)
    {
        var targets = new List<CognitiveMemoryReplayJobTargetDraft>(4);
        AddTarget(targets, CognitiveMemoryReplayJobTargetKind.PredictionError, error.Id, string.Empty, error.ObservationSummary);
        AddTarget(targets, CognitiveMemoryReplayJobTargetKind.MemoryRecord, error.MemoryRecordId, string.Empty, error.ExpectedSummary);
        AddTarget(targets, CognitiveMemoryReplayJobTargetKind.Claim, error.ClaimId, string.Empty, error.ExpectedSummary);
        AddTarget(targets, CognitiveMemoryReplayJobTargetKind.ProcedureSkill, error.ProcedureSkillId, string.Empty, error.CauseHypothesis);
        return targets;
    }

    private static IReadOnlyList<CognitiveMemoryReplayJobTargetDraft> CreateTargets(CognitiveMemorySignalRecord signal)
    {
        var targets = new List<CognitiveMemoryReplayJobTargetDraft>(4);
        AddTarget(targets, CognitiveMemoryReplayJobTargetKind.CognitiveSignal, signal.Id, string.Empty, signal.Summary);
        AddTarget(targets, CognitiveMemoryReplayJobTargetKind.MemoryRecord, signal.MemoryRecordId, string.Empty, signal.Summary);
        AddTarget(targets, CognitiveMemoryReplayJobTargetKind.Claim, signal.ClaimId, string.Empty, signal.Summary);
        AddTarget(targets, CognitiveMemoryReplayJobTargetKind.ProcedureSkill, signal.ProcedureSkillId, string.Empty, signal.Summary);
        return targets;
    }

    private static void AddTarget(
        List<CognitiveMemoryReplayJobTargetDraft> targets,
        CognitiveMemoryReplayJobTargetKind targetKind,
        Guid? targetId,
        string targetKey,
        string summary)
    {
        if (targetId is null || targetId.Value == Guid.Empty)
        {
            return;
        }

        targets.Add(new CognitiveMemoryReplayJobTargetDraft(
            targetKind,
            targetId,
            targetKey,
            string.Empty,
            summary));
    }

    private static CognitiveMemoryHash CreateReplayInputHash(CognitiveMemoryReplayEnqueueRequest request)
    {
        var builder = new StringBuilder(capacity: 256);
        AppendHashSegment(builder, request.ProjectId.ToString("D"));
        AppendHashSegment(builder, request.JobKind);
        AppendHashSegment(builder, request.Reason);
        AppendHashSegment(builder, request.PolicyContext.PolicyProfileId.Value);
        AppendHashSegment(builder, NormalizeSourceScope(request));
        foreach (var target in request.Targets ?? [])
        {
            AppendHashSegment(builder, $"{target.TargetKind}:{target.TargetId?.ToString("D") ?? target.TargetKey}:{target.RequiredInputHash}");
        }

        foreach (var signalId in request.TriggerSignalIds ?? [])
        {
            AppendHashSegment(builder, $"signal:{signalId.Value:D}");
        }

        foreach (var predictionErrorId in request.PredictionErrorIds ?? [])
        {
            AppendHashSegment(builder, $"error:{predictionErrorId.Value:D}");
        }

        return CognitiveMemoryHash.FromUtf8(builder.ToString());
    }

    private static void AppendHashSegment(StringBuilder builder, object? value)
    {
        if (builder.Length > 0)
        {
            builder.Append('|');
        }

        builder.Append(value);
    }

    private static string? ValidateWorkerSubmission(
        CognitiveMemoryReplayJobRecord job,
        CognitiveMemoryReplayWorkerResultSubmission submission)
    {
        if (!StringComparer.Ordinal.Equals(job.InputHash, submission.InputHash))
        {
            return "InputHashMismatch";
        }

        if (!StringComparer.Ordinal.Equals(job.AlgorithmVersion, submission.AlgorithmVersion))
        {
            return "AlgorithmVersionMismatch";
        }

        if (!StringComparer.Ordinal.Equals(job.SourceScopeKey, submission.SourceScopeKey))
        {
            return "SourceScopeMismatch";
        }

        if (!StringComparer.Ordinal.Equals(job.PolicyProfileId, submission.PolicyProfileId))
        {
            return "PolicyProfileMismatch";
        }

        if (!StringComparer.Ordinal.Equals(job.ExpectedOutputSchema, submission.OutputSchema))
        {
            return "OutputSchemaMismatch";
        }

        return null;
    }

    private static CognitiveMemoryReplayOutputStatus ResolveOutputStatus(CognitiveMemoryReplayOutputKind outputKind)
        => outputKind switch
        {
            CognitiveMemoryReplayOutputKind.ReviewItem => CognitiveMemoryReplayOutputStatus.NeedsReview,
            CognitiveMemoryReplayOutputKind.DraftClaimUpdate => CognitiveMemoryReplayOutputStatus.NeedsReview,
            CognitiveMemoryReplayOutputKind.ProjectionInvalidationRequest => CognitiveMemoryReplayOutputStatus.NeedsReview,
            _ => CognitiveMemoryReplayOutputStatus.Draft
        };

    private static int ToQueuePriority(double? displayScore)
        => displayScore is null ? 0 : (int)Math.Round(Math.Clamp(displayScore.Value, 0, 1) * 1000, MidpointRounding.AwayFromZero);

    private static string NormalizeSourceScope(CognitiveMemoryReplayEnqueueRequest request)
        => string.IsNullOrWhiteSpace(request.SourceScopeKey)
            ? request.ProjectId.ToString("D")
            : request.SourceScopeKey.Trim();

    private static CognitiveMemoryTemporalEpisodeLinkRecord CreateEpisodeLink(
        CognitiveMemoryTemporalEpisodeRecord episode,
        CognitiveMemoryTemporalEpisodeLinkDraft draft,
        DateTimeOffset now)
    {
        if (draft.LinkKind == CognitiveMemoryTemporalEpisodeLinkKind.Unknown)
        {
            throw new ArgumentException("Temporal episode link kind must be explicit.", nameof(draft));
        }

        if ((draft.TargetId is null || draft.TargetId.Value == Guid.Empty) && string.IsNullOrWhiteSpace(draft.TargetKey))
        {
            throw new ArgumentException("Temporal episode link requires a target id or target key.", nameof(draft));
        }

        return new CognitiveMemoryTemporalEpisodeLinkRecord
        {
            EpisodeId = episode.Id,
            ProjectId = episode.ProjectId,
            LinkKind = draft.LinkKind,
            TargetId = NormalizeOptional(draft.TargetId),
            TargetKey = draft.TargetKey.Trim(),
            Summary = draft.Summary.Trim(),
            CreatedAtUtc = now
        };
    }

    private static void AddStepEvidence(
        AppDbContext dbContext,
        CognitiveMemoryEpisodeStepRecord step,
        IReadOnlyList<Guid> evidenceAnchorIds,
        CognitiveMemoryEpisodeStepEvidenceRole role,
        DateTimeOffset now)
    {
        foreach (var evidenceAnchorId in evidenceAnchorIds)
        {
            dbContext.Add(new CognitiveMemoryEpisodeStepEvidenceRecord
            {
                StepId = step.Id,
                EpisodeId = step.EpisodeId,
                ProjectId = step.ProjectId,
                EvidenceRole = role,
                EvidenceAnchorId = evidenceAnchorId,
                CreatedAtUtc = now
            });
        }
    }

    private static async Task<IReadOnlyList<Guid>> ValidateSignalsAsync(
        AppDbContext dbContext,
        Guid projectId,
        IReadOnlyList<CognitiveMemorySignalId>? signalIds,
        CancellationToken cancellationToken)
    {
        var ids = signalIds?.Select(id => id.Value).Where(id => id != Guid.Empty).Distinct().ToArray() ?? [];
        if (ids.Length == 0)
        {
            return [];
        }

        var found = await dbContext.Set<CognitiveMemorySignalRecord>()
            .Where(signal => signal.ProjectId == projectId && ids.Contains(signal.Id))
            .Select(signal => signal.Id)
            .ToListAsync(cancellationToken);
        var missing = ids.Except(found).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException($"Replay trigger signal '{missing[0]:D}' does not exist in project '{projectId:D}'.");
        }

        return found;
    }

    private static async Task<IReadOnlyList<Guid>> ValidatePredictionErrorsAsync(
        AppDbContext dbContext,
        Guid projectId,
        IReadOnlyList<CognitiveMemoryPredictionErrorId>? predictionErrorIds,
        CancellationToken cancellationToken)
    {
        var ids = predictionErrorIds?.Select(id => id.Value).Where(id => id != Guid.Empty).Distinct().ToArray() ?? [];
        if (ids.Length == 0)
        {
            return [];
        }

        var found = await dbContext.Set<CognitiveMemoryPredictionErrorRecord>()
            .Where(error => error.ProjectId == projectId && ids.Contains(error.Id))
            .Select(error => error.Id)
            .ToListAsync(cancellationToken);
        var missing = ids.Except(found).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException($"Replay prediction error '{missing[0]:D}' does not exist in project '{projectId:D}'.");
        }

        return found;
    }

    private static async Task EnsureEvidenceAnchorsExistAsync(
        AppDbContext dbContext,
        Guid projectId,
        IReadOnlyList<Guid> evidenceAnchorIds,
        CancellationToken cancellationToken)
    {
        if (evidenceAnchorIds.Count == 0)
        {
            return;
        }

        var found = await dbContext.Set<CognitiveMemoryEvidenceAnchorRecord>()
            .Where(anchor => anchor.ProjectId == projectId && evidenceAnchorIds.Contains(anchor.Id))
            .Select(anchor => anchor.Id)
            .ToListAsync(cancellationToken);
        var missing = evidenceAnchorIds.Except(found).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException($"Evidence anchor '{missing[0]:D}' does not exist in project '{projectId:D}'.");
        }
    }

    private static async Task EnsureStepBelongsToEpisodeAsync(
        AppDbContext dbContext,
        Guid episodeId,
        CognitiveMemoryEpisodeStepId? stepId,
        CancellationToken cancellationToken)
    {
        if (stepId is null)
        {
            return;
        }

        var exists = await dbContext.Set<CognitiveMemoryEpisodeStepRecord>()
            .AnyAsync(step => step.Id == stepId.Value.Value && step.EpisodeId == episodeId, cancellationToken);
        if (!exists)
        {
            throw new InvalidOperationException($"Episode step '{stepId}' does not belong to temporal episode '{episodeId:D}'.");
        }
    }

    private static IReadOnlyList<Guid> DistinctEvidenceAnchorIds(IReadOnlyList<CognitiveMemoryEvidenceAnchorId>? evidenceAnchorIds)
        => evidenceAnchorIds?
            .Select(evidenceAnchorId => evidenceAnchorId.Value)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray() ?? [];

    private static void ValidateEpisodeRequest(CognitiveMemoryTemporalEpisodeCreateRequest request)
    {
        CognitiveMemoryGuard.EnsureNonEmpty(request.ProjectId, nameof(request.ProjectId));
        if (request.EpisodeKind == CognitiveMemoryTemporalEpisodeKind.Unknown)
        {
            throw new ArgumentException("Temporal episode kind must be explicit.", nameof(request));
        }

        if (request.EndedAtUtc is { } endedAtUtc && endedAtUtc < request.StartedAtUtc)
        {
            throw new ArgumentException("Temporal episode end cannot precede start.", nameof(request));
        }
    }

    private static void ValidateStepRequest(CognitiveMemoryEpisodeStepAppendRequest request)
    {
        if (request.ActionKind == CognitiveMemoryEpisodeStepActionKind.Unknown)
        {
            throw new ArgumentException("Episode step action kind must be explicit.", nameof(request));
        }
    }

    private static void ValidateCausalLinkRequest(CognitiveMemoryEpisodeCausalLinkRequest request)
    {
        if (request.LinkKind == CognitiveMemoryEpisodeCausalLinkKind.Unknown)
        {
            throw new ArgumentException("Episode causal link kind must be explicit.", nameof(request));
        }

        if (request.FromStepId is null && request.ToStepId is null && request.EvidenceAnchorId is null && request.ClaimId is null && request.PredictionErrorId is null)
        {
            throw new ArgumentException("Episode causal link requires at least one typed target.", nameof(request));
        }
    }

    private static void ValidateEnqueueRequest(CognitiveMemoryReplayEnqueueRequest request)
    {
        CognitiveMemoryGuard.EnsureNonEmpty(request.ProjectId, nameof(request.ProjectId));
        if (request.JobKind == CognitiveMemoryReplayJobKind.Unknown)
        {
            throw new ArgumentException("Replay job kind must be explicit.", nameof(request));
        }

        if ((request.Targets?.Count ?? 0) == 0 &&
            (request.TriggerSignalIds?.Count ?? 0) == 0 &&
            (request.PredictionErrorIds?.Count ?? 0) == 0)
        {
            throw new ArgumentException("Replay job requires at least one target, signal, or prediction error.", nameof(request));
        }
    }

    private static void ValidatePolicyTrace(Guid projectId, CognitiveMemoryPolicyContext policyContext)
    {
        if (policyContext.ProjectId != projectId)
        {
            throw new ArgumentException($"Policy context project '{policyContext.ProjectId:D}' does not match replay project '{projectId:D}'.", nameof(policyContext));
        }
    }

    private static string CreateOutcomeSummary(string expectedOutcome, string actualOutcome)
        => string.IsNullOrWhiteSpace(actualOutcome)
            ? expectedOutcome.Trim()
            : actualOutcome.Trim();

    private static string SerializeMetadata(IReadOnlyDictionary<string, string>? metadata)
        => metadata is null || metadata.Count == 0
            ? "{}"
            : JsonSerializer.Serialize(new Dictionary<string, string>(metadata, StringComparer.Ordinal), CognitiveMemoryJson.SerializerOptions);

    private static Guid? NormalizeOptional(Guid? value)
        => value is { } actual && actual != Guid.Empty ? actual : null;
}

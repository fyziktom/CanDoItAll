using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemorySignalLedger(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ICognitiveMemoryScoreSpaceRegistry scoreSpaceRegistry,
    ICognitiveMemoryScoreGeometryDriver scoreGeometryDriver,
    IClock clock) : ICognitiveMemorySignalLedger, ICognitiveMemoryPredictionErrorEngine
{
    public async ValueTask<CognitiveMemorySignalPublicationResult> PublishAsync(
        CognitiveMemorySignalPublicationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var result = await AddSignalAsync(dbContext, request, validatePredictionError: true, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async ValueTask<CognitiveMemorySignalQueryResult> QueryAsync(
        CognitiveMemorySignalQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        CognitiveMemoryGuard.EnsureNonEmpty(query.ProjectId, nameof(query.ProjectId));
        ValidatePolicyTrace(query.ProjectId, query.PolicyContext, query.PolicyContext.ActorId);
        cancellationToken.ThrowIfCancellationRequested();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var signalQuery = dbContext.Set<CognitiveMemorySignalRecord>()
            .AsNoTracking()
            .Where(signal => signal.ProjectId == query.ProjectId);

        var signalKinds = NormalizeEnums(query.SignalKinds);
        if (signalKinds.Count > 0)
        {
            signalQuery = signalQuery.Where(signal => signalKinds.Contains(signal.SignalKind));
        }

        var consumerKinds = NormalizeEnums(query.ConsumerKinds);
        if (consumerKinds.Count > 0)
        {
            var signalIds = await dbContext.Set<CognitiveMemorySignalConsumerPolicyRecord>()
                .AsNoTracking()
                .Where(policy => policy.ProjectId == query.ProjectId && consumerKinds.Contains(policy.ConsumerKind))
                .Select(policy => policy.CognitiveSignalId)
                .Distinct()
                .ToListAsync(cancellationToken);
            signalQuery = signalQuery.Where(signal => signalIds.Contains(signal.Id));
        }

        if (query.SinceUtc is { } sinceUtc)
        {
            signalQuery = signalQuery.Where(signal => signal.ObservedAtUtc >= sinceUtc);
        }

        signalQuery = query.PolicyContext.AllowRestrictedContent
            ? signalQuery
            : signalQuery.Where(signal => signal.AccessLevel <= query.PolicyContext.AccessLevel);

        var filtered = dbContext.Database.IsSqlite()
            ? await signalQuery.ToListAsync(cancellationToken)
            : await signalQuery
                .OrderByDescending(signal => signal.ObservedAtUtc)
                .ThenBy(signal => signal.Id)
                .Take(query.Page.Take)
                .ToListAsync(cancellationToken);
        filtered = filtered
            .OrderByDescending(signal => signal.ObservedAtUtc)
            .ThenBy(signal => signal.Id)
            .Take(query.Page.Take)
            .ToList();

        return new CognitiveMemorySignalQueryResult(filtered);
    }

    public async ValueTask<CognitiveMemoryPredictionExpectationRecord> RecordExpectationAsync(
        CognitiveMemoryPredictionExpectationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateExpectationRequest(request);
        cancellationToken.ThrowIfCancellationRequested();

        var createdAtUtc = request.CreatedAtUtc ?? clock.GetUtcNow();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureEvidenceAnchorsExistAsync(dbContext, request.ProjectId, request.EvidenceAnchorIds, cancellationToken);
        await EnsureRelatedRowsExistAsync(
            dbContext,
            request.ProjectId,
            request.WorkspaceFrameId?.Value,
            request.AttentionDecisionId?.Value,
            cancellationToken);

        var expectation = new CognitiveMemoryPredictionExpectationRecord
        {
            ProjectId = request.ProjectId,
            ExpectationKind = request.ExpectationKind,
            ActorKind = request.ActorKind,
            ActorId = CognitiveMemoryGuard.EnsureText(request.ActorId, nameof(request.ActorId)),
            PolicyProfileId = request.PolicyContext.PolicyProfileId.Value,
            WorkspaceFrameId = request.WorkspaceFrameId?.Value,
            AttentionDecisionId = request.AttentionDecisionId?.Value,
            MemoryRecordId = request.MemoryRecordId?.Value,
            ClaimId = request.ClaimId?.Value,
            SourceItemId = request.SourceItemId?.Value,
            ProcedureSkillId = NormalizeOptional(request.ProcedureSkillId),
            WorkflowRunId = NormalizeOptional(request.WorkflowRunId),
            ProcessRunId = NormalizeOptional(request.ProcessRunId),
            ProbeSessionId = NormalizeOptional(request.ProbeSessionId),
            ExpectedContextKey = request.ExpectedContextKey.Trim(),
            ExpectedSourceSufficiency = request.ExpectedSourceSufficiency,
            MinimumExpectedConfidence = request.MinimumExpectedConfidence,
            MaximumExpectedConfidence = request.MaximumExpectedConfidence,
            Summary = CognitiveMemoryGuard.EnsureText(request.Summary, nameof(request.Summary)),
            ExpectedOutcome = CognitiveMemoryGuard.EnsureText(request.ExpectedOutcome, nameof(request.ExpectedOutcome)),
            AlgorithmVersion = CognitiveMemoryScoreSpaceRegistry.CurrentAlgorithmVersion.Value,
            MetadataJson = SerializeMetadata(request.Metadata),
            CreatedAtUtc = createdAtUtc,
            ConcurrencyToken = Guid.NewGuid()
        };
        dbContext.Add(expectation);
        foreach (var evidenceAnchorId in DistinctEvidenceAnchorIds(request.EvidenceAnchorIds))
        {
            dbContext.Add(new CognitiveMemoryPredictionExpectationEvidenceAnchorRecord
            {
                PredictionExpectationId = expectation.Id,
                ProjectId = expectation.ProjectId,
                EvidenceAnchorId = evidenceAnchorId,
                CreatedAtUtc = createdAtUtc
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return expectation;
    }

    public async ValueTask<CognitiveMemoryPredictionErrorObservationResult> ObserveAsync(
        CognitiveMemoryPredictionErrorObservationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidatePredictionErrorRequest(request);
        cancellationToken.ThrowIfCancellationRequested();

        var observedAtUtc = request.ObservedAtUtc ?? clock.GetUtcNow();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureEvidenceAnchorsExistAsync(dbContext, request.ProjectId, request.EvidenceAnchorIds, cancellationToken);
        await EnsureRelatedRowsExistAsync(
            dbContext,
            request.ProjectId,
            request.WorkspaceFrameId?.Value,
            request.AttentionDecisionId?.Value,
            cancellationToken);
        if (request.ExpectationId is { } expectationId)
        {
            var expectationExists = await dbContext.Set<CognitiveMemoryPredictionExpectationRecord>()
                .AnyAsync(expectation => expectation.Id == expectationId.Value && expectation.ProjectId == request.ProjectId, cancellationToken);
            if (!expectationExists)
            {
                throw new InvalidOperationException($"Prediction expectation '{expectationId}' does not exist in project '{request.ProjectId:D}'.");
            }
        }

        var predictionErrorId = CognitiveMemoryPredictionErrorId.New();
        var severityTrace = await EvaluatePredictionErrorSeverityAsync(
            request.ProjectId,
            predictionErrorId.Value,
            request.SeverityComponents,
            request.EvidenceAnchorIds,
            observedAtUtc,
            cancellationToken);
        await CognitiveMemoryScoreTracePersistence.AddIfMissingAsync(dbContext, severityTrace, observedAtUtc, cancellationToken);

        var error = new CognitiveMemoryPredictionErrorRecord
        {
            Id = predictionErrorId.Value,
            ProjectId = request.ProjectId,
            PredictionExpectationId = request.ExpectationId?.Value,
            ErrorKind = request.ErrorKind,
            ActorKind = request.ActorKind,
            ActorId = CognitiveMemoryGuard.EnsureText(request.ActorId, nameof(request.ActorId)),
            PolicyProfileId = request.PolicyContext.PolicyProfileId.Value,
            WorkspaceFrameId = request.WorkspaceFrameId?.Value,
            AttentionDecisionId = request.AttentionDecisionId?.Value,
            MemoryRecordId = request.MemoryRecordId?.Value,
            ClaimId = request.ClaimId?.Value,
            SourceItemId = request.SourceItemId?.Value,
            ProcedureSkillId = NormalizeOptional(request.ProcedureSkillId),
            WorkflowRunId = NormalizeOptional(request.WorkflowRunId),
            ProcessRunId = NormalizeOptional(request.ProcessRunId),
            ProbeTurnId = NormalizeOptional(request.ProbeTurnId),
            SeverityScoreEvaluationTraceId = severityTrace.Id.Value,
            SeverityBucket = severityTrace.ScalarProjection?.Bucket ?? CognitiveMemoryScoreProjectionBucket.Unknown,
            DisplaySeverityProjection = severityTrace.ScalarProjection?.DisplayScore,
            SeverityComponentCount = severityTrace.InputVectors.Single().Components.Count,
            MatchedShapeCount = severityTrace.MatchedShapes.Count,
            MissingRequiredDimensionCount = severityTrace.MissingRequiredDimensions.Count,
            ObservationSummary = CognitiveMemoryGuard.EnsureText(request.ObservationSummary, nameof(request.ObservationSummary)),
            ExpectedSummary = CognitiveMemoryGuard.EnsureText(request.ExpectedSummary, nameof(request.ExpectedSummary)),
            ObservedSummary = CognitiveMemoryGuard.EnsureText(request.ObservedSummary, nameof(request.ObservedSummary)),
            CauseHypothesis = CognitiveMemoryGuard.EnsureText(request.CauseHypothesis, nameof(request.CauseHypothesis)),
            SuggestedActionKind = request.SuggestedActionKind,
            SuggestedAction = CognitiveMemoryGuard.EnsureText(request.SuggestedAction, nameof(request.SuggestedAction)),
            RequiresReview = request.RequiresReview || severityTrace.ScalarProjection?.Bucket is CognitiveMemoryScoreProjectionBucket.NeedsReview or CognitiveMemoryScoreProjectionBucket.Inhibit or CognitiveMemoryScoreProjectionBucket.Reject,
            AlgorithmVersion = severityTrace.AlgorithmVersion.Value,
            MetadataJson = SerializeMetadata(request.Metadata),
            ObservedAtUtc = observedAtUtc,
            CreatedAtUtc = observedAtUtc,
            ConcurrencyToken = Guid.NewGuid()
        };
        dbContext.Add(error);
        foreach (var evidenceAnchorId in DistinctEvidenceAnchorIds(request.EvidenceAnchorIds))
        {
            dbContext.Add(new CognitiveMemoryPredictionErrorEvidenceAnchorRecord
            {
                PredictionErrorId = error.Id,
                ProjectId = error.ProjectId,
                EvidenceAnchorId = evidenceAnchorId,
                CreatedAtUtc = observedAtUtc
            });
        }

        var publishedSignals = new List<CognitiveMemorySignalPublicationResult>();
        foreach (var signalDraft in request.SignalsToPublish ?? [])
        {
            var signalRequest = new CognitiveMemorySignalPublicationRequest(
                request.ProjectId,
                signalDraft.SignalKind,
                signalDraft.SourceKind,
                request.ActorKind,
                request.ActorId,
                request.PolicyContext,
                signalDraft.Summary,
                signalDraft.Components,
                signalDraft.ConsumerKinds,
                request.EvidenceAnchorIds,
                observedAtUtc,
                signalDraft.RequiresReview || error.RequiresReview,
                RiskLevel: MaxRisk(request.PolicyContext.RiskLevel, signalDraft.RiskLevel),
                WorkspaceFrameId: request.WorkspaceFrameId,
                AttentionDecisionId: request.AttentionDecisionId,
                PredictionErrorId: predictionErrorId,
                MemoryRecordId: request.MemoryRecordId,
                ClaimId: request.ClaimId,
                SourceItemId: request.SourceItemId,
                ProcedureSkillId: request.ProcedureSkillId,
                WorkflowRunId: request.WorkflowRunId,
                ProcessRunId: request.ProcessRunId,
                ProbeTurnId: request.ProbeTurnId,
                Metadata: signalDraft.Metadata);
            var signalResult = await AddSignalAsync(dbContext, signalRequest, validatePredictionError: false, cancellationToken);
            publishedSignals.Add(signalResult);
            dbContext.Add(new CognitiveMemoryPredictionErrorSignalRecord
            {
                PredictionErrorId = error.Id,
                CognitiveSignalId = signalResult.Signal.Id,
                ProjectId = error.ProjectId,
                CreatedAtUtc = observedAtUtc
            });
        }

        error.CreatedSignalCount = publishedSignals.Count;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new CognitiveMemoryPredictionErrorObservationResult(error, publishedSignals, severityTrace);
    }

    private async Task<CognitiveMemorySignalPublicationResult> AddSignalAsync(
        AppDbContext dbContext,
        CognitiveMemorySignalPublicationRequest request,
        bool validatePredictionError,
        CancellationToken cancellationToken)
    {
        ValidateSignalRequest(request);
        var observedAtUtc = request.ObservedAtUtc ?? clock.GetUtcNow();
        await EnsureEvidenceAnchorsExistAsync(dbContext, request.ProjectId, request.EvidenceAnchorIds, cancellationToken);
        await EnsureRelatedRowsExistAsync(
            dbContext,
            request.ProjectId,
            request.WorkspaceFrameId?.Value,
            request.AttentionDecisionId?.Value,
            cancellationToken);
        if (validatePredictionError && request.PredictionErrorId is { } predictionErrorId)
        {
            var exists = await dbContext.Set<CognitiveMemoryPredictionErrorRecord>()
                .AnyAsync(error => error.Id == predictionErrorId.Value && error.ProjectId == request.ProjectId, cancellationToken);
            if (!exists)
            {
                throw new InvalidOperationException($"Prediction error '{predictionErrorId}' does not exist in project '{request.ProjectId:D}'.");
            }
        }

        var signalId = CognitiveMemorySignalId.New();
        var trace = await EvaluateSignalAsync(
            request.ProjectId,
            signalId.Value,
            request.Components,
            request.EvidenceAnchorIds,
            observedAtUtc,
            cancellationToken);
        await CognitiveMemoryScoreTracePersistence.AddIfMissingAsync(dbContext, trace, observedAtUtc, cancellationToken);

        var requiresReview = request.RequiresReview ||
            request.RiskLevel == CognitiveMemoryRiskLevel.High ||
            request.AccessLevel == CognitiveMemoryAccessLevel.Restricted ||
            request.RedactionState == CognitiveMemoryRedactionState.Restricted;
        var signal = new CognitiveMemorySignalRecord
        {
            Id = signalId.Value,
            ProjectId = request.ProjectId,
            SignalKind = request.SignalKind,
            SourceKind = request.SourceKind,
            ActorKind = request.ActorKind,
            ActorId = CognitiveMemoryGuard.EnsureText(request.ActorId, nameof(request.ActorId)),
            PolicyProfileId = request.PolicyContext.PolicyProfileId.Value,
            AccessLevel = request.AccessLevel,
            RedactionState = request.RedactionState,
            RiskLevel = request.RiskLevel,
            RequiresReview = requiresReview,
            WorkspaceFrameId = request.WorkspaceFrameId?.Value,
            AttentionDecisionId = request.AttentionDecisionId?.Value,
            PredictionErrorId = request.PredictionErrorId?.Value,
            MemoryRecordId = request.MemoryRecordId?.Value,
            ClaimId = request.ClaimId?.Value,
            SourceItemId = request.SourceItemId?.Value,
            ProcedureSkillId = NormalizeOptional(request.ProcedureSkillId),
            WorkflowRunId = NormalizeOptional(request.WorkflowRunId),
            ProcessRunId = NormalizeOptional(request.ProcessRunId),
            ProbeTurnId = NormalizeOptional(request.ProbeTurnId),
            ReviewItemId = NormalizeOptional(request.ReviewItemId),
            SignalScoreEvaluationTraceId = trace.Id.Value,
            ScoreSchemaVersion = trace.SchemaVersion.Value,
            NormalizationProfileId = CognitiveMemoryScoreSpaceRegistry.CurrentNormalizationProfile.Value,
            AlgorithmVersion = trace.AlgorithmVersion.Value,
            ComponentCount = trace.InputVectors.Single().Components.Count,
            MatchedShapeCount = trace.MatchedShapes.Count,
            MissingRequiredDimensionCount = trace.MissingRequiredDimensions.Count,
            DisplayMagnitudeProjection = trace.ScalarProjection?.DisplayScore,
            Summary = CognitiveMemoryGuard.EnsureText(request.Summary, nameof(request.Summary)),
            MetadataJson = SerializeMetadata(request.Metadata),
            ObservedAtUtc = observedAtUtc,
            CreatedAtUtc = observedAtUtc,
            ConcurrencyToken = Guid.NewGuid()
        };
        dbContext.Add(signal);

        foreach (var evidenceAnchorId in DistinctEvidenceAnchorIds(request.EvidenceAnchorIds))
        {
            dbContext.Add(new CognitiveMemorySignalEvidenceAnchorRecord
            {
                CognitiveSignalId = signal.Id,
                ProjectId = signal.ProjectId,
                EvidenceAnchorId = evidenceAnchorId,
                CreatedAtUtc = observedAtUtc
            });
        }

        var consumerPolicies = new List<CognitiveMemorySignalConsumerPolicyRecord>();
        foreach (var consumerKind in NormalizeEnums(request.ConsumerKinds))
        {
            var policy = new CognitiveMemorySignalConsumerPolicyRecord
            {
                CognitiveSignalId = signal.Id,
                ProjectId = signal.ProjectId,
                ConsumerKind = consumerKind,
                MaximumAccessLevel = request.AccessLevel,
                RequiresReviewBeforeAction = requiresReview,
                CanCreateTruthDirectly = false,
                CreatedAtUtc = observedAtUtc
            };
            consumerPolicies.Add(policy);
            dbContext.Add(policy);
        }

        return new CognitiveMemorySignalPublicationResult(signal, consumerPolicies, trace);
    }

    private async ValueTask<CognitiveMemoryScoreEvaluationTrace> EvaluateSignalAsync(
        Guid projectId,
        Guid signalId,
        IReadOnlyList<CognitiveMemorySignalComponentDraft> components,
        IReadOnlyList<CognitiveMemoryEvidenceAnchorId> evidenceAnchorIds,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken)
    {
        await EnsureComponentsBelongToScoreSpaceAsync(
            CognitiveMemoryScoreSpaceKind.SalienceSignal,
            components,
            cancellationToken);
        var vector = BuildVector(
            CognitiveMemoryScoreSpaceKind.SalienceSignal,
            components,
            evidenceAnchorIds,
            observedAtUtc,
            CognitiveMemoryHash.FromUtf8($"signal:{signalId:D}:{string.Join("|", components.Select(component => $"{component.DimensionKind}:{component.NormalizedValue:0.###}:{component.Confidence:0.###}"))}"));
        return await scoreGeometryDriver.EvaluateAsync(
            new CognitiveMemoryScoreEvaluationRequest(
                projectId,
                CognitiveMemoryScoreOwnerKind.CognitiveSignal,
                signalId,
                CognitiveMemoryScoreSpaceKind.SalienceSignal,
                CognitiveMemoryScoreSpaceRegistry.CurrentSchemaVersion,
                [vector],
                BuildSignalShapes()),
            cancellationToken);
    }

    private async ValueTask<CognitiveMemoryScoreEvaluationTrace> EvaluatePredictionErrorSeverityAsync(
        Guid projectId,
        Guid predictionErrorId,
        IReadOnlyList<CognitiveMemorySignalComponentDraft> components,
        IReadOnlyList<CognitiveMemoryEvidenceAnchorId> evidenceAnchorIds,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken)
    {
        await EnsureComponentsBelongToScoreSpaceAsync(
            CognitiveMemoryScoreSpaceKind.PredictionErrorSeverity,
            components,
            cancellationToken);
        var vector = BuildVector(
            CognitiveMemoryScoreSpaceKind.PredictionErrorSeverity,
            components,
            evidenceAnchorIds,
            observedAtUtc,
            CognitiveMemoryHash.FromUtf8($"prediction-error:{predictionErrorId:D}:{string.Join("|", components.Select(component => $"{component.DimensionKind}:{component.NormalizedValue:0.###}:{component.Confidence:0.###}"))}"));
        return await scoreGeometryDriver.EvaluateAsync(
            new CognitiveMemoryScoreEvaluationRequest(
                projectId,
                CognitiveMemoryScoreOwnerKind.PredictionError,
                predictionErrorId,
                CognitiveMemoryScoreSpaceKind.PredictionErrorSeverity,
                CognitiveMemoryScoreSpaceRegistry.CurrentSchemaVersion,
                [vector],
                BuildPredictionErrorSeverityShapes()),
            cancellationToken);
    }

    private async Task EnsureComponentsBelongToScoreSpaceAsync(
        CognitiveMemoryScoreSpaceKind scoreSpaceKind,
        IReadOnlyList<CognitiveMemorySignalComponentDraft> components,
        CancellationToken cancellationToken)
    {
        var definition = await scoreSpaceRegistry.GetDefinitionAsync(
            scoreSpaceKind,
            CognitiveMemoryScoreSpaceRegistry.CurrentSchemaVersion,
            cancellationToken);
        var allowedDimensions = definition.Dimensions
            .Select(dimension => dimension.Kind)
            .ToHashSet();
        var invalidDimension = components.FirstOrDefault(component => !allowedDimensions.Contains(component.DimensionKind));
        if (invalidDimension is not null)
        {
            throw new InvalidOperationException($"Dimension '{invalidDimension.DimensionKind}' is not declared in score space '{scoreSpaceKind}'.");
        }
    }

    private static CognitiveMemoryScoreVectorSnapshot BuildVector(
        CognitiveMemoryScoreSpaceKind scoreSpaceKind,
        IReadOnlyList<CognitiveMemorySignalComponentDraft> components,
        IReadOnlyList<CognitiveMemoryEvidenceAnchorId> evidenceAnchorIds,
        DateTimeOffset observedAtUtc,
        CognitiveMemoryHash inputHash)
    {
        var evidenceRefs = DistinctEvidenceAnchorIds(evidenceAnchorIds)
            .Select(evidenceAnchorId => new CognitiveMemoryScoreEvidenceRef(
                CognitiveMemoryScoreEvidenceKind.EvidenceAnchor,
                evidenceAnchorId,
                1,
                observedAtUtc))
            .ToArray();
        return new CognitiveMemoryScoreVectorSnapshot(
            scoreSpaceKind,
            CognitiveMemoryScoreSpaceRegistry.CurrentSchemaVersion,
            CognitiveMemoryScoreSpaceRegistry.CurrentNormalizationProfile,
            components.Select(component => new CognitiveMemoryScoreComponent(
                    component.DimensionKind,
                    component.NormalizedValue,
                    component.Confidence,
                    evidenceRefs))
                .ToArray(),
            CognitiveMemoryScoreSpaceRegistry.CurrentAlgorithmVersion,
            observedAtUtc,
            inputHash);
    }

    private static IReadOnlyList<CognitiveMemoryScoreShapeSnapshot> BuildSignalShapes()
    {
        var schema = CognitiveMemoryScoreSpaceRegistry.CurrentSchemaVersion;
        var algorithm = CognitiveMemoryScoreSpaceRegistry.CurrentAlgorithmVersion;
        return
        [
            Shape(CognitiveMemoryScoreProjectionBucket.NeedsReview, "High risk signal requires review-aware consumption.",
            [
                Higher(CognitiveMemoryScoreDimensionKind.RiskImpact, 0.8)
            ]),
            Shape(CognitiveMemoryScoreProjectionBucket.Inhibit, "Wrong-scope signal marks context separation pressure.",
            [
                Higher(CognitiveMemoryScoreDimensionKind.OutcomeMismatch, 0.7),
                Higher(CognitiveMemoryScoreDimensionKind.ContextSeparation, 0.7)
            ]),
            Shape(CognitiveMemoryScoreProjectionBucket.NeedsReview, "Calibration-risk signal requires calibration-aware consumption.",
            [
                Higher(CognitiveMemoryScoreDimensionKind.CalibrationRisk, 0.7)
            ]),
            Shape(CognitiveMemoryScoreProjectionBucket.NeedsReview, "Source-weakness signal requires source-aware consumption.",
            [
                Higher(CognitiveMemoryScoreDimensionKind.SourceWeakness, 0.7)
            ])
        ];

        CognitiveMemoryScoreShapeSnapshot Shape(
            CognitiveMemoryScoreProjectionBucket bucket,
            string explanation,
            IReadOnlyList<CognitiveMemoryScoreShapeComponent> components)
            => new(
                CognitiveMemoryScoreShapeKind.ThresholdEnvelope,
                CognitiveMemoryScoreSpaceKind.SalienceSignal,
                schema,
                components,
                radius: null,
                bucket,
                explanation,
                [],
                algorithm);
    }

    private static IReadOnlyList<CognitiveMemoryScoreShapeSnapshot> BuildPredictionErrorSeverityShapes()
    {
        var schema = CognitiveMemoryScoreSpaceRegistry.CurrentSchemaVersion;
        var algorithm = CognitiveMemoryScoreSpaceRegistry.CurrentAlgorithmVersion;
        return
        [
            Shape(CognitiveMemoryScoreProjectionBucket.NeedsReview, "High prediction-error magnitude requires review-aware follow-up.",
            [
                Higher(CognitiveMemoryScoreDimensionKind.PredictionErrorMagnitude, 0.75)
            ]),
            Shape(CognitiveMemoryScoreProjectionBucket.Inhibit, "Wrong-scope prediction error marks context-boundary failure.",
            [
                Higher(CognitiveMemoryScoreDimensionKind.PredictionErrorMagnitude, 0.65),
                Higher(CognitiveMemoryScoreDimensionKind.ContextSeparation, 0.7)
            ]),
            Shape(CognitiveMemoryScoreProjectionBucket.NeedsReview, "Prediction error with high rework cost requires replay or review consideration.",
            [
                Higher(CognitiveMemoryScoreDimensionKind.ReworkCost, 0.7)
            ])
        ];

        CognitiveMemoryScoreShapeSnapshot Shape(
            CognitiveMemoryScoreProjectionBucket bucket,
            string explanation,
            IReadOnlyList<CognitiveMemoryScoreShapeComponent> components)
            => new(
                CognitiveMemoryScoreShapeKind.ThresholdEnvelope,
                CognitiveMemoryScoreSpaceKind.PredictionErrorSeverity,
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

    private static void ValidateSignalRequest(CognitiveMemorySignalPublicationRequest request)
    {
        CognitiveMemoryGuard.EnsureNonEmpty(request.ProjectId, nameof(request.ProjectId));
        if (request.SignalKind == CognitiveMemorySignalKind.Unknown)
        {
            throw new ArgumentException("Signal kind must be explicit.", nameof(request.SignalKind));
        }

        if (request.SourceKind == CognitiveMemorySignalSourceKind.Unknown)
        {
            throw new ArgumentException("Signal source kind must be explicit.", nameof(request.SourceKind));
        }

        if (request.ActorKind == CognitiveMemoryActorKind.System && string.IsNullOrWhiteSpace(request.ActorId))
        {
            throw new ArgumentException("Signal actor id is required.", nameof(request.ActorId));
        }

        ValidatePolicyTrace(request.ProjectId, request.PolicyContext, request.ActorId);
        EnsurePolicyCanWrite(request.AccessLevel, request.PolicyContext);
        CognitiveMemoryGuard.EnsureText(request.Summary, nameof(request.Summary));
        EnsureNonEmptyComponents(request.Components);
        EnsureNonEmptyEvidence(request.EvidenceAnchorIds);
        if (NormalizeEnums(request.ConsumerKinds).Count == 0)
        {
            throw new ArgumentException("At least one signal consumer kind is required.", nameof(request.ConsumerKinds));
        }
    }

    private static void ValidateExpectationRequest(CognitiveMemoryPredictionExpectationRequest request)
    {
        CognitiveMemoryGuard.EnsureNonEmpty(request.ProjectId, nameof(request.ProjectId));
        if (request.ExpectationKind == CognitiveMemoryPredictionExpectationKind.Unknown)
        {
            throw new ArgumentException("Prediction expectation kind must be explicit.", nameof(request.ExpectationKind));
        }

        ValidatePolicyTrace(request.ProjectId, request.PolicyContext, request.ActorId);
        CognitiveMemoryGuard.EnsureText(request.Summary, nameof(request.Summary));
        CognitiveMemoryGuard.EnsureText(request.ExpectedOutcome, nameof(request.ExpectedOutcome));
        EnsureNonEmptyEvidence(request.EvidenceAnchorIds);
        if (request.MinimumExpectedConfidence is { } minimum)
        {
            CognitiveMemoryScoreGuard.EnsureUnitInterval(minimum, nameof(request.MinimumExpectedConfidence));
        }

        if (request.MaximumExpectedConfidence is { } maximum)
        {
            CognitiveMemoryScoreGuard.EnsureUnitInterval(maximum, nameof(request.MaximumExpectedConfidence));
        }

        if (request.MinimumExpectedConfidence is { } min &&
            request.MaximumExpectedConfidence is { } max &&
            min > max)
        {
            throw new ArgumentException("Minimum expected confidence must not exceed maximum expected confidence.");
        }
    }

    private static void ValidatePredictionErrorRequest(CognitiveMemoryPredictionErrorObservationRequest request)
    {
        CognitiveMemoryGuard.EnsureNonEmpty(request.ProjectId, nameof(request.ProjectId));
        if (request.ErrorKind == CognitiveMemoryPredictionErrorKind.Unknown)
        {
            throw new ArgumentException("Prediction error kind must be explicit.", nameof(request.ErrorKind));
        }

        if (request.SuggestedActionKind == CognitiveMemoryPredictionSuggestedActionKind.Unknown)
        {
            throw new ArgumentException("Prediction error suggested action kind must be explicit.", nameof(request.SuggestedActionKind));
        }

        ValidatePolicyTrace(request.ProjectId, request.PolicyContext, request.ActorId);
        CognitiveMemoryGuard.EnsureText(request.ObservationSummary, nameof(request.ObservationSummary));
        CognitiveMemoryGuard.EnsureText(request.ExpectedSummary, nameof(request.ExpectedSummary));
        CognitiveMemoryGuard.EnsureText(request.ObservedSummary, nameof(request.ObservedSummary));
        CognitiveMemoryGuard.EnsureText(request.CauseHypothesis, nameof(request.CauseHypothesis));
        CognitiveMemoryGuard.EnsureText(request.SuggestedAction, nameof(request.SuggestedAction));
        EnsureNonEmptyComponents(request.SeverityComponents);
        EnsureNonEmptyEvidence(request.EvidenceAnchorIds);
    }

    private static void ValidatePolicyTrace(
        Guid projectId,
        CognitiveMemoryPolicyContext policyContext,
        string actorId)
    {
        ArgumentNullException.ThrowIfNull(policyContext);
        if (policyContext.ProjectId is { } policyProjectId && policyProjectId != projectId)
        {
            throw new InvalidOperationException($"Policy context project '{policyProjectId:D}' does not match signal project '{projectId:D}'.");
        }

        if (!string.Equals(policyContext.ActorId, CognitiveMemoryGuard.EnsureText(actorId, nameof(actorId)), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Signal actor id must match the policy context actor id.");
        }
    }

    private static void EnsurePolicyCanWrite(
        CognitiveMemoryAccessLevel accessLevel,
        CognitiveMemoryPolicyContext policyContext)
    {
        if (!PolicyCanRead(accessLevel, policyContext))
        {
            throw new InvalidOperationException($"Policy profile '{policyContext.PolicyProfileId}' cannot publish '{accessLevel}' cognitive memory signals.");
        }
    }

    private static bool PolicyCanRead(
        CognitiveMemoryAccessLevel accessLevel,
        CognitiveMemoryPolicyContext policyContext)
        => accessLevel <= policyContext.AccessLevel ||
            accessLevel == CognitiveMemoryAccessLevel.Restricted && policyContext.AllowRestrictedContent;

    private static void EnsureNonEmptyComponents(IReadOnlyList<CognitiveMemorySignalComponentDraft> components)
    {
        ArgumentNullException.ThrowIfNull(components);
        if (components.Count == 0)
        {
            throw new ArgumentException("Score components are required.", nameof(components));
        }

        if (components.Select(component => component.DimensionKind).Distinct().Count() != components.Count)
        {
            throw new ArgumentException("Score components must be unique by dimension.", nameof(components));
        }
    }

    private static void EnsureNonEmptyEvidence(IReadOnlyList<CognitiveMemoryEvidenceAnchorId> evidenceAnchorIds)
    {
        ArgumentNullException.ThrowIfNull(evidenceAnchorIds);
        if (evidenceAnchorIds.Count == 0)
        {
            throw new ArgumentException("At least one evidence anchor id is required.", nameof(evidenceAnchorIds));
        }
    }

    private static async Task EnsureEvidenceAnchorsExistAsync(
        AppDbContext dbContext,
        Guid projectId,
        IReadOnlyList<CognitiveMemoryEvidenceAnchorId> evidenceAnchorIds,
        CancellationToken cancellationToken)
    {
        var ids = DistinctEvidenceAnchorIds(evidenceAnchorIds);
        var found = await dbContext.Set<CognitiveMemoryEvidenceAnchorRecord>()
            .Where(anchor => anchor.ProjectId == projectId && ids.Contains(anchor.Id))
            .Select(anchor => anchor.Id)
            .ToListAsync(cancellationToken);
        var missing = ids.Except(found).ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException($"Evidence anchor '{missing[0]:D}' does not exist in project '{projectId:D}'.");
        }
    }

    private static async Task EnsureRelatedRowsExistAsync(
        AppDbContext dbContext,
        Guid projectId,
        Guid? workspaceFrameId,
        Guid? attentionDecisionId,
        CancellationToken cancellationToken)
    {
        if (workspaceFrameId is { } frameId)
        {
            var workspaceExists = await dbContext.Set<CognitiveMemoryWorkspaceFrameRecord>()
                .AnyAsync(frame => frame.Id == frameId && frame.ProjectId == projectId, cancellationToken);
            if (!workspaceExists)
            {
                throw new InvalidOperationException($"Workspace frame '{frameId:D}' does not exist in project '{projectId:D}'.");
            }
        }

        if (attentionDecisionId is { } decisionId)
        {
            var decisionExists = await dbContext.Set<CognitiveMemoryAttentionDecisionRecord>()
                .AnyAsync(decision => decision.Id == decisionId && decision.ProjectId == projectId, cancellationToken);
            if (!decisionExists)
            {
                throw new InvalidOperationException($"Attention decision '{decisionId:D}' does not exist in project '{projectId:D}'.");
            }
        }
    }

    private static IReadOnlyList<Guid> DistinctEvidenceAnchorIds(IReadOnlyList<CognitiveMemoryEvidenceAnchorId> evidenceAnchorIds)
        => evidenceAnchorIds
            .Select(evidenceAnchorId => evidenceAnchorId.Value)
            .Distinct()
            .ToArray();

    private static IReadOnlyList<TEnum> NormalizeEnums<TEnum>(IReadOnlyList<TEnum>? values)
        where TEnum : struct, Enum
        => values?
            .Where(value => Convert.ToInt32(value) != 0)
            .Distinct()
            .ToArray() ?? [];

    private static CognitiveMemoryRiskLevel MaxRisk(
        CognitiveMemoryRiskLevel left,
        CognitiveMemoryRiskLevel right)
        => left > right ? left : right;

    private static Guid? NormalizeOptional(Guid? value)
        => value is { } actual && actual != Guid.Empty ? actual : null;

    private static string SerializeMetadata(IReadOnlyDictionary<string, string>? metadata)
        => metadata is null || metadata.Count == 0
            ? "{}"
            : JsonSerializer.Serialize(new Dictionary<string, string>(metadata, StringComparer.Ordinal), CognitiveMemoryJson.SerializerOptions);
}

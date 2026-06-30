using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemoryProbeService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ICognitiveMemoryRecallOrchestrator recallOrchestrator,
    ICognitiveMemoryScoreGeometryDriver scoreGeometryDriver,
    ICognitiveMemoryCalibrationHealthService calibrationHealthService,
    IClock clock) : ICognitiveMemoryProbeService
{
    private static readonly Regex ProbeEmailRegex = new(
        @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex ProbePhoneRegex = new(
        @"(?:\+\d{1,3}\s*)?(?:\d[\s.-]?){7,}\d",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private const string ProbeFeedbackSourceSystem = "ProbeFeedback";
    private const string ProbeFeedbackSourceItemType = "ProbeTurnFeedback";
    private const string ProbeFeedbackAlgorithmVersion = "probe-feedback-repair-v1";
    private const int MaximumReviewTitleLength = 300;
    private const int MaximumReasonLength = 1200;
    private const int MaximumStoredProbeQuestionLength = 2000;
    private const int MaximumStoredProbeAnswerSummaryLength = 4000;
    private const int MaximumStoredProbeWarningsJsonLength = 4000;
    private const int MaximumStoredProbeMetadataJsonLength = 8000;
    private const int MaximumStoredProbeFeedbackNotesLength = 2000;
    private const int MaximumStoredProbeCorrectionLength = 8000;
    private const int MaximumStoredProbeFindingSummaryLength = 2000;
    private const int MaximumStoredProbeRegressionExpectedTextLength = 4000;

    public async ValueTask<CognitiveMemoryProbeSessionRecord> StartAsync(
        CognitiveMemoryProbeStartRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ProjectId == Guid.Empty)
        {
            throw new ArgumentException("Probe session requires a project id.", nameof(request));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var session = new CognitiveMemoryProbeSessionRecord
        {
            ProjectId = request.ProjectId,
            Status = CognitiveMemoryProbeSessionStatus.Active,
            RecallMode = request.RecallMode,
            WorkspaceFrameId = request.WorkspaceFrameId?.Value,
            Title = CognitiveMemoryGuard.EnsureText(request.Title, nameof(request.Title)),
            ActorId = CognitiveMemoryGuard.EnsureText(request.PolicyContext.ActorId, nameof(request.PolicyContext.ActorId)),
            PolicyProfileId = request.PolicyContext.PolicyProfileId.Value,
            AccessLevel = request.PolicyContext.AccessLevel,
            RiskLevel = request.PolicyContext.RiskLevel,
            AllowRestrictedContent = request.PolicyContext.AllowRestrictedContent,
            ProjectionCollectionName = request.ProjectionCollectionName?.Value ?? string.Empty,
            ProjectionProfileId = request.ProjectionProfileId?.Value ?? string.Empty,
            EmbeddingProfileId = request.EmbeddingProfileId?.Value ?? string.Empty,
            AlgorithmVersion = "probe-core-v1",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        dbContext.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        return session;
    }

    public async ValueTask<CognitiveMemoryProbeAskResult> AskAsync(
        CognitiveMemoryProbeAskRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.SessionId == Guid.Empty)
        {
            throw new ArgumentException("Probe ask requires a session id.", nameof(request));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var session = await dbContext.Set<CognitiveMemoryProbeSessionRecord>()
            .SingleOrDefaultAsync(item => item.Id == request.SessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Probe session '{request.SessionId:D}' was not found.");

        if (session.Status != CognitiveMemoryProbeSessionStatus.Active)
        {
            throw new InvalidOperationException($"Probe session '{session.Id:D}' is not active.");
        }

        var policyContext = CreateStoredPolicyContext(session);
        var recallResult = await recallOrchestrator.RecallAsync(
            new CognitiveMemoryRecallRequest(
                session.ProjectId,
                CognitiveMemoryGuard.EnsureText(request.Question, nameof(request.Question)),
                request.Intent,
                session.RecallMode,
                policyContext,
                request.Budget,
                WorkspaceFrameId: session.WorkspaceFrameId is { } frameId ? new CognitiveMemoryWorkspaceFrameId(frameId) : null,
                ProjectionCollectionName: request.ProjectionCollectionName ?? CreateProjectionCollectionName(session.ProjectionCollectionName),
                ProjectionProfileId: request.ProjectionProfileId ?? CreateProjectionProfileId(session.ProjectionProfileId),
                EmbeddingProfileId: request.EmbeddingProfileId ?? CreateEmbeddingProfileId(session.EmbeddingProfileId),
                Metadata: request.Metadata),
            cancellationToken);
        var now = clock.GetUtcNow();
        var includedSourceCount = recallResult.ContextPack.SourceRefs.Count(item => item.IncludedInContext);
        var trace = await CognitiveMemoryAdvancedScoring.EvaluateAndPersistAsync(
            dbContext,
            scoreGeometryDriver,
            session.ProjectId,
            CognitiveMemoryScoreOwnerKind.ProbeTurn,
            ownerId: null,
            CognitiveMemoryScoreSpaceKind.ProbeAssessment,
            [
                Component(CognitiveMemoryScoreDimensionKind.EvidenceStrength, includedSourceCount > 0 ? 0.75 : 0.2),
                Component(CognitiveMemoryScoreDimensionKind.SourceSufficiency, includedSourceCount > 0 ? 0.8 : 0.25),
                Component(CognitiveMemoryScoreDimensionKind.RegressionValue, 0.6)
            ],
            CognitiveMemoryScoreProjectionBucket.WeakAccept,
            now,
            cancellationToken);
        var sequence = session.TurnCount + 1;
        var answerSummary = CreateProbeAnswerSummary(request.Question, recallResult.ContextPack);
        var turn = new CognitiveMemoryProbeTurnRecord
        {
            ProbeSessionId = session.Id,
            ProjectId = session.ProjectId,
            Sequence = sequence,
            Status = CognitiveMemoryProbeTurnStatus.Answered,
            Intent = request.Intent,
            Question = TrimText(request.Question, MaximumStoredProbeQuestionLength),
            AnswerSummary = TrimText(answerSummary, MaximumStoredProbeAnswerSummaryLength),
            RecallTraceId = recallResult.TraceId,
            ContextPackId = recallResult.ContextPack.Id.Value,
            AnswerGateDecisionId = recallResult.ContextPack.Metadata.TryGetValue("answerGateDecisionId", out var answerGateId) &&
                                   Guid.TryParse(answerGateId, out var parsedAnswerGateId)
                ? parsedAnswerGateId
                : null,
            ProbeScoreEvaluationTraceId = trace.Id.Value,
            ProbeScoreBucket = trace.ScalarProjection?.Bucket ?? CognitiveMemoryScoreProjectionBucket.Unknown,
            DisplayProbeScore = trace.ScalarProjection?.DisplayScore,
            WarningCount = recallResult.Warnings.Count,
            WarningsJson = TrimText(Serialize(recallResult.Warnings), MaximumStoredProbeWarningsJsonLength),
            MetadataJson = TrimText(
                Serialize(request.Metadata ?? new Dictionary<string, string>()),
                MaximumStoredProbeMetadataJsonLength),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        session.TurnCount = sequence;
        session.UpdatedAtUtc = now;
        dbContext.Add(turn);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new CognitiveMemoryProbeAskResult(session, turn, recallResult);
    }

    private static CognitiveMemoryPolicyContext CreateStoredPolicyContext(CognitiveMemoryProbeSessionRecord session)
        => new(
            session.ProjectId,
            session.ActorId,
            session.AccessLevel,
            new CognitiveMemoryPolicyProfileId(session.PolicyProfileId),
            session.RiskLevel,
            session.AllowRestrictedContent);

    private static CognitiveMemoryProjectionCollectionName? CreateProjectionCollectionName(string value)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : new CognitiveMemoryProjectionCollectionName(value.Trim());

    private static CognitiveMemoryProjectionProfileId? CreateProjectionProfileId(string value)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : new CognitiveMemoryProjectionProfileId(value.Trim());

    private static CognitiveMemoryEmbeddingProfileId? CreateEmbeddingProfileId(string value)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : new CognitiveMemoryEmbeddingProfileId(value.Trim());

    private static string CreateProbeAnswerSummary(string question, CognitiveMemoryRecallContextPack contextPack)
    {
        var builder = new StringBuilder();
        builder.Append("Question: ");
        builder.AppendLine(TrimInline(question, 500));
        builder.Append("Context summary: ");
        builder.AppendLine(TrimInline(contextPack.Summary, 800));

        var includedSections = contextPack.Sections
            .Where(section => !string.IsNullOrWhiteSpace(section.Content))
            .Take(5)
            .ToArray();
        if (includedSections.Length == 0)
        {
            builder.AppendLine("Supported context: no selected context sections were available.");
        }
        else
        {
            builder.AppendLine("Supported context:");
            foreach (var section in includedSections)
            {
                builder.Append("- ");
                builder.Append(TrimInline(section.Title, 120));
                builder.Append(": ");
                builder.AppendLine(TrimInline(section.Content, 500));
            }
        }

        var sourceRefs = contextPack.SourceRefs
            .Concat(includedSections.SelectMany(section => section.SourceRefs))
            .Where(source => source.IncludedInContext)
            .DistinctBy(source => new { source.SourceSystem, source.Locator, source.Summary })
            .Take(8)
            .ToArray();
        if (sourceRefs.Length == 0)
        {
            builder.AppendLine("Source refs: none included in context.");
        }
        else
        {
            builder.AppendLine("Source refs:");
            foreach (var source in sourceRefs)
            {
                builder.Append("- ");
                builder.Append(TrimInline(source.SourceSystem, 80));
                builder.Append(" ");
                builder.Append(TrimInline(source.Locator, 220));
                builder.Append(": ");
                builder.AppendLine(TrimInline(source.Summary, 300));
            }
        }

        if (contextPack.Warnings.Count > 0)
        {
            builder.AppendLine("Warnings:");
            foreach (var warning in contextPack.Warnings.Take(5))
            {
                builder.Append("- ");
                builder.AppendLine(TrimInline(warning, 300));
            }
        }

        return builder.ToString().Trim();
    }

    public async ValueTask<CognitiveMemoryProbeFeedbackRecord> RecordFeedbackAsync(
        CognitiveMemoryProbeFeedbackRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var turn = await dbContext.Set<CognitiveMemoryProbeTurnRecord>()
            .SingleOrDefaultAsync(item => item.Id == request.TurnId, cancellationToken)
            ?? throw new InvalidOperationException($"Probe turn '{request.TurnId:D}' was not found.");
        var session = await dbContext.Set<CognitiveMemoryProbeSessionRecord>()
            .SingleAsync(item => item.Id == turn.ProbeSessionId, cancellationToken);
        var now = clock.GetUtcNow();
        var repairCandidateRequired = RequiresRepairCandidate(request.Action);
        var reviewRequired = request.RequestHumanReview ||
                             request.Action == CognitiveMemoryProbeFeedbackAction.RequestReview ||
                             repairCandidateRequired ||
                             request.RiskLevel == CognitiveMemoryRiskLevel.High;
        CognitiveMemoryReviewItemRecord? reviewItem = null;
        if (reviewRequired)
        {
            reviewItem = new CognitiveMemoryReviewItemRecord
            {
                ProjectId = turn.ProjectId,
                ReviewKind = CognitiveMemoryReviewKind.Contradiction,
                SubjectKind = CognitiveMemoryReviewSubjectKind.RecallTrace,
                SubjectId = turn.RecallTraceId,
                Status = CognitiveMemoryReviewStatus.Pending,
                RiskLevel = request.RiskLevel,
                ReasonCode = "probe-feedback",
                ReasonText = CreateReviewReasonText(request),
                SourceEvidenceCount = 0,
                CreatedAtUtc = now,
                ConcurrencyToken = Guid.NewGuid()
            };
            dbContext.Add(reviewItem);
        }

        CognitiveMemoryProbeRegressionTestCaseRecord? regression = null;
        if (request.CreateRegressionTest || request.Action == CognitiveMemoryProbeFeedbackAction.CreateRegression)
        {
            regression = new CognitiveMemoryProbeRegressionTestCaseRecord
            {
                ProjectId = turn.ProjectId,
                ProbeTurnId = turn.Id,
                Status = CognitiveMemoryProbeRegressionStatus.Active,
                Question = turn.Question,
                ExpectedEvidenceText = string.IsNullOrWhiteSpace(request.CorrectionText)
                    ? turn.AnswerSummary
                    : TrimText(request.CorrectionText, MaximumStoredProbeRegressionExpectedTextLength),
                ExpectedContextKey = "project-scope",
                AccessPolicyProfileId = session.PolicyProfileId,
                EvaluatorProfileVersion = "probe-regression-v1",
                CreatedAtUtc = now
            };
            dbContext.Add(regression);
        }

        var feedback = new CognitiveMemoryProbeFeedbackRecord
        {
            ProbeTurnId = turn.Id,
            ProbeSessionId = session.Id,
            ProjectId = turn.ProjectId,
            Action = request.Action,
            CalibrationOutcome = request.CalibrationOutcome,
            RiskLevel = request.RiskLevel,
            Notes = TrimText(request.Notes, MaximumStoredProbeFeedbackNotesLength),
            CorrectionText = TrimText(request.CorrectionText, MaximumStoredProbeCorrectionLength),
            ReviewItemId = reviewItem?.Id,
            RegressionTestCaseId = regression?.Id,
            CreatedAtUtc = now
        };
        dbContext.Add(feedback);

        if (repairCandidateRequired && reviewItem is not null)
        {
            AddProbeFeedbackRepairCandidate(dbContext, session, turn, feedback, request, reviewItem, now);
        }

        var findingKind = request.Action switch
        {
            CognitiveMemoryProbeFeedbackAction.NeedsSource => CognitiveMemoryProbeFindingKind.MissingSource,
            CognitiveMemoryProbeFeedbackAction.WrongScope => CognitiveMemoryProbeFindingKind.WrongScope,
            CognitiveMemoryProbeFeedbackAction.MarkIncorrect => CognitiveMemoryProbeFindingKind.Overconfident,
            CognitiveMemoryProbeFeedbackAction.AddCorrection => CognitiveMemoryProbeFindingKind.Contradiction,
            _ => CognitiveMemoryProbeFindingKind.Unknown
        };
        if (findingKind != CognitiveMemoryProbeFindingKind.Unknown)
        {
            dbContext.Add(new CognitiveMemoryProbeFindingRecord
            {
                ProbeTurnId = turn.Id,
                ProjectId = turn.ProjectId,
                FindingKind = findingKind,
                RiskLevel = request.RiskLevel,
                Summary = TrimText(
                    string.IsNullOrWhiteSpace(request.Notes) ? request.Action.ToString() : request.Notes,
                    MaximumStoredProbeFindingSummaryLength),
                ReviewItemId = reviewItem?.Id,
                CreatedAtUtc = now
            });
        }

        turn.Status = CognitiveMemoryProbeTurnStatus.FeedbackRecorded;
        turn.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        var calibrationEvent = await calibrationHealthService.RecordOutcomeAsync(
            new CognitiveMemoryCalibrationOutcomeRequest(
                turn.ProjectId,
                "probe",
                turn.Intent.ToString(),
                new CognitiveMemoryModelProfileId("default-model"),
                new CognitiveMemoryRiskKey(request.RiskLevel.ToString()),
                findingKind.ToString(),
                "calibration-v1",
                Math.Clamp(turn.DisplayProbeScore ?? 0.5, 0, 1),
                request.CalibrationOutcome is CognitiveMemoryCalibrationOutcomeKind.CorrectHighConfidence or CognitiveMemoryCalibrationOutcomeKind.CorrectLowConfidence,
                request.CalibrationOutcome,
                ProbeTurnId: turn.Id,
                RecallTraceId: turn.RecallTraceId,
                ReviewItemId: reviewItem?.Id),
            cancellationToken);

        await using var updateContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var persistedFeedback = await updateContext.Set<CognitiveMemoryProbeFeedbackRecord>()
            .SingleAsync(item => item.Id == feedback.Id, cancellationToken);
        persistedFeedback.CalibrationEventId = calibrationEvent.Id;
        await updateContext.SaveChangesAsync(cancellationToken);
        return persistedFeedback;
    }

    private static bool RequiresRepairCandidate(CognitiveMemoryProbeFeedbackAction action)
        => action is CognitiveMemoryProbeFeedbackAction.AddCorrection
            or CognitiveMemoryProbeFeedbackAction.MarkIncorrect
            or CognitiveMemoryProbeFeedbackAction.WrongScope;

    private static void AddProbeFeedbackRepairCandidate(
        AppDbContext dbContext,
        CognitiveMemoryProbeSessionRecord session,
        CognitiveMemoryProbeTurnRecord turn,
        CognitiveMemoryProbeFeedbackRecord feedback,
        CognitiveMemoryProbeFeedbackRequest request,
        CognitiveMemoryReviewItemRecord reviewItem,
        DateTimeOffset now)
    {
        var locator = $"probe-session/{session.Id:D}/turn/{turn.Id:D}/feedback/{feedback.Id:D}";
        var content = CreateProbeFeedbackSourceContent(turn, request);
        var contentHash = CognitiveMemoryHash.FromUtf8(content).Value;
        var correctionSummary = CreateCorrectionSummary(turn, request);
        var correctionHash = CognitiveMemoryHash.FromUtf8(correctionSummary).Value;
        var idempotencyKey = $"probe-feedback-repair:{feedback.Id:D}";
        var title = TrimText($"Probe correction: {turn.Question}", MaximumReviewTitleLength);

        var sourceManifest = new CognitiveMemorySourceManifestRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = turn.ProjectId,
            SourceSystem = ProbeFeedbackSourceSystem,
            SourceScopeKey = $"project:{turn.ProjectId:D}",
            SourceSnapshotId = $"probe-feedback:{feedback.Id:D}",
            SnapshotHash = contentHash,
            ProviderVersion = ProbeFeedbackAlgorithmVersion,
            ScanStatus = CognitiveMemoryRunStatus.Succeeded,
            ObservedAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        var sourceItem = new CognitiveMemorySourceItemRecord
        {
            Id = Guid.NewGuid(),
            SourceManifestId = sourceManifest.Id,
            ProjectId = turn.ProjectId,
            SourceSystem = ProbeFeedbackSourceSystem,
            SourceItemKey = $"probe-turn-feedback:{feedback.Id:D}",
            SourceItemType = ProbeFeedbackSourceItemType,
            Title = title,
            ContentText = content,
            Locator = locator,
            ContentHash = contentHash,
            RedactionState = CognitiveMemoryRedactionState.Safe,
            AccessLevel = CognitiveMemoryAccessLevel.Project,
            AccessScope = turn.ProjectId.ToString("D"),
            ProvenanceJson = CreateProbeFeedbackProvenanceJson(session, turn, feedback, request),
            ObservedAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        var evidenceAnchor = new CognitiveMemoryEvidenceAnchorRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = turn.ProjectId,
            AnchorKind = CognitiveMemoryEvidenceAnchorKind.ProbeTurn,
            SourceManifestId = sourceManifest.Id,
            SourceItemId = sourceItem.Id,
            SourceSystem = ProbeFeedbackSourceSystem,
            Locator = locator,
            StructuredPath = "$.probeFeedback",
            TextStart = 0,
            TextEnd = content.Length,
            QuoteHash = correctionHash,
            TrustLevel = CognitiveMemorySourceTrustLevel.ExternalUnverified,
            RedactionState = CognitiveMemoryRedactionState.Safe,
            SourceHash = contentHash,
            ObservedAtUtc = now,
            CreatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        var mutationCommand = new CognitiveMemoryMutationCommandRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = turn.ProjectId,
            CommandKind = ResolveRepairCommandKind(request.Action),
            Status = CognitiveMemoryMutationCommandStatus.ReviewRequired,
            ActorKind = CognitiveMemoryActorKind.User,
            ActorId = session.ActorId,
            IdempotencyKey = idempotencyKey,
            EvidenceAnchorIdsJson = SerializeGuidList([evidenceAnchor.Id]),
            PayloadJson = content,
            RequiresHumanReview = true,
            ReviewReason = CreateCandidateReason(request),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        var runId = Guid.NewGuid();
        var run = new CognitiveMemoryRunRecord
        {
            Id = runId,
            ProjectId = turn.ProjectId,
            RunKind = CognitiveMemoryRunKind.Consolidation,
            Status = CognitiveMemoryRunStatus.Succeeded,
            OperationMode = CognitiveMemoryOperationMode.Consolidate,
            IdempotencyKey = idempotencyKey,
            InputHash = contentHash,
            AlgorithmVersion = ProbeFeedbackAlgorithmVersion,
            StartedAtUtc = now,
            CompletedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        var consolidationRun = new CognitiveMemoryConsolidationRunRecord
        {
            Id = runId,
            ProjectId = turn.ProjectId,
            Mode = CognitiveMemoryConsolidationMode.ContradictionReview,
            TriggerKind = CognitiveMemoryConsolidationTriggerKind.Manual,
            Status = CognitiveMemoryRunStatus.Succeeded,
            ProfileName = "probe-feedback-repair",
            IdempotencyKey = idempotencyKey,
            InputHash = contentHash,
            OutputHash = correctionHash,
            AlgorithmVersion = ProbeFeedbackAlgorithmVersion,
            LeaseOwnerId = session.ActorId,
            LeaseExpiresAtUtc = now,
            SourceItemsScanned = 1,
            CandidatesCreated = 1,
            MutationCommandsSubmitted = 1,
            ReviewItemsCreated = 1,
            StartedAtUtc = now,
            CompletedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        var payload = new CognitiveMemoryConsolidationCandidatePayload(
            CognitiveMemoryConsolidationCandidateKind.Contradiction,
            sourceItem.Id,
            evidenceAnchor.Id,
            mutationCommand.Id,
            reviewItem.Id,
            ProbeFeedbackSourceSystem,
            ProbeFeedbackSourceItemType,
            title,
            correctionSummary,
            contentHash,
            CreateCandidateReason(request));
        var candidate = new CognitiveMemoryConsolidationCandidateRecord
        {
            Id = Guid.NewGuid(),
            RunId = consolidationRun.Id,
            ProjectId = turn.ProjectId,
            CandidateKind = payload.CandidateKind,
            Status = CognitiveMemoryConsolidationCandidateStatus.ReviewRequired,
            SourceItemId = sourceItem.Id,
            EvidenceAnchorId = evidenceAnchor.Id,
            MutationCommandId = mutationCommand.Id,
            ReviewItemId = reviewItem.Id,
            ScoreBucket = CognitiveMemoryScoreProjectionBucket.NeedsReview,
            DisplayPriorityProjection = ResolveRepairPriority(request.RiskLevel),
            SourceContentHash = contentHash,
            OutputHash = correctionHash,
            AlgorithmVersion = ProbeFeedbackAlgorithmVersion,
            ReasonCode = "ProbeFeedbackRepair",
            ReasonText = payload.Reason,
            PayloadJson = JsonSerializer.Serialize(
                payload,
                CognitiveMemoryJsonSerializerContext.Default.CognitiveMemoryConsolidationCandidatePayload),
            CreatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };

        reviewItem.SourceEvidenceCount = 1;

        dbContext.AddRange(sourceManifest, sourceItem, evidenceAnchor, mutationCommand, run, consolidationRun, candidate);
        dbContext.Add(new CognitiveMemoryMutationAuditEventRecord
        {
            Id = Guid.NewGuid(),
            MutationCommandId = mutationCommand.Id,
            ProjectId = turn.ProjectId,
            Sequence = 1,
            EventKind = CognitiveMemoryMutationAuditEventKind.ReviewRequired,
            Message = $"Probe feedback repair candidate '{candidate.Id:D}' requires review before memory mutation.",
            CreatedAtUtc = now
        });
    }

    private static CognitiveMemoryMutationCommandKind ResolveRepairCommandKind(CognitiveMemoryProbeFeedbackAction action)
        => action switch
        {
            CognitiveMemoryProbeFeedbackAction.WrongScope => CognitiveMemoryMutationCommandKind.NarrowScope,
            CognitiveMemoryProbeFeedbackAction.MarkIncorrect => CognitiveMemoryMutationCommandKind.AttackClaim,
            _ => CognitiveMemoryMutationCommandKind.ProposeClaim
        };

    private static string CreateProbeFeedbackSourceContent(
        CognitiveMemoryProbeTurnRecord turn,
        CognitiveMemoryProbeFeedbackRequest request)
        => string.Join(
            Environment.NewLine,
            [
                $"Question: {turn.Question}",
                $"Recalled answer summary: {FirstNonEmpty(turn.AnswerSummary, "No answer summary was recorded.")}",
                $"Feedback action: {request.Action}",
                $"Calibration outcome: {request.CalibrationOutcome}",
                $"Risk level: {request.RiskLevel}",
                $"User notes: {FirstNonEmpty(request.Notes, "No notes supplied.")}",
                $"Correction or expected truth: {FirstNonEmpty(request.CorrectionText, request.Notes, $"The probe answer was flagged as {request.Action}.")}"
            ]);

    private static string CreateProbeFeedbackProvenanceJson(
        CognitiveMemoryProbeSessionRecord session,
        CognitiveMemoryProbeTurnRecord turn,
        CognitiveMemoryProbeFeedbackRecord feedback,
        CognitiveMemoryProbeFeedbackRequest request)
    {
        var payload = new Dictionary<string, string>
        {
            ["sourceSystem"] = ProbeFeedbackSourceSystem,
            ["sessionId"] = session.Id.ToString("D"),
            ["turnId"] = turn.Id.ToString("D"),
            ["feedbackId"] = feedback.Id.ToString("D"),
            ["recallTraceId"] = turn.RecallTraceId.ToString("D"),
            ["action"] = request.Action.ToString(),
            ["calibrationOutcome"] = request.CalibrationOutcome.ToString(),
            ["riskLevel"] = request.RiskLevel.ToString()
        };

        return JsonSerializer.Serialize(
            payload,
            CognitiveMemoryJsonSerializerContext.Default.DictionaryStringString);
    }

    private static string CreateReviewReasonText(CognitiveMemoryProbeFeedbackRequest request)
        => FirstNonEmpty(
            request.Notes,
            request.CorrectionText,
            request.Action is CognitiveMemoryProbeFeedbackAction.AddCorrection
                or CognitiveMemoryProbeFeedbackAction.MarkIncorrect
                or CognitiveMemoryProbeFeedbackAction.WrongScope
                ? "Probe feedback proposed a source-backed memory repair."
                : "Probe feedback requires review.");

    private static string CreateCorrectionSummary(
        CognitiveMemoryProbeTurnRecord turn,
        CognitiveMemoryProbeFeedbackRequest request)
    {
        var fallback = request.Action switch
        {
            CognitiveMemoryProbeFeedbackAction.WrongScope => $"The answer to '{turn.Question}' was marked wrong-scope and must be narrowed before reuse.",
            CognitiveMemoryProbeFeedbackAction.MarkIncorrect => $"The answer to '{turn.Question}' was marked incorrect and must not be reused as trusted memory without repair.",
            _ => $"The answer to '{turn.Question}' has a user-supplied correction."
        };

        return TrimText(FirstNonEmpty(request.CorrectionText, request.Notes, fallback), MaximumReasonLength);
    }

    private static string CreateCandidateReason(CognitiveMemoryProbeFeedbackRequest request)
        => TrimText(
            $"Probe feedback action {request.Action} requires review-gated repair before the correction can become trusted memory. {FirstNonEmpty(request.Notes, request.CorrectionText, string.Empty)}",
            MaximumReasonLength);

    private static double ResolveRepairPriority(CognitiveMemoryRiskLevel riskLevel)
        => riskLevel switch
        {
            CognitiveMemoryRiskLevel.High => 0.95,
            CognitiveMemoryRiskLevel.Medium => 0.75,
            _ => 0.55
        };

    private static string SerializeGuidList(IReadOnlyList<Guid> values)
        => JsonSerializer.Serialize(
            values.ToArray(),
            CognitiveMemoryJsonSerializerContext.Default.GuidArray);

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static string TrimText(string? value, int maxLength)
    {
        var trimmed = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string TrimInline(string? value, int maxLength)
        => RedactSensitiveText(TrimText(value, maxLength))
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\n', ' ');

    private static string RedactSensitiveText(string value)
    {
        var redacted = ProbeEmailRegex.Replace(value, "[redacted-email]");
        return ProbePhoneRegex.Replace(redacted, "[redacted-phone]");
    }

    public async ValueTask<CognitiveMemoryProbeRegressionRunRecord> ReplayRegressionAsync(
        CognitiveMemoryProbeReplayRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var regression = await dbContext.Set<CognitiveMemoryProbeRegressionTestCaseRecord>()
            .SingleOrDefaultAsync(item => item.Id == request.RegressionTestCaseId, cancellationToken)
            ?? throw new InvalidOperationException($"Probe regression test case '{request.RegressionTestCaseId:D}' was not found.");
        var now = clock.GetUtcNow();
        var run = new CognitiveMemoryProbeRegressionRunRecord
        {
            ProjectId = regression.ProjectId,
            RegressionTestCaseId = regression.Id,
            EvaluatorProfileVersion = regression.EvaluatorProfileVersion,
            StartedAtUtc = now
        };

        try
        {
            var result = await recallOrchestrator.RecallAsync(
                new CognitiveMemoryRecallRequest(
                    regression.ProjectId,
                    regression.Question,
                    CognitiveMemoryRecallIntentKind.Testing,
                    CognitiveMemoryRecallMode.DeepSourceGrounded,
                    request.PolicyContext,
                    request.Budget),
                cancellationToken);
            var rendered = string.Join(
                Environment.NewLine,
                result.ContextPack.Sections.Select(section => $"{section.Title}\n{section.Content}"));
            run.RecallTraceId = result.TraceId;
            run.Outcome = string.IsNullOrWhiteSpace(regression.ExpectedEvidenceText) ||
                          rendered.Contains(regression.ExpectedEvidenceText, StringComparison.OrdinalIgnoreCase)
                ? CognitiveMemoryProbeRegressionRunOutcome.Passed
                : CognitiveMemoryProbeRegressionRunOutcome.Failed;
            run.FailureReason = run.Outcome == CognitiveMemoryProbeRegressionRunOutcome.Failed
                ? "Expected evidence text was not present in the replayed context pack."
                : string.Empty;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            run.Outcome = CognitiveMemoryProbeRegressionRunOutcome.Blocked;
            run.FailureReason = exception.Message;
        }

        run.CompletedAtUtc = clock.GetUtcNow();
        dbContext.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);
        return run;
    }

    private static CognitiveMemoryScoreComponent Component(CognitiveMemoryScoreDimensionKind kind, double value)
        => CognitiveMemoryAdvancedScoring.Component(kind, value);

    private static string Serialize<T>(T value)
        => JsonSerializer.Serialize(value, CognitiveMemoryAdvancedJson.Options);
}

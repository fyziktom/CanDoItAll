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

        var policyContext = new CognitiveMemoryPolicyContext(
            session.ProjectId,
            session.ActorId,
            CognitiveMemoryAccessLevel.Project,
            new CognitiveMemoryPolicyProfileId(session.PolicyProfileId),
            CognitiveMemoryRiskLevel.Low,
            AllowRestrictedContent: false);
        var recallResult = await recallOrchestrator.RecallAsync(
            new CognitiveMemoryRecallRequest(
                session.ProjectId,
                CognitiveMemoryGuard.EnsureText(request.Question, nameof(request.Question)),
                request.Intent,
                session.RecallMode,
                policyContext,
                request.Budget,
                WorkspaceFrameId: session.WorkspaceFrameId is { } frameId ? new CognitiveMemoryWorkspaceFrameId(frameId) : null,
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

public sealed class CognitiveMemorySelfModelStore(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ICognitiveMemoryScoreGeometryDriver scoreGeometryDriver,
    IClock clock) : ICognitiveMemorySelfModelStore
{
    public async ValueTask<CognitiveMemorySelfModelProfileRecord> EnsureSeedProfileAsync(
        CognitiveMemorySelfModelQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await FindSelfModelAsync(dbContext, query, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var now = clock.GetUtcNow();
        var selfModel = new CognitiveMemorySelfModelProfileRecord
        {
            ProjectId = query.ProjectId,
            Status = CognitiveMemorySelfModelStatus.Active,
            ModelProfileId = NormalizeModelProfileId(query.ModelProfileId),
            RoleKey = NormalizeRoleKey(query.RoleKey),
            ProfileVersion = "self-model-seed-v1",
            OperatingPrinciples = "Use source-backed memory, expose uncertainty, and route risky or source-poor answers to review.",
            AllowedTaskCategoriesJson = JsonSerializer.Serialize(
                new[] { "development", "architecture", "testing", "analysis" },
                CognitiveMemoryAdvancedJson.Options),
            RestrictedTaskCategoriesJson = JsonSerializer.Serialize(
                new[] { "unreviewed-high-risk-procedure", "redacted-source-disclosure" },
                CognitiveMemoryAdvancedJson.Options),
            AlgorithmVersion = "self-model-v1",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        dbContext.Add(selfModel);
        await dbContext.SaveChangesAsync(cancellationToken);

        var competenceTrace = await CognitiveMemoryAdvancedScoring.EvaluateAndPersistAsync(
            dbContext,
            scoreGeometryDriver,
            query.ProjectId,
            CognitiveMemoryScoreOwnerKind.CalibrationAggregate,
            selfModel.Id,
            CognitiveMemoryScoreSpaceKind.SelfModelCompetence,
            [
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.SourceSufficiency, 0.55),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.RegressionFailure, 0.2),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.SelfModelStability, 0.7)
            ],
            CognitiveMemoryScoreProjectionBucket.WeakAccept,
            now,
            cancellationToken);
        dbContext.Add(new CognitiveMemoryDomainCompetenceProfileRecord
        {
            ProjectId = query.ProjectId,
            SelfModelProfileId = selfModel.Id,
            DomainKey = NormalizeKey(query.DomainKey),
            TaskTypeKey = NormalizeKey(query.TaskTypeKey),
            ModelProfileId = NormalizeModelProfileId(query.ModelProfileId),
            ProfileVersion = selfModel.ProfileVersion,
            CompetenceLevel = CognitiveMemoryCompetenceLevel.Developing,
            CompetenceScoreEvaluationTraceId = competenceTrace.Id.Value,
            EvidenceCount = 1,
            EvidenceRefsJson = "[]",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        dbContext.Add(new CognitiveMemoryKnownFailurePatternRecord
        {
            ProjectId = query.ProjectId,
            SelfModelProfileId = selfModel.Id,
            PatternKind = CognitiveMemoryKnownFailurePatternKind.SourceInsufficientAnswer,
            DomainKey = NormalizeKey(query.DomainKey),
            TaskTypeKey = NormalizeKey(query.TaskTypeKey),
            TriggerSummary = "Source insufficiency or wrong-scope evidence should trigger source audit or probing.",
            Mitigation = "Require source-backed context or ask for clarification before producing a confident answer.",
            RequiresReview = true,
            EvidenceRefsJson = "[]",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        dbContext.Add(new CognitiveMemorySelfRegulationPolicyProfileRecord
        {
            ProjectId = query.ProjectId,
            SelfModelProfileId = selfModel.Id,
            PolicyKey = "default",
            ProfileVersion = selfModel.ProfileVersion,
            AllowedPosturesJson = JsonSerializer.Serialize(Enum.GetNames<CognitiveMemoryAnswerPostureKind>(), CognitiveMemoryAdvancedJson.Options),
            RequiredOperationsJson = JsonSerializer.Serialize(Enum.GetNames<CognitiveMemoryRequiredOperationKind>(), CognitiveMemoryAdvancedJson.Options),
            ReviewThreshold = 0.65,
            AbstentionThreshold = 0.85,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return selfModel;
    }

    public async ValueTask<CognitiveMemorySelfModelSnapshot> LoadAsync(
        CognitiveMemorySelfModelQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var selfModel = await FindSelfModelAsync(dbContext, query, cancellationToken)
            ?? throw new InvalidOperationException($"No active cognitive self-model exists for model profile '{query.ModelProfileId}' and role '{query.RoleKey}'.");
        var competence = await dbContext.Set<CognitiveMemoryDomainCompetenceProfileRecord>()
            .AsNoTracking()
            .Where(item => item.ProjectId == query.ProjectId &&
                           item.ModelProfileId == NormalizeModelProfileId(query.ModelProfileId) &&
                           item.DomainKey == NormalizeKey(query.DomainKey) &&
                           item.TaskTypeKey == NormalizeKey(query.TaskTypeKey) &&
                           item.ProfileVersion == selfModel.ProfileVersion)
            .SingleOrDefaultAsync(cancellationToken);
        var patterns = await dbContext.Set<CognitiveMemoryKnownFailurePatternRecord>()
            .AsNoTracking()
            .Where(item => item.SelfModelProfileId == selfModel.Id &&
                           item.DomainKey == NormalizeKey(query.DomainKey) &&
                           item.TaskTypeKey == NormalizeKey(query.TaskTypeKey))
            .OrderBy(item => item.PatternKind)
            .ToListAsync(cancellationToken);
        var policy = await dbContext.Set<CognitiveMemorySelfRegulationPolicyProfileRecord>()
            .AsNoTracking()
            .Where(item => item.SelfModelProfileId == selfModel.Id &&
                           item.PolicyKey == "default" &&
                           item.ProfileVersion == selfModel.ProfileVersion)
            .SingleOrDefaultAsync(cancellationToken);
        return new CognitiveMemorySelfModelSnapshot(selfModel, competence, patterns, policy);
    }

    public async ValueTask<CognitiveMemorySelfModelUpdateProposalRecord> ProposeUpdateAsync(
        CognitiveMemorySelfModelUpdateProposalRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.EvidenceRefs.Count == 0)
        {
            throw new InvalidOperationException("Self-model updates require evidence references.");
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var proposal = new CognitiveMemorySelfModelUpdateProposalRecord
        {
            ProjectId = request.ProjectId,
            Status = CognitiveMemorySelfModelUpdateProposalStatus.PendingReview,
            ModelProfileId = NormalizeModelProfileId(request.ModelProfileId),
            DomainKey = NormalizeKey(request.DomainKey),
            ProposedChange = CognitiveMemoryGuard.EnsureText(request.ProposedChange, nameof(request.ProposedChange)),
            EvidenceRefsJson = JsonSerializer.Serialize(request.EvidenceRefs, CognitiveMemoryAdvancedJson.Options),
            RequestedByActorId = CognitiveMemoryGuard.EnsureText(request.RequestedByActorId, nameof(request.RequestedByActorId)),
            CreatedAtUtc = clock.GetUtcNow()
        };
        dbContext.Add(proposal);
        await dbContext.SaveChangesAsync(cancellationToken);
        return proposal;
    }

    private static Task<CognitiveMemorySelfModelProfileRecord?> FindSelfModelAsync(
        AppDbContext dbContext,
        CognitiveMemorySelfModelQuery query,
        CancellationToken cancellationToken)
        => dbContext.Set<CognitiveMemorySelfModelProfileRecord>()
            .Where(item => item.ProjectId == query.ProjectId &&
                           item.ModelProfileId == NormalizeModelProfileId(query.ModelProfileId) &&
                           item.RoleKey == NormalizeRoleKey(query.RoleKey) &&
                           item.Status == CognitiveMemorySelfModelStatus.Active)
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    internal static string NormalizeKey(string value)
        => CognitiveMemoryGuard.EnsureText(value, nameof(value)).ToLowerInvariant();

    internal static CognitiveMemoryModelProfileId NormalizeModelProfileId(CognitiveMemoryModelProfileId value)
        => new(NormalizeKey(value.Value));

    internal static CognitiveMemoryRoleKey NormalizeRoleKey(CognitiveMemoryRoleKey value)
        => new(NormalizeKey(value.Value));
}

public sealed class CognitiveMemoryCalibrationHealthService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ICognitiveMemoryScoreGeometryDriver scoreGeometryDriver,
    IClock clock) : ICognitiveMemoryCalibrationHealthService
{
    public async ValueTask<CognitiveMemoryCalibrationEventRecord> RecordOutcomeAsync(
        CognitiveMemoryCalibrationOutcomeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        CognitiveMemoryScoreGuard.EnsureUnitInterval(request.PredictedConfidence, nameof(request.PredictedConfidence));
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var calibrationEvent = new CognitiveMemoryCalibrationEventRecord
        {
            ProjectId = request.ProjectId,
            DomainKey = Normalize(request.DomainKey),
            TaskTypeKey = Normalize(request.TaskTypeKey),
            ModelProfileId = NormalizeModelProfileId(request.ModelProfileId),
            RiskKey = NormalizeRiskKey(request.RiskKey),
            FeaturePatternKey = Normalize(request.FeaturePatternKey),
            ProfileVersion = Normalize(request.ProfileVersion),
            PredictedConfidence = request.PredictedConfidence,
            ActualCorrect = request.ActualCorrect,
            OutcomeKind = request.OutcomeKind,
            ProbeTurnId = request.ProbeTurnId,
            RecallTraceId = request.RecallTraceId,
            ReviewItemId = request.ReviewItemId,
            ProfessorReviewId = request.ProfessorReviewId,
            ObservedAtUtc = now
        };
        dbContext.Add(calibrationEvent);
        await dbContext.SaveChangesAsync(cancellationToken);
        await RecalculateAggregateAsync(dbContext, request, now, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return calibrationEvent;
    }

    public async ValueTask<CognitiveMemoryCalibrationHealthSnapshot?> GetAggregateAsync(
        Guid? projectId,
        string domainKey,
        string taskTypeKey,
        string modelProfileId,
        string riskKey,
        string featurePatternKey,
        string profileVersion,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var aggregate = await FindAggregateQuery(
                dbContext,
                projectId,
                Normalize(domainKey),
                Normalize(taskTypeKey),
                new CognitiveMemoryModelProfileId(Normalize(modelProfileId)),
                new CognitiveMemoryRiskKey(Normalize(riskKey)),
                Normalize(featurePatternKey),
                Normalize(profileVersion))
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (aggregate is null)
        {
            return null;
        }

        var bins = await dbContext.Set<CognitiveMemoryCalibrationBinRecord>()
            .AsNoTracking()
            .Where(item => item.CalibrationAggregateId == aggregate.Id)
            .OrderBy(item => item.BinIndex)
            .ToListAsync(cancellationToken);
        return new CognitiveMemoryCalibrationHealthSnapshot(aggregate, bins);
    }

    private async Task RecalculateAggregateAsync(
        AppDbContext dbContext,
        CognitiveMemoryCalibrationOutcomeRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var domainKey = Normalize(request.DomainKey);
        var taskTypeKey = Normalize(request.TaskTypeKey);
        var modelProfileId = NormalizeModelProfileId(request.ModelProfileId);
        var riskKey = NormalizeRiskKey(request.RiskKey);
        var featurePatternKey = Normalize(request.FeaturePatternKey);
        var profileVersion = Normalize(request.ProfileVersion);
        var events = await dbContext.Set<CognitiveMemoryCalibrationEventRecord>()
            .Where(item => item.ProjectId == request.ProjectId &&
                           item.DomainKey == domainKey &&
                           item.TaskTypeKey == taskTypeKey &&
                           item.ModelProfileId == modelProfileId &&
                           item.RiskKey == riskKey &&
                           item.FeaturePatternKey == featurePatternKey &&
                           item.ProfileVersion == profileVersion)
            .ToListAsync(cancellationToken);
        if (events.Count == 0)
        {
            return;
        }

        var aggregate = await FindAggregateQuery(
                dbContext,
                request.ProjectId,
                domainKey,
                taskTypeKey,
                modelProfileId,
                riskKey,
                featurePatternKey,
                profileVersion)
            .SingleOrDefaultAsync(cancellationToken);
        if (aggregate is null)
        {
            aggregate = new CognitiveMemoryCalibrationAggregateRecord
            {
                ProjectId = request.ProjectId,
                DomainKey = domainKey,
                TaskTypeKey = taskTypeKey,
                ModelProfileId = modelProfileId,
                RiskKey = riskKey,
                FeaturePatternKey = featurePatternKey,
                ProfileVersion = profileVersion
            };
            dbContext.Add(aggregate);
        }

        aggregate.ObservationCount = events.Count;
        aggregate.BrierScore = events.Average(item => Math.Pow(item.PredictedConfidence - (item.ActualCorrect ? 1 : 0), 2));
        aggregate.SignedBias = events.Average(item => item.PredictedConfidence - (item.ActualCorrect ? 1 : 0));
        aggregate.OverconfidenceRate = events.Count(IsOverconfidence) / (double)events.Count;
        aggregate.UnderconfidenceRate = events.Count(IsUnderconfidence) / (double)events.Count;
        aggregate.AbstentionQualityRate = events.Count(item => item.OutcomeKind == CognitiveMemoryCalibrationOutcomeKind.AbstentionAppropriate) / (double)events.Count;
        aggregate.WrongScopeRate = events.Count(item => item.OutcomeKind == CognitiveMemoryCalibrationOutcomeKind.WrongScope) / (double)events.Count;
        aggregate.SourceInsufficientRate = events.Count(item => item.OutcomeKind == CognitiveMemoryCalibrationOutcomeKind.SourceInsufficient) / (double)events.Count;
        aggregate.ExpectedCalibrationError = CalculateExpectedCalibrationError(events);
        var trace = await CognitiveMemoryAdvancedScoring.EvaluateAndPersistAsync(
            dbContext,
            scoreGeometryDriver,
            request.ProjectId,
            CognitiveMemoryScoreOwnerKind.CalibrationAggregate,
            aggregate.Id,
            CognitiveMemoryScoreSpaceKind.CalibrationHealth,
            [
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.OverconfidenceRate, aggregate.OverconfidenceRate),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.UnderconfidenceRate, aggregate.UnderconfidenceRate),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.CalibrationRisk, Math.Clamp(aggregate.ExpectedCalibrationError, 0, 1)),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.AbstentionQuality, aggregate.AbstentionQualityRate),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.WrongScopeRecurrence, aggregate.WrongScopeRate),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.SourceInsufficientRecurrence, aggregate.SourceInsufficientRate)
            ],
            aggregate.OverconfidenceRate >= 0.4 || aggregate.SourceInsufficientRate >= 0.4
                ? CognitiveMemoryScoreProjectionBucket.NeedsReview
                : CognitiveMemoryScoreProjectionBucket.WeakAccept,
            now,
            cancellationToken);
        aggregate.CalibrationScoreEvaluationTraceId = trace.Id.Value;
        aggregate.UpdatedAtUtc = now;

        var oldBins = await dbContext.Set<CognitiveMemoryCalibrationBinRecord>()
            .Where(item => item.CalibrationAggregateId == aggregate.Id)
            .ToListAsync(cancellationToken);
        dbContext.RemoveRange(oldBins);
        foreach (var bin in BuildBins(aggregate.Id, request.ProjectId, events, now))
        {
            dbContext.Add(bin);
        }
    }

    private static IQueryable<CognitiveMemoryCalibrationAggregateRecord> FindAggregateQuery(
        AppDbContext dbContext,
        Guid? projectId,
        string domainKey,
        string taskTypeKey,
        CognitiveMemoryModelProfileId modelProfileId,
        CognitiveMemoryRiskKey riskKey,
        string featurePatternKey,
        string profileVersion)
        => dbContext.Set<CognitiveMemoryCalibrationAggregateRecord>()
            .Where(item => item.ProjectId == projectId &&
                           item.DomainKey == domainKey &&
                           item.TaskTypeKey == taskTypeKey &&
                           item.ModelProfileId == modelProfileId &&
                           item.RiskKey == riskKey &&
                           item.FeaturePatternKey == featurePatternKey &&
                           item.ProfileVersion == profileVersion);

    private static IReadOnlyList<CognitiveMemoryCalibrationBinRecord> BuildBins(
        Guid aggregateId,
        Guid? projectId,
        IReadOnlyList<CognitiveMemoryCalibrationEventRecord> events,
        DateTimeOffset now)
        => Enumerable.Range(0, 10)
            .Select(index =>
            {
                var lower = index / 10d;
                var upper = (index + 1) / 10d;
                var binEvents = events
                    .Where(item => item.PredictedConfidence >= lower &&
                                   (index == 9 ? item.PredictedConfidence <= upper : item.PredictedConfidence < upper))
                    .ToArray();
                return new CognitiveMemoryCalibrationBinRecord
                {
                    CalibrationAggregateId = aggregateId,
                    ProjectId = projectId,
                    BinIndex = index,
                    LowerBound = lower,
                    UpperBound = upper,
                    ObservationCount = binEvents.Length,
                    AveragePredictedConfidence = binEvents.Length == 0 ? 0 : binEvents.Average(item => item.PredictedConfidence),
                    ActualAccuracy = binEvents.Length == 0 ? 0 : binEvents.Count(item => item.ActualCorrect) / (double)binEvents.Length,
                    UpdatedAtUtc = now
                };
            })
            .ToArray();

    private static double CalculateExpectedCalibrationError(IReadOnlyList<CognitiveMemoryCalibrationEventRecord> events)
    {
        var bins = BuildBins(Guid.NewGuid(), null, events, DateTimeOffset.UnixEpoch);
        return bins.Sum(bin =>
            bin.ObservationCount == 0
                ? 0
                : (bin.ObservationCount / (double)events.Count) * Math.Abs(bin.AveragePredictedConfidence - bin.ActualAccuracy));
    }

    private static bool IsOverconfidence(CognitiveMemoryCalibrationEventRecord item)
        => item.OutcomeKind is CognitiveMemoryCalibrationOutcomeKind.IncorrectHighConfidence or CognitiveMemoryCalibrationOutcomeKind.HumanReviewRejected or CognitiveMemoryCalibrationOutcomeKind.ProfessorDisagreed ||
           item.PredictedConfidence >= 0.7 && !item.ActualCorrect;

    private static bool IsUnderconfidence(CognitiveMemoryCalibrationEventRecord item)
        => item.OutcomeKind == CognitiveMemoryCalibrationOutcomeKind.CorrectLowConfidence ||
           item.PredictedConfidence <= 0.4 && item.ActualCorrect;

    private static string Normalize(string value)
        => CognitiveMemorySelfModelStore.NormalizeKey(value);

    private static CognitiveMemoryModelProfileId NormalizeModelProfileId(CognitiveMemoryModelProfileId value)
        => new(Normalize(value.Value));

    private static CognitiveMemoryRiskKey NormalizeRiskKey(CognitiveMemoryRiskKey value)
        => new(Normalize(value.Value));
}

public sealed class CognitiveMemorySelfRegulationOrchestrator(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ICognitiveMemorySelfModelStore selfModelStore,
    ICognitiveMemoryCalibrationHealthService calibrationHealthService,
    ICognitiveMemoryScoreGeometryDriver scoreGeometryDriver,
    IClock clock) : ICognitiveMemorySelfRegulationOrchestrator
{
    public async ValueTask<CognitiveMemorySelfRegulationAssessmentResult> AssessAsync(
        CognitiveMemorySelfRegulationAssessmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var query = new CognitiveMemorySelfModelQuery(
            request.ProjectId,
            request.ModelProfileId,
            request.RoleKey,
            request.DomainKey,
            request.TaskTypeKey);
        await selfModelStore.EnsureSeedProfileAsync(query, cancellationToken);
        var selfModel = await selfModelStore.LoadAsync(query, cancellationToken);
        var calibration = await calibrationHealthService.GetAggregateAsync(
            request.ProjectId,
            request.DomainKey,
            request.TaskTypeKey,
            request.ModelProfileId.Value,
            request.RiskLevel.ToString(),
            "general",
            selfModel.SelfModel.ProfileVersion,
            cancellationToken);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var competenceFit = selfModel.Competence?.CompetenceLevel switch
        {
            CognitiveMemoryCompetenceLevel.Strong => 0.9,
            CognitiveMemoryCompetenceLevel.Reliable => 0.75,
            CognitiveMemoryCompetenceLevel.Developing => 0.55,
            CognitiveMemoryCompetenceLevel.Weak => 0.25,
            _ => 0.45
        };
        var calibrationFit = calibration is null
            ? 0.45
            : 1 - Math.Clamp(calibration.Aggregate.OverconfidenceRate + calibration.Aggregate.SourceInsufficientRate, 0, 1);
        var knownFailurePressure = selfModel.FailurePatterns.Count > 0 && request.SourceSufficiency < 0.5
            ? 0.75
            : 0.1;
        var state = ClassifyState(request, calibration?.Aggregate, competenceFit, knownFailurePressure);
        var triggers = BuildHumilityTriggers(request, calibration?.Aggregate, competenceFit, knownFailurePressure);
        var requiredOperations = BuildRequiredOperations(state, triggers);
        var warnings = triggers.Select(trigger => trigger.ToString()).Distinct(StringComparer.Ordinal).ToArray();
        var assessmentTrace = await CognitiveMemoryAdvancedScoring.EvaluateAndPersistAsync(
            dbContext,
            scoreGeometryDriver,
            request.ProjectId,
            CognitiveMemoryScoreOwnerKind.SelfRegulationAssessment,
            null,
            CognitiveMemoryScoreSpaceKind.SelfRegulationAssessment,
            [
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.EvidenceStrength, request.SourceSufficiency),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.EvidenceCoverage, request.EvidenceCoverage),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.SourceReliability, request.PolicyContext.AllowRestrictedContent ? 0.55 : 0.8),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.ContextFit, request.ContextFit),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.ContradictionPressure, request.ContradictionPressure),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.RedactionPressure, request.RedactionPressure),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.CognitiveLoad, request.CognitiveLoad),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.HistoricalCalibrationFit, calibrationFit),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.DomainCompetenceFit, competenceFit),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.KnownFailurePatternSimilarity, knownFailurePressure),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.ConsequenceRisk, request.HighImpact ? 0.9 : RiskToUnit(request.RiskLevel))
            ],
            state is CognitiveMemorySelfRegulationStateKind.HighRiskUnverified or CognitiveMemorySelfRegulationStateKind.ProfessorReviewNeeded
                ? CognitiveMemoryScoreProjectionBucket.NeedsReview
                : CognitiveMemoryScoreProjectionBucket.WeakAccept,
            now,
            cancellationToken);
        var assessment = new CognitiveMemorySelfRegulationAssessmentRecord
        {
            ProjectId = request.ProjectId,
            SelfModelProfileId = selfModel.SelfModel.Id,
            DomainCompetenceProfileId = selfModel.Competence?.Id,
            CalibrationAggregateId = calibration?.Aggregate.Id,
            RecallTraceId = request.RecallTraceId,
            WorkspaceFrameId = request.WorkspaceFrameId,
            AttentionDecisionId = request.AttentionDecisionId,
            ActorId = request.ActorId,
            ModelProfileId = CognitiveMemorySelfModelStore.NormalizeModelProfileId(request.ModelProfileId),
            DomainKey = CognitiveMemorySelfModelStore.NormalizeKey(request.DomainKey),
            TaskTypeKey = CognitiveMemorySelfModelStore.NormalizeKey(request.TaskTypeKey),
            State = state,
            AssessmentScoreEvaluationTraceId = assessmentTrace.Id.Value,
            AssessmentBucket = assessmentTrace.ScalarProjection?.Bucket ?? CognitiveMemoryScoreProjectionBucket.Unknown,
            DisplayAssessmentScore = assessmentTrace.ScalarProjection?.DisplayScore,
            WarningsJson = JsonSerializer.Serialize(warnings, CognitiveMemoryAdvancedJson.Options),
            RequiredOperationsJson = JsonSerializer.Serialize(requiredOperations, CognitiveMemoryAdvancedJson.Options),
            AlgorithmVersion = "self-regulation-v1",
            CreatedAtUtc = now
        };
        dbContext.Add(assessment);
        await dbContext.SaveChangesAsync(cancellationToken);

        var triggerRecords = triggers
            .Select(trigger => new CognitiveMemoryHumilityTriggerRecord
            {
                SelfRegulationAssessmentId = assessment.Id,
                ProjectId = request.ProjectId,
                TriggerKind = trigger,
                Reason = trigger.ToString(),
                CreatedAtUtc = now
            })
            .ToList();
        dbContext.AddRange(triggerRecords);
        var reinforcements = BuildReinforcements(request, calibration?.Aggregate)
            .Select(reinforcement => new CognitiveMemoryConfidenceReinforcementRecord
            {
                SelfRegulationAssessmentId = assessment.Id,
                ProjectId = request.ProjectId,
                ReinforcementKind = reinforcement,
                Reason = reinforcement.ToString(),
                CreatedAtUtc = now
            })
            .ToList();
        dbContext.AddRange(reinforcements);
        var postureKind = SelectPosture(state, requiredOperations);
        var postureTrace = await CognitiveMemoryAdvancedScoring.EvaluateAndPersistAsync(
            dbContext,
            scoreGeometryDriver,
            request.ProjectId,
            CognitiveMemoryScoreOwnerKind.SelfRegulationAssessment,
            assessment.Id,
            CognitiveMemoryScoreSpaceKind.AnswerPosture,
            [
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.SourceSufficiency, request.SourceSufficiency),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.ContextFit, request.ContextFit),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.HistoricalCalibrationFit, calibrationFit),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.DomainCompetenceFit, competenceFit),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.HumilityTriggerPressure, Math.Clamp(triggers.Count / 4d, 0, 1)),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.ConfidenceReinforcementPressure, Math.Clamp(reinforcements.Count / 4d, 0, 1)),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.ProfessorReviewValue, postureKind == CognitiveMemoryAnswerPostureKind.ProfessorReviewRequired ? 0.9 : 0.1)
            ],
            postureKind is CognitiveMemoryAnswerPostureKind.Abstain or CognitiveMemoryAnswerPostureKind.ProfessorReviewRequired
                ? CognitiveMemoryScoreProjectionBucket.NeedsReview
                : CognitiveMemoryScoreProjectionBucket.WeakAccept,
            now,
            cancellationToken);
        var posture = new CognitiveMemoryAnswerPostureDecisionRecord
        {
            ProjectId = request.ProjectId,
            SelfRegulationAssessmentId = assessment.Id,
            Posture = postureKind,
            PostureScoreEvaluationTraceId = postureTrace.Id.Value,
            PostureBucket = postureTrace.ScalarProjection?.Bucket ?? CognitiveMemoryScoreProjectionBucket.Unknown,
            RequiredOperationsJson = JsonSerializer.Serialize(requiredOperations, CognitiveMemoryAdvancedJson.Options),
            WarningsJson = JsonSerializer.Serialize(warnings, CognitiveMemoryAdvancedJson.Options),
            Reason = $"State {state} selected posture {postureKind}.",
            CreatedAtUtc = now
        };
        dbContext.Add(posture);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (request.RecallTraceId is { } recallTraceId)
        {
            var recallTrace = await dbContext.Set<CognitiveMemoryRecallTraceRecord>()
                .SingleOrDefaultAsync(item => item.Id == recallTraceId, cancellationToken);
            if (recallTrace is not null)
            {
                recallTrace.SelfRegulationAssessmentId = assessment.Id;
                recallTrace.AnswerPostureDecisionId = posture.Id;
            }
        }

        if (request.AttentionDecisionId is { } attentionDecisionId)
        {
            var attentionDecision = await dbContext.Set<CognitiveMemoryAttentionDecisionRecord>()
                .SingleOrDefaultAsync(item => item.Id == attentionDecisionId, cancellationToken);
            if (attentionDecision is not null)
            {
                attentionDecision.SelfRegulationAssessmentId = assessment.Id;
                attentionDecision.AnswerPostureDecisionId = posture.Id;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new CognitiveMemorySelfRegulationAssessmentResult(assessment, posture, triggerRecords, reinforcements);
    }

    private static CognitiveMemorySelfRegulationStateKind ClassifyState(
        CognitiveMemorySelfRegulationAssessmentRequest request,
        CognitiveMemoryCalibrationAggregateRecord? aggregate,
        double competenceFit,
        double knownFailurePressure)
    {
        if (request.RedactionPressure >= 0.75)
        {
            return CognitiveMemorySelfRegulationStateKind.AccessLimited;
        }

        if (request.HighImpact && (request.SourceSufficiency < 0.65 || competenceFit < 0.5))
        {
            return CognitiveMemorySelfRegulationStateKind.HighRiskUnverified;
        }

        if (request.ContradictionPressure >= 0.65 || knownFailurePressure >= 0.7)
        {
            return CognitiveMemorySelfRegulationStateKind.ProfessorReviewNeeded;
        }

        if (aggregate?.OverconfidenceRate >= 0.4)
        {
            return CognitiveMemorySelfRegulationStateKind.Overconfident;
        }

        if (aggregate?.UnderconfidenceRate >= 0.4)
        {
            return CognitiveMemorySelfRegulationStateKind.Underconfident;
        }

        if (request.SourceSufficiency < 0.45)
        {
            return CognitiveMemorySelfRegulationStateKind.SourcePoor;
        }

        return request.ContextFit < 0.5
            ? CognitiveMemorySelfRegulationStateKind.Exploratory
            : CognitiveMemorySelfRegulationStateKind.Calibrated;
    }

    private static IReadOnlyList<CognitiveMemoryHumilityTriggerKind> BuildHumilityTriggers(
        CognitiveMemorySelfRegulationAssessmentRequest request,
        CognitiveMemoryCalibrationAggregateRecord? aggregate,
        double competenceFit,
        double knownFailurePressure)
    {
        var triggers = new List<CognitiveMemoryHumilityTriggerKind>();
        if (request.SourceSufficiency < 0.45 && request.RiskLevel == CognitiveMemoryRiskLevel.High)
        {
            triggers.Add(CognitiveMemoryHumilityTriggerKind.SourcePoorHighRisk);
        }

        if (request.ContradictionPressure >= 0.6)
        {
            triggers.Add(CognitiveMemoryHumilityTriggerKind.ContradictionPressure);
        }

        if (knownFailurePressure >= 0.7)
        {
            triggers.Add(CognitiveMemoryHumilityTriggerKind.WrongScopePattern);
        }

        if (request.RecentCorrection)
        {
            triggers.Add(CognitiveMemoryHumilityTriggerKind.RecentCorrection);
        }

        if (competenceFit < 0.4)
        {
            triggers.Add(CognitiveMemoryHumilityTriggerKind.WeakDomain);
        }

        if (request.HighImpact && request.RiskLevel == CognitiveMemoryRiskLevel.High)
        {
            triggers.Add(CognitiveMemoryHumilityTriggerKind.HighImpactUnvalidatedProcedure);
        }

        if (request.RedactionPressure >= 0.7)
        {
            triggers.Add(CognitiveMemoryHumilityTriggerKind.RedactionPreventsProof);
        }

        if (request.CognitiveLoad >= 0.8)
        {
            triggers.Add(CognitiveMemoryHumilityTriggerKind.CognitiveLoadSaturation);
        }

        if (aggregate?.SourceInsufficientRate >= 0.4)
        {
            triggers.Add(CognitiveMemoryHumilityTriggerKind.GeneratedSummaryPrimarySupport);
        }

        return triggers.Distinct().ToArray();
    }

    private static IReadOnlyList<CognitiveMemoryConfidenceReinforcementKind> BuildReinforcements(
        CognitiveMemorySelfRegulationAssessmentRequest request,
        CognitiveMemoryCalibrationAggregateRecord? aggregate)
    {
        var reinforcements = new List<CognitiveMemoryConfidenceReinforcementKind>();
        if (request.SourceSufficiency >= 0.75 && request.EvidenceCoverage >= 0.75)
        {
            reinforcements.Add(CognitiveMemoryConfidenceReinforcementKind.IndependentSourcesAgree);
        }

        if (aggregate is not null && aggregate.ObservationCount >= 3 && aggregate.OverconfidenceRate < 0.25)
        {
            reinforcements.Add(CognitiveMemoryConfidenceReinforcementKind.RegressionPassed);
        }

        return reinforcements;
    }

    private static IReadOnlyList<CognitiveMemoryRequiredOperationKind> BuildRequiredOperations(
        CognitiveMemorySelfRegulationStateKind state,
        IReadOnlyList<CognitiveMemoryHumilityTriggerKind> triggers)
        => state switch
        {
            CognitiveMemorySelfRegulationStateKind.AccessLimited => [CognitiveMemoryRequiredOperationKind.SourceAudit, CognitiveMemoryRequiredOperationKind.Abstain],
            CognitiveMemorySelfRegulationStateKind.HighRiskUnverified => [CognitiveMemoryRequiredOperationKind.HumanReview],
            CognitiveMemorySelfRegulationStateKind.ProfessorReviewNeeded => [CognitiveMemoryRequiredOperationKind.ProfessorReview],
            CognitiveMemorySelfRegulationStateKind.SourcePoor => [CognitiveMemoryRequiredOperationKind.SourceAudit],
            CognitiveMemorySelfRegulationStateKind.Overconfident => [CognitiveMemoryRequiredOperationKind.Probe],
            _ => triggers.Count > 0 ? [CognitiveMemoryRequiredOperationKind.Clarify] : []
        };

    private static CognitiveMemoryAnswerPostureKind SelectPosture(
        CognitiveMemorySelfRegulationStateKind state,
        IReadOnlyList<CognitiveMemoryRequiredOperationKind> requiredOperations)
    {
        if (requiredOperations.Contains(CognitiveMemoryRequiredOperationKind.Abstain))
        {
            return CognitiveMemoryAnswerPostureKind.Abstain;
        }

        if (requiredOperations.Contains(CognitiveMemoryRequiredOperationKind.ProfessorReview))
        {
            return CognitiveMemoryAnswerPostureKind.ProfessorReviewRequired;
        }

        if (requiredOperations.Contains(CognitiveMemoryRequiredOperationKind.HumanReview))
        {
            return CognitiveMemoryAnswerPostureKind.HumanReviewRequired;
        }

        if (requiredOperations.Contains(CognitiveMemoryRequiredOperationKind.SourceAudit))
        {
            return CognitiveMemoryAnswerPostureKind.SourceAuditRequired;
        }

        if (requiredOperations.Contains(CognitiveMemoryRequiredOperationKind.Probe))
        {
            return CognitiveMemoryAnswerPostureKind.ProbeRequired;
        }

        return state == CognitiveMemorySelfRegulationStateKind.Calibrated
            ? CognitiveMemoryAnswerPostureKind.Direct
            : CognitiveMemoryAnswerPostureKind.Caveated;
    }

    private static double RiskToUnit(CognitiveMemoryRiskLevel riskLevel)
        => riskLevel switch
        {
            CognitiveMemoryRiskLevel.High => 0.9,
            CognitiveMemoryRiskLevel.Medium => 0.55,
            _ => 0.2
        };
}

public sealed class CognitiveMemoryProfessorReviewService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ICognitiveMemoryScoreGeometryDriver scoreGeometryDriver,
    IClock clock) : ICognitiveMemoryProfessorReviewService
{
    public async ValueTask<CognitiveMemoryProfessorReviewRecord> RequestReviewAsync(
        CognitiveMemoryProfessorReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var trace = await CognitiveMemoryAdvancedScoring.EvaluateAndPersistAsync(
            dbContext,
            scoreGeometryDriver,
            request.ProjectId,
            CognitiveMemoryScoreOwnerKind.ProfessorReview,
            null,
            CognitiveMemoryScoreSpaceKind.ProfessorReviewRouting,
            [
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.ProfessorReviewValue, 0.85),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.ConsequenceRisk, request.PolicyContext.RiskLevel == CognitiveMemoryRiskLevel.High ? 0.9 : 0.45),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.SourceSufficiency, string.IsNullOrWhiteSpace(request.ContextSummary) ? 0.85 : 0.35),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.AccessPolicyRisk, request.PolicyContext.AllowRestrictedContent ? 0.45 : 0.1),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.RedactionPressure, request.PolicyContext.AllowRestrictedContent ? 0.35 : 0.1)
            ],
            CognitiveMemoryScoreProjectionBucket.NeedsReview,
            now,
            cancellationToken);
        var review = new CognitiveMemoryProfessorReviewRecord
        {
            ProjectId = request.ProjectId,
            ReviewMode = request.ReviewMode,
            Status = CognitiveMemoryProfessorReviewStatus.Requested,
            RequestedByActorId = CognitiveMemoryGuard.EnsureText(request.RequestedByActorId, nameof(request.RequestedByActorId)),
            ModelProfileId = request.ModelProfileId,
            PromptProfileVersion = CognitiveMemoryGuard.EnsureText(request.PromptProfileVersion, nameof(request.PromptProfileVersion)),
            PolicyProfileId = request.PolicyContext.PolicyProfileId.Value,
            SelfRegulationAssessmentId = request.SelfRegulationAssessmentId,
            AnswerPostureDecisionId = request.AnswerPostureDecisionId,
            RoutingScoreEvaluationTraceId = trace.Id.Value,
            InputSummary = CognitiveMemoryGuard.EnsureText(request.InputSummary, nameof(request.InputSummary)),
            ContextSummary = request.PolicyContext.AllowRestrictedContent ? request.ContextSummary.Trim() : RedactRestrictedContext(request.ContextSummary),
            OutputHash = CognitiveMemoryHash.FromUtf8("requested").Value,
            CreatedAtUtc = now
        };
        dbContext.Add(review);
        foreach (var suggestionKind in request.RequestedSuggestionKinds.DefaultIfEmpty(CognitiveMemoryProfessorSuggestionKind.NoAction))
        {
            dbContext.Add(new CognitiveMemoryProfessorReviewActionRecord
            {
                ProfessorReviewId = review.Id,
                ProjectId = request.ProjectId,
                SuggestionKind = suggestionKind,
                Summary = "Requested professor review suggestion.",
                CreatedAtUtc = now
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return review;
    }

    public async ValueTask<CognitiveMemoryProfessorReviewRecord> CompleteReviewAsync(
        Guid reviewId,
        string critique,
        string missingEvidence,
        CognitiveMemoryAnswerPostureKind recommendedPosture,
        IReadOnlyList<CognitiveMemoryProfessorSuggestionKind> suggestionKinds,
        CancellationToken cancellationToken = default)
    {
        if (reviewId == Guid.Empty)
        {
            throw new ArgumentException("Professor review id must not be empty.", nameof(reviewId));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var review = await dbContext.Set<CognitiveMemoryProfessorReviewRecord>()
            .SingleOrDefaultAsync(item => item.Id == reviewId, cancellationToken)
            ?? throw new InvalidOperationException($"Professor review '{reviewId:D}' was not found.");
        var now = clock.GetUtcNow();
        review.Status = CognitiveMemoryProfessorReviewStatus.Completed;
        review.Critique = CognitiveMemoryGuard.EnsureText(critique, nameof(critique));
        review.MissingEvidence = missingEvidence.Trim();
        review.RecommendedPosture = recommendedPosture;
        review.OutputHash = CognitiveMemoryHash.FromUtf8($"{review.Critique}\n{review.MissingEvidence}\n{recommendedPosture}").Value;
        review.CompletedAtUtc = now;

        foreach (var suggestionKind in suggestionKinds.DefaultIfEmpty(CognitiveMemoryProfessorSuggestionKind.NoAction).Distinct())
        {
            var action = new CognitiveMemoryProfessorReviewActionRecord
            {
                ProfessorReviewId = review.Id,
                ProjectId = review.ProjectId,
                SuggestionKind = suggestionKind,
                Summary = $"Professor review suggested {suggestionKind}.",
                CreatedAtUtc = now
            };
            if (suggestionKind == CognitiveMemoryProfessorSuggestionKind.ReviewItem)
            {
                var reviewItem = new CognitiveMemoryReviewItemRecord
                {
                    ProjectId = review.ProjectId,
                    ReviewKind = CognitiveMemoryReviewKind.GeneratedMemory,
                    SubjectKind = CognitiveMemoryReviewSubjectKind.Run,
                    SubjectId = review.Id,
                    Status = CognitiveMemoryReviewStatus.Pending,
                    RiskLevel = CognitiveMemoryRiskLevel.Medium,
                    ReasonCode = "professor-review",
                    ReasonText = "Professor review produced a governed review action.",
                    CreatedAtUtc = now
                };
                dbContext.Add(reviewItem);
                action.CreatedReviewItemId = reviewItem.Id;
            }

            if (suggestionKind == CognitiveMemoryProfessorSuggestionKind.LearningProposal && review.ProjectId is { } projectId)
            {
                var region = await EnsureKnowledgeRegionAsync(dbContext, projectId, "professor-review", now, cancellationToken);
                var gap = new CognitiveMemoryKnowledgeGapRecord
                {
                    ProjectId = projectId,
                    KnowledgeRegionId = region.Id,
                    GapKind = CognitiveMemoryKnowledgeGapKind.ProfessorSuggestedExpansion,
                    Summary = review.MissingEvidence,
                    EvidenceRefsJson = "[]",
                    CreatedAtUtc = now
                };
                dbContext.Add(gap);
                var proposal = new CognitiveMemoryLearningProposalRecord
                {
                    ProjectId = projectId,
                    KnowledgeGapId = gap.Id,
                    Status = CognitiveMemoryLearningProposalStatus.PendingApproval,
                    Title = "Professor review learning expansion",
                    Explanation = review.Critique,
                    EvidenceRefsJson = "[]",
                    Risks = new CognitiveMemoryRiskNotes("Professor review is challenge input, not source truth."),
                    AcceptanceCriteria = "Learning output must cite source refs and pass review.",
                    NeedScoreEvaluationTraceId = review.RoutingScoreEvaluationTraceId,
                    NeedBucket = CognitiveMemoryScoreProjectionBucket.NeedsReview,
                    CreatedAtUtc = now
                };
                dbContext.Add(proposal);
                action.CreatedLearningProposalId = proposal.Id;
            }

            dbContext.Add(action);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return review;
    }

    private static string RedactRestrictedContext(string value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : "[redacted by cognitive-memory professor-review access policy]";

    private static async Task<CognitiveMemoryKnowledgeRegionRecord> EnsureKnowledgeRegionAsync(
        AppDbContext dbContext,
        Guid projectId,
        string regionKey,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var region = await dbContext.Set<CognitiveMemoryKnowledgeRegionRecord>()
            .SingleOrDefaultAsync(
                item => item.ProjectId == projectId &&
                        item.RegionKind == CognitiveMemoryKnowledgeRegionKind.Domain &&
                        item.RegionKey == regionKey,
                cancellationToken);
        if (region is not null)
        {
            return region;
        }

        region = new CognitiveMemoryKnowledgeRegionRecord
        {
            ProjectId = projectId,
            RegionKind = CognitiveMemoryKnowledgeRegionKind.Domain,
            RegionKey = regionKey,
            DisplayName = "Professor review",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        dbContext.Add(region);
        return region;
    }
}

public sealed class CognitiveMemoryAnswerGateService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ICognitiveMemoryScoreGeometryDriver scoreGeometryDriver,
    IClock clock) : ICognitiveMemoryAnswerGateService
{
    public async ValueTask<CognitiveMemoryAnswerGateDecisionRecord> DecideAsync(
        CognitiveMemoryAnswerGateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var posture = request.AnswerPostureDecisionId is { } postureId
            ? await dbContext.Set<CognitiveMemoryAnswerPostureDecisionRecord>()
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == postureId, cancellationToken)
            : null;
        var decisionKind = SelectDecision(request, posture);
        var requiredOperations = RequiredOperationsFor(decisionKind);
        var warnings = BuildAnswerWarnings(request, posture, decisionKind);
        var trace = await CognitiveMemoryAdvancedScoring.EvaluateAndPersistAsync(
            dbContext,
            scoreGeometryDriver,
            request.ProjectId,
            CognitiveMemoryScoreOwnerKind.AnswerGateDecision,
            null,
            CognitiveMemoryScoreSpaceKind.AnswerGate,
            [
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.SourceSufficiency, request.SourceSufficiency),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.ContextFit, request.ContextFit),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.EvidenceSupport, request.EvidenceSupport),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.ContradictionPressure, request.ContradictionPressure),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.StalenessPressure, request.StalenessPressure),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.RedactionPressure, request.RedactionPressure),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.CalibrationRisk, request.CalibrationRisk),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.RiskImpact, RiskToUnit(request.RiskLevel)),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.ProcedureMaturity, request.ProcedureUnvalidated ? 0.1 : 0.8),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.AccessPolicyRisk, request.PolicyContext.AllowRestrictedContent ? 0.35 : 0.1)
            ],
            decisionKind == CognitiveMemoryAnswerGateDecisionKind.Answer
                ? CognitiveMemoryScoreProjectionBucket.WeakAccept
                : CognitiveMemoryScoreProjectionBucket.NeedsReview,
            now,
            cancellationToken);
        var decision = new CognitiveMemoryAnswerGateDecisionRecord
        {
            ProjectId = request.ProjectId,
            RecallTraceId = request.RecallTraceId,
            SelfRegulationAssessmentId = request.SelfRegulationAssessmentId,
            AnswerPostureDecisionId = request.AnswerPostureDecisionId,
            ProfessorReviewId = request.ProfessorReviewId,
            DecisionKind = decisionKind,
            ScoreEvaluationTraceId = trace.Id.Value,
            DecisionBucket = trace.ScalarProjection?.Bucket ?? CognitiveMemoryScoreProjectionBucket.Unknown,
            DisplayConfidenceProjection = trace.ScalarProjection?.DisplayScore,
            WarningsJson = JsonSerializer.Serialize(warnings, CognitiveMemoryAdvancedJson.Options),
            RequiredOperationsJson = JsonSerializer.Serialize(requiredOperations, CognitiveMemoryAdvancedJson.Options),
            Reason = $"Answer gate selected {decisionKind}.",
            DraftAnswerSummary = request.DraftAnswerSummary.Trim(),
            CreatedAtUtc = now
        };
        dbContext.Add(decision);
        if (request.RecallTraceId is { } recallTraceId)
        {
            var recallTrace = await dbContext.Set<CognitiveMemoryRecallTraceRecord>()
                .SingleOrDefaultAsync(item => item.Id == recallTraceId, cancellationToken);
            if (recallTrace is not null)
            {
                recallTrace.AnswerGateDecisionId = decision.Id;
                recallTrace.SelfRegulationAssessmentId ??= request.SelfRegulationAssessmentId;
                recallTrace.AnswerPostureDecisionId ??= request.AnswerPostureDecisionId;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return decision;
    }

    private static CognitiveMemoryAnswerGateDecisionKind SelectDecision(
        CognitiveMemoryAnswerGateRequest request,
        CognitiveMemoryAnswerPostureDecisionRecord? posture)
    {
        if (posture?.Posture == CognitiveMemoryAnswerPostureKind.Abstain ||
            request.RedactionPressure >= 0.85)
        {
            return CognitiveMemoryAnswerGateDecisionKind.Abstain;
        }

        if (posture?.Posture == CognitiveMemoryAnswerPostureKind.ProfessorReviewRequired ||
            request.ProfessorReviewRequired && request.ProfessorReviewId is null)
        {
            return CognitiveMemoryAnswerGateDecisionKind.ProfessorReview;
        }

        if (posture?.Posture == CognitiveMemoryAnswerPostureKind.HumanReviewRequired ||
            request.RiskLevel == CognitiveMemoryRiskLevel.High && (request.SourceSufficiency < 0.65 || request.ProcedureUnvalidated))
        {
            return CognitiveMemoryAnswerGateDecisionKind.Review;
        }

        if (posture?.Posture == CognitiveMemoryAnswerPostureKind.ProbeRequired)
        {
            return CognitiveMemoryAnswerGateDecisionKind.Probe;
        }

        if (posture?.Posture == CognitiveMemoryAnswerPostureKind.SourceAuditRequired ||
            request.SourceSufficiency < 0.45)
        {
            return CognitiveMemoryAnswerGateDecisionKind.SourceAudit;
        }

        if (posture?.Posture == CognitiveMemoryAnswerPostureKind.ClarifyFirst ||
            request.ContextFit < 0.45)
        {
            return CognitiveMemoryAnswerGateDecisionKind.Clarify;
        }

        if (request.ContradictionPressure >= 0.55 || request.StalenessPressure >= 0.65 || request.CalibrationRisk >= 0.65)
        {
            return CognitiveMemoryAnswerGateDecisionKind.Warn;
        }

        return CognitiveMemoryAnswerGateDecisionKind.Answer;
    }

    private static IReadOnlyList<CognitiveMemoryRequiredOperationKind> RequiredOperationsFor(CognitiveMemoryAnswerGateDecisionKind decisionKind)
        => decisionKind switch
        {
            CognitiveMemoryAnswerGateDecisionKind.Clarify => [CognitiveMemoryRequiredOperationKind.Clarify],
            CognitiveMemoryAnswerGateDecisionKind.SourceAudit => [CognitiveMemoryRequiredOperationKind.SourceAudit],
            CognitiveMemoryAnswerGateDecisionKind.Probe => [CognitiveMemoryRequiredOperationKind.Probe],
            CognitiveMemoryAnswerGateDecisionKind.Review => [CognitiveMemoryRequiredOperationKind.HumanReview],
            CognitiveMemoryAnswerGateDecisionKind.ProfessorReview => [CognitiveMemoryRequiredOperationKind.ProfessorReview],
            CognitiveMemoryAnswerGateDecisionKind.LearningRequest => [CognitiveMemoryRequiredOperationKind.LearningProposal],
            CognitiveMemoryAnswerGateDecisionKind.Abstain => [CognitiveMemoryRequiredOperationKind.Abstain],
            _ => []
        };

    private static IReadOnlyList<string> BuildAnswerWarnings(
        CognitiveMemoryAnswerGateRequest request,
        CognitiveMemoryAnswerPostureDecisionRecord? posture,
        CognitiveMemoryAnswerGateDecisionKind decisionKind)
    {
        var warnings = new List<string>();
        if (request.SourceSufficiency < 0.65)
        {
            warnings.Add("source-sufficiency-limited");
        }

        if (request.ContradictionPressure >= 0.5)
        {
            warnings.Add("contradiction-risk");
        }

        if (request.RedactionPressure >= 0.5)
        {
            warnings.Add("redaction-limited");
        }

        if (posture is not null && decisionKind != CognitiveMemoryAnswerGateDecisionKind.Answer)
        {
            warnings.Add($"posture:{posture.Posture}");
        }

        return warnings;
    }

    private static double RiskToUnit(CognitiveMemoryRiskLevel riskLevel)
        => riskLevel switch
        {
            CognitiveMemoryRiskLevel.High => 0.9,
            CognitiveMemoryRiskLevel.Medium => 0.55,
            _ => 0.2
        };
}

public sealed class CognitiveMemoryEpistemicDriveService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ICognitiveMemoryScoreGeometryDriver scoreGeometryDriver,
    IClock clock) : ICognitiveMemoryEpistemicDriveService
{
    public async ValueTask<IReadOnlyList<CognitiveMemoryLearningProposalRecord>> ScanAsync(
        CognitiveMemoryEpistemicScanRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var proposals = new List<CognitiveMemoryLearningProposalRecord>();
        var answerGateGaps = await dbContext.Set<CognitiveMemoryAnswerGateDecisionRecord>()
            .AsNoTracking()
            .Where(item => item.ProjectId == request.ProjectId &&
                           item.DecisionKind != CognitiveMemoryAnswerGateDecisionKind.Answer &&
                           item.DecisionKind != CognitiveMemoryAnswerGateDecisionKind.Warn)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken);
        foreach (var answerGate in answerGateGaps)
        {
            proposals.Add(await CreateProposalAsync(
                dbContext,
                request.ProjectId,
                "answer-gate",
                CognitiveMemoryKnowledgeGapKind.RepeatedAbstention,
                $"Answer gate required {answerGate.DecisionKind}",
                answerGate.Reason,
                0.8,
                0.7,
                now,
                cancellationToken));
        }

        var calibrationGaps = await dbContext.Set<CognitiveMemoryCalibrationAggregateRecord>()
            .AsNoTracking()
            .Where(item => item.ProjectId == request.ProjectId &&
                           (item.OverconfidenceRate >= 0.4 ||
                            item.SourceInsufficientRate >= 0.4 ||
                            item.WrongScopeRate >= 0.4))
            .Take(20)
            .ToListAsync(cancellationToken);
        foreach (var aggregate in calibrationGaps)
        {
            proposals.Add(await CreateProposalAsync(
                dbContext,
                request.ProjectId,
                aggregate.DomainKey,
                CognitiveMemoryKnowledgeGapKind.PoorCalibration,
                $"Calibration gap in {aggregate.DomainKey}/{aggregate.TaskTypeKey}",
                "Repeated overconfidence, wrong-scope, or source-insufficient outcomes need learning/probing.",
                Math.Clamp(aggregate.OverconfidenceRate + aggregate.SourceInsufficientRate + aggregate.WrongScopeRate, 0, 1),
                Math.Clamp(aggregate.SourceInsufficientRate + aggregate.WrongScopeRate, 0, 1),
                now,
                cancellationToken));
        }

        proposals.AddRange(await CreateSourceCoverageProposalsAsync(
            dbContext,
            request.ProjectId,
            now,
            cancellationToken));

        await dbContext.SaveChangesAsync(cancellationToken);
        return proposals;
    }

    public async ValueTask<CognitiveMemoryLearningProposalRecord> DecideProposalAsync(
        Guid proposalId,
        CognitiveMemoryLearningProposalStatus decision,
        string actorId,
        string notes,
        CancellationToken cancellationToken = default)
    {
        if (proposalId == Guid.Empty)
        {
            throw new ArgumentException("Learning proposal id must not be empty.", nameof(proposalId));
        }

        if (decision is CognitiveMemoryLearningProposalStatus.Draft or CognitiveMemoryLearningProposalStatus.PendingApproval)
        {
            throw new ArgumentException("Learning proposal decision must be terminal or approval-like.", nameof(decision));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var proposal = await dbContext.Set<CognitiveMemoryLearningProposalRecord>()
            .SingleOrDefaultAsync(item => item.Id == proposalId, cancellationToken)
            ?? throw new InvalidOperationException($"Learning proposal '{proposalId:D}' was not found.");
        var now = clock.GetUtcNow();
        proposal.Status = decision;
        proposal.DecidedByActorId = CognitiveMemoryGuard.EnsureText(actorId, nameof(actorId));
        proposal.DecisionNotes = notes.Trim();
        proposal.DecidedAtUtc = now;
        if (decision == CognitiveMemoryLearningProposalStatus.Approved)
        {
            dbContext.Add(new CognitiveMemoryLearningTaskRecord
            {
                ProjectId = proposal.ProjectId,
                LearningProposalId = proposal.Id,
                Status = CognitiveMemoryLearningTaskStatus.Planned,
                WorkflowExecutorKey = CognitiveMemoryWorkflowExecutorIds.LearningProposal.Value,
                ApprovalActorId = actorId.Trim(),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return proposal;
    }

    private async Task<CognitiveMemoryLearningProposalRecord> CreateProposalAsync(
        AppDbContext dbContext,
        Guid projectId,
        string regionKey,
        CognitiveMemoryKnowledgeGapKind gapKind,
        string title,
        string explanation,
        double missingKnowledgePressure,
        double sourceWeakness,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        string evidenceRefsJson = "[]",
        CognitiveMemoryCoverageState? coverageStateOverride = null,
        int sourceEvidenceCount = 0)
    {
        var region = await dbContext.Set<CognitiveMemoryKnowledgeRegionRecord>()
            .SingleOrDefaultAsync(
                item => item.ProjectId == projectId &&
                        item.RegionKind == CognitiveMemoryKnowledgeRegionKind.Domain &&
                        item.RegionKey == regionKey,
                cancellationToken);
        if (region is null)
        {
            region = new CognitiveMemoryKnowledgeRegionRecord
            {
                ProjectId = projectId,
                RegionKind = CognitiveMemoryKnowledgeRegionKind.Domain,
                RegionKey = regionKey,
                DisplayName = regionKey,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            dbContext.Add(region);
        }

        var coverage = await dbContext.Set<CognitiveMemoryCoverageMapRecord>()
            .SingleOrDefaultAsync(item => item.ProjectId == projectId && item.KnowledgeRegionId == region.Id, cancellationToken);
        if (coverage is null)
        {
            dbContext.Add(new CognitiveMemoryCoverageMapRecord
            {
                ProjectId = projectId,
                KnowledgeRegionId = region.Id,
                CoverageState = coverageStateOverride ?? (sourceWeakness >= 0.7 ? CognitiveMemoryCoverageState.Thin : CognitiveMemoryCoverageState.Unknown),
                SourceEvidenceCount = sourceEvidenceCount,
                RefreshedAtUtc = now
            });
        }
        else
        {
            coverage.CoverageState = coverageStateOverride ?? coverage.CoverageState;
            coverage.SourceEvidenceCount = Math.Max(coverage.SourceEvidenceCount, sourceEvidenceCount);
            coverage.RefreshedAtUtc = now;
        }

        var gap = new CognitiveMemoryKnowledgeGapRecord
        {
            ProjectId = projectId,
            KnowledgeRegionId = region.Id,
            GapKind = gapKind,
            Summary = explanation,
            EvidenceRefsJson = evidenceRefsJson,
            CreatedAtUtc = now
        };
        dbContext.Add(gap);
        var trace = await CognitiveMemoryAdvancedScoring.EvaluateAndPersistAsync(
            dbContext,
            scoreGeometryDriver,
            projectId,
            CognitiveMemoryScoreOwnerKind.LearningProposal,
            gap.Id,
            CognitiveMemoryScoreSpaceKind.EpistemicNeed,
            [
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.MissingKnowledgePressure, missingKnowledgePressure),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.SourceWeakness, sourceWeakness),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.ExpectedLearningValue, 0.75),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.ExpectedEffort, 0.45)
            ],
            CognitiveMemoryScoreProjectionBucket.NeedsReview,
            now,
            cancellationToken);
        var proposal = new CognitiveMemoryLearningProposalRecord
        {
            ProjectId = projectId,
            KnowledgeGapId = gap.Id,
            Status = CognitiveMemoryLearningProposalStatus.PendingApproval,
            Title = title,
            Explanation = explanation,
            EvidenceRefsJson = evidenceRefsJson,
            Risks = new CognitiveMemoryRiskNotes("Learning proposals do not create canonical truth until source-backed review accepts outputs."),
            AcceptanceCriteria = "Approved learning must cite source refs and route durable changes through mutation authority or review.",
            NeedScoreEvaluationTraceId = trace.Id.Value,
            NeedBucket = trace.ScalarProjection?.Bucket ?? CognitiveMemoryScoreProjectionBucket.Unknown,
            DisplayPriorityProjection = trace.ScalarProjection?.DisplayScore,
            CreatedAtUtc = now
        };
        dbContext.Add(proposal);
        return proposal;
    }

    private async Task<IReadOnlyList<CognitiveMemoryLearningProposalRecord>> CreateSourceCoverageProposalsAsync(
        AppDbContext dbContext,
        Guid projectId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var sourceItems = await dbContext.Set<CognitiveMemorySourceItemRecord>()
            .AsNoTracking()
            .Where(item => item.ProjectId == projectId)
            .OrderBy(item => item.Id)
            .Take(200)
            .Select(item => new EpistemicSourceCoverageSnapshot(
                item.Id,
                item.SourceSystem,
                item.SourceItemType,
                item.Title,
                item.ContentText,
                item.ContentHash))
            .ToListAsync(cancellationToken);
        var proposals = new List<CognitiveMemoryLearningProposalRecord>();
        foreach (var group in sourceItems
            .SelectMany(source => CognitiveMemoryConsolidationFactExtractor
                .ResolvePlanningDimensions(source.ContentText)
                .Select(dimension => new { Source = source, Dimension = dimension }))
            .GroupBy(item => item.Dimension, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var regionKey = $"planning:{group.Key}";
            if (await HasCanonicalCoverageAsync(dbContext, projectId, regionKey, cancellationToken) ||
                await HasExistingLearningProposalAsync(dbContext, projectId, regionKey, cancellationToken))
            {
                continue;
            }

            var evidenceSources = group
                .Select(item => item.Source)
                .DistinctBy(source => source.Id)
                .Take(5)
                .ToArray();
            var evidenceRefsJson = JsonSerializer.Serialize(
                evidenceSources.Select(source => $"source-item:{source.Id:D}").ToArray(),
                CognitiveMemoryAdvancedJson.Options);
            var explanation =
                $"Source-backed coverage gap: {evidenceSources.Length} source item(s) discuss planning dimension '{group.Key}', but no canonical reusable memory covers it yet.";
            proposals.Add(await CreateProposalAsync(
                dbContext,
                projectId,
                regionKey,
                CognitiveMemoryKnowledgeGapKind.ProfessorSuggestedExpansion,
                $"Study reusable planning knowledge for {group.Key}",
                explanation,
                missingKnowledgePressure: Math.Clamp(0.45 + evidenceSources.Length * 0.1, 0, 1),
                sourceWeakness: 0.25,
                now,
                cancellationToken,
                evidenceRefsJson,
                CognitiveMemoryCoverageState.Thin,
                evidenceSources.Length));
        }

        return proposals;
    }

    private static async Task<bool> HasCanonicalCoverageAsync(
        AppDbContext dbContext,
        Guid projectId,
        string regionKey,
        CancellationToken cancellationToken)
        => await dbContext.Set<CognitiveMemoryRecord>()
            .AsNoTracking()
            .AnyAsync(record =>
                record.ProjectId == projectId &&
                record.TopicKey == regionKey,
                cancellationToken);

    private static async Task<bool> HasExistingLearningProposalAsync(
        AppDbContext dbContext,
        Guid projectId,
        string regionKey,
        CancellationToken cancellationToken)
        => await (
            from proposal in dbContext.Set<CognitiveMemoryLearningProposalRecord>().AsNoTracking()
            join gap in dbContext.Set<CognitiveMemoryKnowledgeGapRecord>().AsNoTracking()
                on proposal.KnowledgeGapId equals gap.Id
            join region in dbContext.Set<CognitiveMemoryKnowledgeRegionRecord>().AsNoTracking()
                on gap.KnowledgeRegionId equals region.Id
            where proposal.ProjectId == projectId &&
                  region.RegionKey == regionKey &&
                  proposal.Status != CognitiveMemoryLearningProposalStatus.Draft
            select proposal.Id)
            .AnyAsync(cancellationToken);

    private sealed record EpistemicSourceCoverageSnapshot(
        Guid Id,
        string SourceSystem,
        string SourceItemType,
        string Title,
        string ContentText,
        string ContentHash);
}

public sealed class CognitiveMemoryCrossProjectMemoryService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ICognitiveMemoryScoreGeometryDriver scoreGeometryDriver,
    IClock clock) : ICognitiveMemoryCrossProjectMemoryService
{
    public async ValueTask<CognitiveMemoryCrossProjectPromotionCandidateRecord> CreateCandidateAsync(
        CognitiveMemoryCrossProjectPromotionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var sourceRecord = await dbContext.Set<CognitiveMemoryRecord>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.SourceMemoryRecordId, cancellationToken)
            ?? throw new InvalidOperationException($"Source memory record '{request.SourceMemoryRecordId:D}' was not found.");
        if (sourceRecord.AccessLevel == CognitiveMemoryAccessLevel.Restricted && !request.PolicyContext.AllowRestrictedContent)
        {
            throw new InvalidOperationException("Restricted project memory cannot be proposed for cross-project promotion without restricted-content policy.");
        }

        var now = clock.GetUtcNow();
        var trace = await CognitiveMemoryAdvancedScoring.EvaluateAndPersistAsync(
            dbContext,
            scoreGeometryDriver,
            request.SourceProjectId,
            CognitiveMemoryScoreOwnerKind.CrossProjectCandidate,
            request.SourceMemoryRecordId,
            CognitiveMemoryScoreSpaceKind.CrossProjectPromotion,
            [
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.SemanticSimilarity, request.SemanticSimilarity),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.EntityEquivalence, request.EntityEquivalence),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.ContextSeparation, request.ContextSeparation),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.SourceReusePermission, request.SourceReusePermission),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.PolicyCompatibility, request.PolicyCompatibility),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.PrivacyRisk, sourceRecord.AccessLevel == CognitiveMemoryAccessLevel.Public ? 0.1 : 0.45),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.GlobalReuseValue, 0.75)
            ],
            request.ContextSeparation >= 0.6 || request.PolicyCompatibility < 0.5
                ? CognitiveMemoryScoreProjectionBucket.Inhibit
                : CognitiveMemoryScoreProjectionBucket.NeedsReview,
            now,
            cancellationToken);
        var reviewItem = new CognitiveMemoryReviewItemRecord
        {
            ProjectId = request.SourceProjectId,
            ReviewKind = CognitiveMemoryReviewKind.GeneratedMemory,
            SubjectKind = CognitiveMemoryReviewSubjectKind.MemoryRecord,
            SubjectId = sourceRecord.Id,
            Status = CognitiveMemoryReviewStatus.Pending,
            RiskLevel = sourceRecord.RiskLevel,
            ReasonCode = "cross-project-promotion",
            ReasonText = request.Reason.Trim(),
            SourceEvidenceCount = sourceRecord.SourceEvidenceCount,
            CreatedAtUtc = now
        };
        dbContext.Add(reviewItem);
        var candidate = new CognitiveMemoryCrossProjectPromotionCandidateRecord
        {
            SourceProjectId = request.SourceProjectId,
            SourceMemoryRecordId = request.SourceMemoryRecordId,
            Status = CognitiveMemoryCrossProjectPromotionStatus.PendingReview,
            PromotionScoreEvaluationTraceId = trace.Id.Value,
            PromotionBucket = trace.ScalarProjection?.Bucket ?? CognitiveMemoryScoreProjectionBucket.Unknown,
            RequestedByActorId = request.RequestedByActorId.Trim(),
            Reason = request.Reason.Trim(),
            ReviewItemId = reviewItem.Id,
            CreatedAtUtc = now
        };
        dbContext.Add(candidate);
        await dbContext.SaveChangesAsync(cancellationToken);
        return candidate;
    }

    public async ValueTask<CognitiveMemoryCrossProjectPromotionCandidateRecord> DecideAsync(
        Guid candidateId,
        CognitiveMemoryCrossProjectPromotionStatus decision,
        string actorId,
        string notes,
        CancellationToken cancellationToken = default)
    {
        if (decision is CognitiveMemoryCrossProjectPromotionStatus.Candidate or CognitiveMemoryCrossProjectPromotionStatus.PendingReview)
        {
            throw new ArgumentException("Cross-project promotion decision must be a terminal promotion state.", nameof(decision));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var candidate = await dbContext.Set<CognitiveMemoryCrossProjectPromotionCandidateRecord>()
            .SingleOrDefaultAsync(item => item.Id == candidateId, cancellationToken)
            ?? throw new InvalidOperationException($"Cross-project promotion candidate '{candidateId:D}' was not found.");
        candidate.Status = decision;
        candidate.DecidedByActorId = CognitiveMemoryGuard.EnsureText(actorId, nameof(actorId));
        candidate.DecisionNotes = notes.Trim();
        candidate.DecidedAtUtc = clock.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        return candidate;
    }
}

public sealed class CognitiveMemoryDistributedComputeCoordinator(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock) : ICognitiveMemoryDistributedComputeCoordinator
{
    public async ValueTask<CognitiveMemoryDistributedWorkerRecord> RegisterWorkerAsync(
        string workerId,
        string machineName,
        IReadOnlyList<CognitiveMemoryDistributedJobKind> capabilities,
        CancellationToken cancellationToken = default)
    {
        if (capabilities.Count == 0)
        {
            throw new ArgumentException("Distributed workers must declare at least one capability.", nameof(capabilities));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var normalizedWorkerId = CognitiveMemoryGuard.EnsureText(workerId, nameof(workerId));
        var now = clock.GetUtcNow();
        var worker = await dbContext.Set<CognitiveMemoryDistributedWorkerRecord>()
            .SingleOrDefaultAsync(item => item.WorkerId == normalizedWorkerId, cancellationToken);
        if (worker is null)
        {
            worker = new CognitiveMemoryDistributedWorkerRecord
            {
                WorkerId = normalizedWorkerId
            };
            worker.CreatedOrUpdated(machineName, capabilities, now);
            dbContext.Add(worker);
        }
        else
        {
            worker.CreatedOrUpdated(machineName, capabilities, now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return worker;
    }

    public async ValueTask<CognitiveMemoryDistributedJobRecord> EnqueueAsync(
        CognitiveMemoryDistributedJobEnqueueRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var inputHash = CognitiveMemoryHash.FromUtf8(request.InputPayloadJson);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await dbContext.Set<CognitiveMemoryDistributedJobRecord>()
            .SingleOrDefaultAsync(
                item => item.ProjectId == request.ProjectId &&
                        item.JobKind == request.JobKind &&
                        item.InputHash == inputHash.Value,
                cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var now = clock.GetUtcNow();
        var job = new CognitiveMemoryDistributedJobRecord
        {
            ProjectId = request.ProjectId,
            JobKind = request.JobKind,
            State = CognitiveMemoryDistributedJobState.Queued,
            SourceScopeKey = CognitiveMemoryGuard.EnsureText(request.SourceScopeKey, nameof(request.SourceScopeKey)),
            InputPayloadJson = CognitiveMemoryGuard.EnsureText(request.InputPayloadJson, nameof(request.InputPayloadJson)),
            InputHashAlgorithm = inputHash.Algorithm,
            InputHash = inputHash.Value,
            ExpectedOutputSchema = CognitiveMemoryGuard.EnsureText(request.ExpectedOutputSchema, nameof(request.ExpectedOutputSchema)),
            AlgorithmVersion = CognitiveMemoryGuard.EnsureText(request.AlgorithmVersion, nameof(request.AlgorithmVersion)),
            PolicyProfileId = CognitiveMemoryGuard.EnsureText(request.PolicyProfileId, nameof(request.PolicyProfileId)),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        dbContext.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);
        return job;
    }

    public async ValueTask<CognitiveMemoryDistributedLeaseClaim?> ClaimAsync(
        string workerId,
        IReadOnlyList<CognitiveMemoryDistributedJobKind> capabilities,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration), "Lease duration must be positive.");
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var normalizedWorkerId = CognitiveMemoryGuard.EnsureText(workerId, nameof(workerId));
        var now = clock.GetUtcNow();
        var job = await dbContext.Set<CognitiveMemoryDistributedJobRecord>()
            .Where(item => capabilities.Contains(item.JobKind) &&
                           (item.State == CognitiveMemoryDistributedJobState.Queued ||
                            item.State == CognitiveMemoryDistributedJobState.Leased && item.LeaseExpiresAtUtc < now))
            .OrderBy(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (job is null)
        {
            return null;
        }

        job.State = CognitiveMemoryDistributedJobState.Leased;
        job.LeasedWorkerId = normalizedWorkerId;
        job.LeaseToken = Guid.NewGuid().ToString("N");
        job.LeaseExpiresAtUtc = now.Add(leaseDuration);
        job.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new CognitiveMemoryDistributedLeaseClaim(
            job.Id,
            job.LeaseToken,
            job.LeaseExpiresAtUtc.Value,
            job.InputPayloadJson,
            job.InputHash);
    }

    public async ValueTask<CognitiveMemoryDistributedWorkerResultRecord> SubmitResultAsync(
        Guid jobId,
        string workerId,
        string leaseToken,
        string inputHash,
        string outputPayloadJson,
        string algorithmVersion,
        string outputSchema,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var job = await dbContext.Set<CognitiveMemoryDistributedJobRecord>()
            .SingleOrDefaultAsync(item => item.Id == jobId, cancellationToken)
            ?? throw new InvalidOperationException($"Distributed job '{jobId:D}' was not found.");
        var now = clock.GetUtcNow();
        var outputHash = CognitiveMemoryHash.FromUtf8(outputPayloadJson);
        var result = new CognitiveMemoryDistributedWorkerResultRecord
        {
            DistributedJobId = job.Id,
            ProjectId = job.ProjectId,
            WorkerId = CognitiveMemoryGuard.EnsureText(workerId, nameof(workerId)),
            InputHash = inputHash.Trim().ToLowerInvariant(),
            OutputHash = outputHash.Value,
            AlgorithmVersion = algorithmVersion.Trim(),
            OutputSchema = outputSchema.Trim(),
            OutputPayloadJson = outputPayloadJson.Trim(),
            SubmittedAtUtc = now
        };
        var rejection = ValidateDistributedResult(job, result, leaseToken, now);
        if (rejection is null)
        {
            result.Status = CognitiveMemoryDistributedResultStatus.Accepted;
            result.AcceptedAtUtc = now;
            job.State = CognitiveMemoryDistributedJobState.Completed;
            job.UpdatedAtUtc = now;
        }
        else
        {
            result.Status = CognitiveMemoryDistributedResultStatus.Rejected;
            result.RejectionReason = rejection;
            job.State = CognitiveMemoryDistributedJobState.Rejected;
            job.UpdatedAtUtc = now;
        }

        dbContext.Add(result);
        await dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }

    private static string? ValidateDistributedResult(
        CognitiveMemoryDistributedJobRecord job,
        CognitiveMemoryDistributedWorkerResultRecord result,
        string leaseToken,
        DateTimeOffset now)
    {
        if (job.State != CognitiveMemoryDistributedJobState.Leased)
        {
            return "Job is not leased.";
        }

        if (job.LeaseExpiresAtUtc is null || job.LeaseExpiresAtUtc <= now)
        {
            return "Lease expired.";
        }

        if (!string.Equals(job.LeaseToken, leaseToken, StringComparison.Ordinal))
        {
            return "Lease token mismatch.";
        }

        if (!string.Equals(job.LeasedWorkerId, result.WorkerId, StringComparison.Ordinal))
        {
            return "Worker id mismatch.";
        }

        if (!string.Equals(job.InputHash, result.InputHash, StringComparison.OrdinalIgnoreCase))
        {
            return "Input hash mismatch.";
        }

        if (!string.Equals(job.AlgorithmVersion, result.AlgorithmVersion, StringComparison.Ordinal))
        {
            return "Algorithm version mismatch.";
        }

        return string.Equals(job.ExpectedOutputSchema, result.OutputSchema, StringComparison.Ordinal)
            ? null
            : "Output schema mismatch.";
    }
}

internal static class CognitiveMemoryDistributedWorkerRecordExtensions
{
    public static void CreatedOrUpdated(
        this CognitiveMemoryDistributedWorkerRecord worker,
        string machineName,
        IReadOnlyList<CognitiveMemoryDistributedJobKind> capabilities,
        DateTimeOffset now)
    {
        worker.MachineName = CognitiveMemoryGuard.EnsureText(machineName, nameof(machineName));
        worker.Status = CognitiveMemoryDistributedWorkerStatus.Active;
        worker.CapabilitiesJson = JsonSerializer.Serialize(capabilities, CognitiveMemoryAdvancedJson.Options);
        worker.LastSeenAtUtc = now;
    }
}

internal static class CognitiveMemoryAdvancedScoring
{
    public static CognitiveMemoryScoreComponent Component(CognitiveMemoryScoreDimensionKind kind, double value, double confidence = 1)
        => new(kind, Math.Clamp(value, 0, 1), Math.Clamp(confidence, 0, 1));

    public static async Task<CognitiveMemoryScoreEvaluationTrace> EvaluateAndPersistAsync(
        AppDbContext dbContext,
        ICognitiveMemoryScoreGeometryDriver scoreGeometryDriver,
        Guid? projectId,
        CognitiveMemoryScoreOwnerKind ownerKind,
        Guid? ownerId,
        CognitiveMemoryScoreSpaceKind spaceKind,
        IReadOnlyList<CognitiveMemoryScoreComponent> components,
        CognitiveMemoryScoreProjectionBucket bucket,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var vector = new CognitiveMemoryScoreVectorSnapshot(
            spaceKind,
            CognitiveMemoryScoreSpaceRegistry.CurrentSchemaVersion,
            CognitiveMemoryScoreSpaceRegistry.CurrentNormalizationProfile,
            components,
            CognitiveMemoryScoreSpaceRegistry.CurrentAlgorithmVersion,
            now,
            CognitiveMemoryHash.FromUtf8($"{projectId:D}|{ownerKind}|{ownerId:D}|{spaceKind}|{string.Join('|', components.Select(item => $"{item.DimensionKind}:{item.NormalizedValue:0.000}"))}"));
        var shape = new CognitiveMemoryScoreShapeSnapshot(
            CognitiveMemoryScoreShapeKind.ThresholdEnvelope,
            spaceKind,
            CognitiveMemoryScoreSpaceRegistry.CurrentSchemaVersion,
            components.Select(component => new CognitiveMemoryScoreShapeComponent(
                    component.DimensionKind,
                    component.NormalizedValue,
                    0,
                    1,
                    1))
                .ToArray(),
            radius: null,
            bucket,
            $"{spaceKind} evaluated by cognitive-memory advanced service.",
            [],
            CognitiveMemoryScoreSpaceRegistry.CurrentAlgorithmVersion);
        var trace = await scoreGeometryDriver.EvaluateAsync(
            new CognitiveMemoryScoreEvaluationRequest(
                projectId,
                ownerKind,
                ownerId,
                spaceKind,
                CognitiveMemoryScoreSpaceRegistry.CurrentSchemaVersion,
                [vector],
                [shape]),
            cancellationToken);
        await CognitiveMemoryScoreTracePersistence.AddIfMissingAsync(dbContext, trace, now, cancellationToken);
        return trace;
    }
}

internal static class CognitiveMemoryAdvancedJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };
}

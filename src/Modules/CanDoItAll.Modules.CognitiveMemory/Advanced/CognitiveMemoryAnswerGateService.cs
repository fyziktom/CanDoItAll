using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CognitiveMemory;

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


using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CognitiveMemory;

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


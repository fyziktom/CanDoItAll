using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Voice;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Common;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;
using System.Text;

namespace CanDoItAll.Modules.CognitiveMemory.Pages;

public partial class CognitiveMemoryPage
{
    internal static Guid? ResolveSelectedMemoryRecordId(
        CognitiveMemoryReviewUiSnapshot snapshot,
        Guid? preferredId)
    {
        if (preferredId.HasValue &&
            snapshot.MemoryRecords.Any(record => record.Id.Value == preferredId.Value))
        {
            return preferredId.Value;
        }

        return snapshot.MemoryRecords.FirstOrDefault()?.Id.Value;
    }

    internal static Guid? ResolveSelectedReviewItemId(
        CognitiveMemoryReviewUiSnapshot snapshot,
        Guid? preferredId)
    {
        if (preferredId.HasValue &&
            snapshot.ReviewItems.Any(item => item.Id.Value == preferredId.Value))
        {
            return preferredId.Value;
        }

        return snapshot.ReviewItems.FirstOrDefault()?.Id.Value;
    }

    internal static Guid? ResolveSelectedRecallTraceId(
        CognitiveMemoryReviewUiSnapshot snapshot,
        Guid? preferredId)
    {
        if (preferredId.HasValue &&
            snapshot.RecallTraces.Any(trace => trace.Id == preferredId.Value))
        {
            return preferredId.Value;
        }

        return snapshot.RecallTraces.FirstOrDefault()?.Id;
    }

    internal static string SummaryValue(int? value)
        => value?.ToString() ?? "-";

    internal static string FormatDate(DateTimeOffset value)
        => value.ToLocalTime().ToString("MMM d, HH:mm");

    internal static string FormatShortId(Guid value)
        => value.ToString("N")[..8];

    internal static string ScoreText(double? value)
        => value.HasValue ? value.Value.ToString("0.00") : "n/a";

    internal static string FormatLabel<TValue>(TValue value)
        where TValue : struct, Enum
    {
        var text = value.ToString();
        var builder = new StringBuilder(text.Length + 8);

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (index > 0 &&
                char.IsUpper(character) &&
                (char.IsLower(text[index - 1]) ||
                 char.IsDigit(text[index - 1]) ||
                 index + 1 < text.Length && char.IsLower(text[index + 1])))
            {
                builder.Append(' ');
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    internal static int VisibleSourceEvidenceCount(CognitiveMemoryExplorerItem record)
        => Math.Max(record.SourceEvidenceCount, record.SourceLinks.Count);

    internal static string ReviewTone(
        CognitiveMemoryReviewStatus status,
        CognitiveMemoryRiskLevel riskLevel)
        => status switch
        {
            CognitiveMemoryReviewStatus.Approved => "success",
            CognitiveMemoryReviewStatus.Rejected => "danger",
            CognitiveMemoryReviewStatus.NeedsChanges => "warning",
            CognitiveMemoryReviewStatus.Deferred => "secondary",
            _ when riskLevel == CognitiveMemoryRiskLevel.High => "danger",
            _ => "warning"
        };

    internal static string RecordTone(
        CognitiveMemoryValidationState validationState,
        CognitiveMemoryRiskLevel riskLevel)
        => validationState switch
        {
            CognitiveMemoryValidationState.Approved or CognitiveMemoryValidationState.HumanReviewed => "success",
            CognitiveMemoryValidationState.Rejected => "danger",
            CognitiveMemoryValidationState.NeedsHumanReview => "warning",
            _ when riskLevel == CognitiveMemoryRiskLevel.High => "danger",
            _ => "secondary"
        };

    internal static string RiskTone(CognitiveMemoryRiskLevel riskLevel)
        => riskLevel switch
        {
            CognitiveMemoryRiskLevel.High => "danger",
            CognitiveMemoryRiskLevel.Medium => "warning",
            _ => "success"
        };

    internal static string RunTone(CognitiveMemoryRunStatus status)
        => status switch
        {
            CognitiveMemoryRunStatus.Succeeded => "success",
            CognitiveMemoryRunStatus.Failed => "danger",
            CognitiveMemoryRunStatus.Blocked => "warning",
            CognitiveMemoryRunStatus.Running => "info",
            CognitiveMemoryRunStatus.Cancelled => "neutral",
            _ => "secondary"
        };

    internal static string ProjectionTone(CognitiveMemoryProjectionStatus status, bool rebuildRequired)
        => status == CognitiveMemoryProjectionStatus.Failed
            ? "danger"
            : rebuildRequired || status == CognitiveMemoryProjectionStatus.RebuildRequired
                ? "warning"
                : status == CognitiveMemoryProjectionStatus.Projected
                    ? "success"
                    : "secondary";

    internal static string ReplayTone(CognitiveMemoryReplayJobState state)
        => state switch
        {
            CognitiveMemoryReplayJobState.Completed => "success",
            CognitiveMemoryReplayJobState.Failed => "danger",
            CognitiveMemoryReplayJobState.NeedsReview => "warning",
            CognitiveMemoryReplayJobState.Running => "info",
            _ => "secondary"
        };

    internal static string ProcedureTone(CognitiveMemoryProcedureSkillMaturity maturity, CognitiveMemoryRiskLevel riskLevel)
        => riskLevel == CognitiveMemoryRiskLevel.High
            ? "danger"
            : maturity switch
            {
                CognitiveMemoryProcedureSkillMaturity.Automatable or CognitiveMemoryProcedureSkillMaturity.Validated => "success",
                CognitiveMemoryProcedureSkillMaturity.Reviewed => "info",
                CognitiveMemoryProcedureSkillMaturity.Draft or CognitiveMemoryProcedureSkillMaturity.Observed => "warning",
                _ => "secondary"
            };

    internal static string ProbeTone(CognitiveMemoryProbeSessionStatus status)
        => status switch
        {
            CognitiveMemoryProbeSessionStatus.Active => "info",
            CognitiveMemoryProbeSessionStatus.Closed => "success",
            CognitiveMemoryProbeSessionStatus.Abandoned => "warning",
            _ => "secondary"
        };

    internal static string SelfRegulationTone(CognitiveMemorySelfRegulationStateKind state)
        => state switch
        {
            CognitiveMemorySelfRegulationStateKind.Calibrated => "success",
            CognitiveMemorySelfRegulationStateKind.ProfessorReviewNeeded or
            CognitiveMemorySelfRegulationStateKind.HighRiskUnverified or
            CognitiveMemorySelfRegulationStateKind.AccessLimited => "danger",
            CognitiveMemorySelfRegulationStateKind.Overconfident or
            CognitiveMemorySelfRegulationStateKind.SourcePoor or
            CognitiveMemorySelfRegulationStateKind.Fragmented => "warning",
            CognitiveMemorySelfRegulationStateKind.Underconfident or
            CognitiveMemorySelfRegulationStateKind.Exploratory => "info",
            _ => "secondary"
        };

    internal static string AnswerGateTone(CognitiveMemoryAnswerGateDecisionKind decisionKind)
        => decisionKind switch
        {
            CognitiveMemoryAnswerGateDecisionKind.Answer => "success",
            CognitiveMemoryAnswerGateDecisionKind.Warn => "warning",
            CognitiveMemoryAnswerGateDecisionKind.Abstain or
            CognitiveMemoryAnswerGateDecisionKind.Review or
            CognitiveMemoryAnswerGateDecisionKind.ProfessorReview => "danger",
            CognitiveMemoryAnswerGateDecisionKind.Clarify or
            CognitiveMemoryAnswerGateDecisionKind.SourceAudit or
            CognitiveMemoryAnswerGateDecisionKind.Probe or
            CognitiveMemoryAnswerGateDecisionKind.LearningRequest => "info",
            _ => "secondary"
        };

    internal static string ProfessorReviewTone(CognitiveMemoryProfessorReviewStatus status)
        => status switch
        {
            CognitiveMemoryProfessorReviewStatus.Completed or CognitiveMemoryProfessorReviewStatus.Routed => "success",
            CognitiveMemoryProfessorReviewStatus.RejectedByPolicy => "danger",
            CognitiveMemoryProfessorReviewStatus.Requested => "warning",
            _ => "secondary"
        };

    internal static string LearningProposalTone(CognitiveMemoryLearningProposalStatus status)
        => status switch
        {
            CognitiveMemoryLearningProposalStatus.Approved or CognitiveMemoryLearningProposalStatus.Completed => "success",
            CognitiveMemoryLearningProposalStatus.Rejected or CognitiveMemoryLearningProposalStatus.Snoozed => "secondary",
            CognitiveMemoryLearningProposalStatus.PendingApproval => "warning",
            _ => "info"
        };

    internal static string CrossProjectTone(CognitiveMemoryCrossProjectPromotionStatus status)
        => status switch
        {
            CognitiveMemoryCrossProjectPromotionStatus.Approved => "success",
            CognitiveMemoryCrossProjectPromotionStatus.Rejected or CognitiveMemoryCrossProjectPromotionStatus.Demoted => "danger",
            CognitiveMemoryCrossProjectPromotionStatus.PendingReview => "warning",
            _ => "secondary"
        };

    internal static string DistributedJobTone(CognitiveMemoryDistributedJobState state)
        => state switch
        {
            CognitiveMemoryDistributedJobState.Completed => "success",
            CognitiveMemoryDistributedJobState.Rejected or CognitiveMemoryDistributedJobState.Expired => "danger",
            CognitiveMemoryDistributedJobState.Leased => "info",
            CognitiveMemoryDistributedJobState.Queued => "warning",
            _ => "secondary"
        };

    internal static string OperatorAuditTone(
        CognitiveMemoryOperatorAuditKind auditKind,
        CognitiveMemoryOperatorAuditStatus status)
        => status switch
        {
            CognitiveMemoryOperatorAuditStatus.Failed or CognitiveMemoryOperatorAuditStatus.Rejected => "danger",
            CognitiveMemoryOperatorAuditStatus.ReviewRequired or CognitiveMemoryOperatorAuditStatus.NeedsReview or CognitiveMemoryOperatorAuditStatus.RebuildRequired or CognitiveMemoryOperatorAuditStatus.Blocked => "warning",
            CognitiveMemoryOperatorAuditStatus.Accepted or CognitiveMemoryOperatorAuditStatus.Supported or CognitiveMemoryOperatorAuditStatus.Safe or CognitiveMemoryOperatorAuditStatus.Succeeded => "success",
            CognitiveMemoryOperatorAuditStatus.Running => "info",
            CognitiveMemoryOperatorAuditStatus.Restricted => "danger",
            _ => auditKind switch
            {
                CognitiveMemoryOperatorAuditKind.ProjectionFailure => "warning",
                CognitiveMemoryOperatorAuditKind.MutationCommand => "info",
                CognitiveMemoryOperatorAuditKind.MutationAuditEvent => "secondary",
                CognitiveMemoryOperatorAuditKind.ClaimState => "info",
                CognitiveMemoryOperatorAuditKind.EvidenceAnchor => "secondary",
                CognitiveMemoryOperatorAuditKind.RetentionCleanup => "info",
                _ => "secondary"
            }
        };

    internal static string QualityClusterTone(CognitiveMemoryQualityClusterReadiness readiness)
        => readiness switch
        {
            CognitiveMemoryQualityClusterReadiness.AggregateReady => "success",
            CognitiveMemoryQualityClusterReadiness.NeedsHumanReview or CognitiveMemoryQualityClusterReadiness.Contradictory => "warning",
            CognitiveMemoryQualityClusterReadiness.Restricted => "danger",
            CognitiveMemoryQualityClusterReadiness.NeedsMoreEvidence => "info",
            _ => "secondary"
        };

    internal static string AggregateCandidateTone(CognitiveMemoryDreamAggregateCandidateStatus status)
        => status switch
        {
            CognitiveMemoryDreamAggregateCandidateStatus.Approved => "success",
            CognitiveMemoryDreamAggregateCandidateStatus.NeedsHumanReview => "warning",
            CognitiveMemoryDreamAggregateCandidateStatus.Rejected => "danger",
            CognitiveMemoryDreamAggregateCandidateStatus.Applied => "info",
            CognitiveMemoryDreamAggregateCandidateStatus.Proposed => "secondary",
            _ => "secondary"
        };
}

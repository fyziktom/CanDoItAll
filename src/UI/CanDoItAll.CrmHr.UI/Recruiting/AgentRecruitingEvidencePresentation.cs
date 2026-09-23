using System.Text;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AppComponents;
using CanDoItAll.CrmHr.UI.Activity;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.CrmHr.UI.Pickers;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;

namespace CanDoItAll.CrmHr.UI.Recruiting;

// Presentation helpers of the Recruiting workspace: labels, tones, option lists and projections its surface renders.
public static class AgentRecruitingEvidencePresentation
{
    public static AgentRecruitingHumanReview? ResolveLatestReview(
        AgentRecruitingInterview interview,
        Guid attemptId)
    {
        return interview.Reviews
            .Where(item => item.AttemptId == attemptId)
            .OrderByDescending(item => item.ReviewedAtUtc)
            .FirstOrDefault();
    }

    public static string FormatValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Not set" : value;
    }

    public static string FormatReadiness(AgentRecruitingReadinessStatus status)
    {
        return status switch
        {
            AgentRecruitingReadinessStatus.NoInterviews => "No assessments",
            AgentRecruitingReadinessStatus.IncompleteEvidence => "Incomplete evidence",
            AgentRecruitingReadinessStatus.AwaitingHumanApproval => "Needs human review",
            _ => SplitPascalCase(status.ToString())
        };
    }

    public static string FormatTargetKind(AgentRecruitingTargetKind targetKind)
    {
        return targetKind switch
        {
            AgentRecruitingTargetKind.AgentExecutionRun => "Agent work",
            AgentRecruitingTargetKind.WorkflowRun => "Workflow",
            AgentRecruitingTargetKind.ProcessRun => "Process",
            _ => targetKind.ToString()
        };
    }

    public static string FormatClassification(AgentRecruitingAssessmentClassification classification)
    {
        return SplitPascalCase(classification.ToString());
    }

    public static string FormatNextStep(AgentRecruitingProposedNextStep nextStep)
    {
        return nextStep switch
        {
            AgentRecruitingProposedNextStep.RequestHumanReview => "Human review",
            AgentRecruitingProposedNextStep.AssignTraining => "Training",
            AgentRecruitingProposedNextStep.Reassess => "Recheck",
            _ => SplitPascalCase(nextStep.ToString())
        };
    }

    public static string FormatCompleteness(AgentRecruitingEvidenceCompleteness completeness)
    {
        return completeness == AgentRecruitingEvidenceCompleteness.Complete
            ? "Evidence complete"
            : "Evidence incomplete";
    }

    public static string SplitPascalCase(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var builder = new StringBuilder(value.Length + 8);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (index > 0 && char.IsUpper(character) && !char.IsUpper(value[index - 1]))
            {
                builder.Append(' ');
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    public static string ResolveReadinessTone(AgentRecruitingReadinessStatus status)
    {
        return status switch
        {
            AgentRecruitingReadinessStatus.Ready => "success",
            AgentRecruitingReadinessStatus.Rejected => "danger",
            AgentRecruitingReadinessStatus.AwaitingHumanApproval => "warning",
            AgentRecruitingReadinessStatus.IncompleteEvidence => "warning",
            _ => "neutral"
        };
    }

    public static string ResolveNextStepTone(AgentRecruitingProposedNextStep? nextStep)
    {
        return nextStep switch
        {
            AgentRecruitingProposedNextStep.Advance => "success",
            AgentRecruitingProposedNextStep.Reject => "danger",
            AgentRecruitingProposedNextStep.AssignTraining => "warning",
            AgentRecruitingProposedNextStep.Reassess => "info",
            AgentRecruitingProposedNextStep.Hold => "warning",
            _ => "neutral"
        };
    }

    public static string ResolveCompletenessTone(AgentRecruitingEvidenceCompleteness completeness)
    {
        return completeness == AgentRecruitingEvidenceCompleteness.Complete ? "success" : "warning";
    }

    public static string ResolveClassificationTone(AgentRecruitingAssessmentClassification classification)
    {
        return classification switch
        {
            AgentRecruitingAssessmentClassification.StrongFit => "success",
            AgentRecruitingAssessmentClassification.Suitable => "info",
            AgentRecruitingAssessmentClassification.NeedsTraining => "warning",
            AgentRecruitingAssessmentClassification.NotSuitable => "danger",
            _ => "neutral"
        };
    }
}

public sealed class AttemptDraft
{
    public Guid? InterviewId { get; set; }
    public AgentRecruitingTargetKind TargetKind { get; set; } = AgentRecruitingTargetKind.AgentExecutionRun;
    public string TargetId { get; set; } = string.Empty;
    public string ChallengeKey { get; set; } = "crm-hr-assessment";
    public string ChallengeVersion { get; set; } = "v1";
    public string RubricVersion { get; set; } = "v1";
    public AgentRecruitingAssessmentClassification Classification { get; set; } = AgentRecruitingAssessmentClassification.Inconclusive;
    public decimal Confidence { get; set; } = 0.5m;
    public AgentRecruitingProposedNextStep ProposedNextStep { get; set; } = AgentRecruitingProposedNextStep.RequestHumanReview;
    public string AnalysisSummary { get; set; } = string.Empty;
    public string Strengths { get; set; } = string.Empty;
    public string Gaps { get; set; } = string.Empty;
    public AgentRecruitingAutomatedDecision AutomatedDecision { get; set; } = AgentRecruitingAutomatedDecision.NeedsHumanReview;
    public decimal? Score { get; set; }
    public Guid? EvaluatorAgentId { get; set; }
    public Guid? ProviderProfileId { get; set; }
    public string Model { get; set; } = string.Empty;
    public string InputHash { get; set; } = string.Empty;
    public string OutputHash { get; set; } = string.Empty;
    public string StructuredOutputContractKey { get; set; } = string.Empty;
    public string StructuredOutputSchemaHash { get; set; } = string.Empty;
}

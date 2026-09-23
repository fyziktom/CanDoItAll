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
public static class CrmHrRecruitingWorkspacePresentation
{
    public static readonly IReadOnlyList<PagedRecordFilterOption<RecruitmentApplicationScope>> ApplicationScopeOptions =
    [
        new(RecruitmentApplicationScope.All, "All", "crmhr-recruiting-stage-all"),
        new(RecruitmentApplicationScope.Applied, "Applied", "crmhr-recruiting-stage-applied"),
        new(RecruitmentApplicationScope.Screening, "Screening", "crmhr-recruiting-stage-screening"),
        new(RecruitmentApplicationScope.Interviewing, "Interviewing", "crmhr-recruiting-stage-interviewing"),
        new(RecruitmentApplicationScope.Offer, "Offer", "crmhr-recruiting-stage-offer"),
        new(RecruitmentApplicationScope.Hired, "Hired", "crmhr-recruiting-stage-hired"),
        new(RecruitmentApplicationScope.Rejected, "Rejected", "crmhr-recruiting-stage-rejected"),
        new(RecruitmentApplicationScope.Withdrawn, "Withdrawn", "crmhr-recruiting-stage-withdrawn")
    ];

    public static string ResolveStageTone(RecruitmentStage stage)
    {
        return stage switch
        {
            RecruitmentStage.Hired => "success",
            RecruitmentStage.Offer => "primary",
            RecruitmentStage.Interviewing => "info",
            RecruitmentStage.Rejected => "danger",
            RecruitmentStage.Withdrawn => "warning",
            _ => "base"
        };
    }

    public static string ResolveDecisionTone(RecruitmentDecision decision)
    {
        return decision switch
        {
            RecruitmentDecision.Approved => "success",
            RecruitmentDecision.Rejected => "danger",
            RecruitmentDecision.Withdrawn => "warning",
            _ => "base"
        };
    }

    public static string FormatValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Not set" : value;
    }

    public static string FormatCandidateType(PartyType partyType)
    {
        return partyType == PartyType.AiAgent ? "AI agent" : "Person";
    }

    public static string FormatBinding(AiResourceBindingStatus bindingStatus)
    {
        return bindingStatus switch
        {
            AiResourceBindingStatus.Bound => "Technical binding ready",
            AiResourceBindingStatus.PendingBackfill => "Binding pending",
            AiResourceBindingStatus.Error => "Binding error",
            _ => "Not technically bound"
        };
    }

    public static string ResolveBindingTone(AiResourceBindingStatus bindingStatus)
    {
        return bindingStatus switch
        {
            AiResourceBindingStatus.Bound => "success",
            AiResourceBindingStatus.Error => "danger",
            AiResourceBindingStatus.PendingBackfill => "warning",
            _ => "neutral"
        };
    }
}

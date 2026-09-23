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

namespace CanDoItAll.CrmHr.UI.Workforce;

// Presentation helpers of the Workforce workspace: labels, tones, option lists and projections its surface renders.
public static class CrmHrWorkforceWorkspacePresentation
{
    public const int ProfileTabIndex = 1;

    public static readonly IReadOnlyList<string> WorkforceStatuses =
    [
        "Planned",
        "Active",
        "Leave",
        "Inactive",
        "Alumni"
    ];

    public static readonly IReadOnlyList<string> SeniorityValues =
    [
        string.Empty,
        "Associate",
        "Mid",
        "Senior",
        "Lead",
        "Principal"
    ];

    public static string ResolveLifecycleTone(PartyLifecycleStatus lifecycleStatus)
    {
        return lifecycleStatus switch
        {
            PartyLifecycleStatus.Active => "success",
            PartyLifecycleStatus.Candidate or PartyLifecycleStatus.Prospect => "info",
            PartyLifecycleStatus.Former or PartyLifecycleStatus.Inactive => "warning",
            PartyLifecycleStatus.Archived => "neutral",
            _ => "neutral"
        };
    }

    public static string ResolveStatusTone(string status)
    {
        return status switch
        {
            "Active" => "success",
            "Leave" => "warning",
            "Inactive" => "danger",
            "Alumni" => "info",
            _ => "base"
        };
    }

    public static string ResolveAvailabilityTone(WorkforceAvailabilityState availabilityState)
    {
        return availabilityState switch
        {
            WorkforceAvailabilityState.Overallocated => "danger",
            WorkforceAvailabilityState.Bench => "success",
            WorkforceAvailabilityState.NearAvailable => "warning",
            _ => "info"
        };
    }

    public static string FormatValue(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Not set" : value;
    }

    public static string FormatDate(DateOnly? value)
    {
        return value.HasValue ? value.Value.ToString("yyyy-MM-dd") : "Not set";
    }

    public static string FormatOptionalPercent(decimal? value)
    {
        return value.HasValue ? $"{value.Value:0.##}%" : "Not loaded";
    }

    public static string BuildSkillCoverage(IReadOnlyList<PartySkillItemModel> skills)
    {
        return string.Join(", ", skills.Select(item => item.SkillName));
    }
}

public sealed class DeliveryUnitQuickCreateModel
{
    public string Name { get; set; } = string.Empty;
    public string ExternalCode { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}

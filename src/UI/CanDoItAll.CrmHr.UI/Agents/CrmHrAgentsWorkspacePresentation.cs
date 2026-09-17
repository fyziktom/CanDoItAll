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

namespace CanDoItAll.CrmHr.UI.Agents;

// Presentation helpers of the Agents workspace: labels, tones, option lists and projections its surface renders.
public static class CrmHrAgentsWorkspacePresentation
{
    public static readonly IReadOnlyList<PagedRecordFilterOption<AiAgentValidationFilter>> ValidationFilters =
    [
        new(AiAgentValidationFilter.All, "All", "crmhr-agent-validation-all"),
        new(AiAgentValidationFilter.Draft, "Draft", "crmhr-agent-validation-draft"),
        new(AiAgentValidationFilter.ReviewRequired, "Review required", "crmhr-agent-validation-review"),
        new(AiAgentValidationFilter.Approved, "Approved", "crmhr-agent-validation-approved"),
        new(AiAgentValidationFilter.Suspended, "Suspended", "crmhr-agent-validation-suspended")
    ];
}

public enum AiAgentValidationFilter
{
    All,
    Draft,
    ReviewRequired,
    Approved,
    Suspended
}

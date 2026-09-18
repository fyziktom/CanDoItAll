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

namespace CanDoItAll.CrmHr.UI.Crm;

// Presentation helpers of the Crm workspace: labels, tones, option lists and projections its surface renders.
public static class CrmHrCrmWorkspacePresentation
{
    public static string FormatConnectionRole(CrmAccountConnectionRole role)
    {
        return role switch
        {
            CrmAccountConnectionRole.PrimaryContact => "Primary contact",
            CrmAccountConnectionRole.Stakeholder => "Stakeholder",
            CrmAccountConnectionRole.BillingContact => "Billing contact",
            CrmAccountConnectionRole.ContractContact => "Contract contact",
            CrmAccountConnectionRole.AccountManager => "Account manager",
            CrmAccountConnectionRole.DeliveryLead => "Delivery lead",
            CrmAccountConnectionRole.Sponsor => "Sponsor",
            CrmAccountConnectionRole.TechnicalContact => "Technical contact",
            _ => throw new ArgumentOutOfRangeException(
                nameof(role),
                role,
                "Unknown CRM account connection role.")
        };
    }
}

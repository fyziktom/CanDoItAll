using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.CrmHr;

internal static class ProjectPartyAssignmentPresentation
{
    public static string ResolveRoleLabel(ProjectPartyAssignmentRole role)
    {
        return role switch
        {
            ProjectPartyAssignmentRole.Customer => "Customer",
            ProjectPartyAssignmentRole.CustomerContact => "Customer contact",
            ProjectPartyAssignmentRole.DeliveryUnit => "Delivery unit",
            ProjectPartyAssignmentRole.TeamMember => "Team member",
            ProjectPartyAssignmentRole.Manager => "Manager",
            ProjectPartyAssignmentRole.Partner => "Partner",
            ProjectPartyAssignmentRole.Vendor => "Vendor",
            ProjectPartyAssignmentRole.Stakeholder => "Stakeholder",
            ProjectPartyAssignmentRole.MeetingParticipant => "Meeting participant",
            ProjectPartyAssignmentRole.WorkItemAssignee => "Work-item assignee",
            ProjectPartyAssignmentRole.Reviewer => "Reviewer",
            ProjectPartyAssignmentRole.AiAgent => "AI agent",
            ProjectPartyAssignmentRole.BillingContact => "Billing contact",
            ProjectPartyAssignmentRole.TechnicalContact => "Technical contact",
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "The project assignment role is not supported.")
        };
    }
}

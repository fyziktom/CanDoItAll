using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.CrmHr;

internal static class ProjectAssignmentRoleMapping {
    internal static ProjectPartyAssignmentRole MapRole(ProjectPartyAssignmentKind role) {
        return role switch {
            ProjectPartyAssignmentKind.Customer => ProjectPartyAssignmentRole.Customer,
            ProjectPartyAssignmentKind.CustomerContact => ProjectPartyAssignmentRole.CustomerContact,
            ProjectPartyAssignmentKind.DeliveryUnit => ProjectPartyAssignmentRole.DeliveryUnit,
            ProjectPartyAssignmentKind.TeamMember => ProjectPartyAssignmentRole.TeamMember,
            ProjectPartyAssignmentKind.Manager => ProjectPartyAssignmentRole.Manager,
            ProjectPartyAssignmentKind.Partner => ProjectPartyAssignmentRole.Partner,
            ProjectPartyAssignmentKind.Vendor => ProjectPartyAssignmentRole.Vendor,
            ProjectPartyAssignmentKind.Stakeholder => ProjectPartyAssignmentRole.Stakeholder,
            ProjectPartyAssignmentKind.MeetingParticipant => ProjectPartyAssignmentRole.MeetingParticipant,
            ProjectPartyAssignmentKind.WorkItemAssignee => ProjectPartyAssignmentRole.WorkItemAssignee,
            ProjectPartyAssignmentKind.Reviewer => ProjectPartyAssignmentRole.Reviewer,
            ProjectPartyAssignmentKind.AiAgent => ProjectPartyAssignmentRole.AiAgent,
            ProjectPartyAssignmentKind.BillingContact => ProjectPartyAssignmentRole.BillingContact,
            ProjectPartyAssignmentKind.TechnicalContact => ProjectPartyAssignmentRole.TechnicalContact,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "The project assignment kind is not supported.")
        };
    }

    internal static ProjectPartyAssignmentKind MapRole(ProjectPartyAssignmentRole role) {
        return role switch {
            ProjectPartyAssignmentRole.Customer => ProjectPartyAssignmentKind.Customer,
            ProjectPartyAssignmentRole.CustomerContact => ProjectPartyAssignmentKind.CustomerContact,
            ProjectPartyAssignmentRole.DeliveryUnit => ProjectPartyAssignmentKind.DeliveryUnit,
            ProjectPartyAssignmentRole.TeamMember => ProjectPartyAssignmentKind.TeamMember,
            ProjectPartyAssignmentRole.Manager => ProjectPartyAssignmentKind.Manager,
            ProjectPartyAssignmentRole.Partner => ProjectPartyAssignmentKind.Partner,
            ProjectPartyAssignmentRole.Vendor => ProjectPartyAssignmentKind.Vendor,
            ProjectPartyAssignmentRole.Stakeholder => ProjectPartyAssignmentKind.Stakeholder,
            ProjectPartyAssignmentRole.MeetingParticipant => ProjectPartyAssignmentKind.MeetingParticipant,
            ProjectPartyAssignmentRole.WorkItemAssignee => ProjectPartyAssignmentKind.WorkItemAssignee,
            ProjectPartyAssignmentRole.Reviewer => ProjectPartyAssignmentKind.Reviewer,
            ProjectPartyAssignmentRole.AiAgent => ProjectPartyAssignmentKind.AiAgent,
            ProjectPartyAssignmentRole.BillingContact => ProjectPartyAssignmentKind.BillingContact,
            ProjectPartyAssignmentRole.TechnicalContact => ProjectPartyAssignmentKind.TechnicalContact,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "The project assignment role is not supported.")
        };
    }

}

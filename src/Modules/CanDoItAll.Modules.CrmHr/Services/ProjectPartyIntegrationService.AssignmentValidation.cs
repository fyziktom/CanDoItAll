using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.CrmHr;

public sealed partial class ProjectPartyIntegrationService
{
    private static Error? ValidateAssignmentPartyType(ProjectPartyAssignmentRole role, PartyType partyType)
    {
        if (role != ProjectPartyAssignmentRole.WorkItemAssignee ||
            partyType is PartyType.Person or PartyType.AiAgent)
        {
            return null;
        }

        return Error.Validation(
            "A work-item assignee must be a person or AI agent.",
            "crmhr.project-assignment.work-item-assignee-party-type-invalid");
    }
}

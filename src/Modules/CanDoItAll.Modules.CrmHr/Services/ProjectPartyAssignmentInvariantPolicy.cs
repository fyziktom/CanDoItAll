using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.CrmHr;

internal static class ProjectPartyAssignmentInvariantPolicy
{
    public static Error? ValidateValues(ProjectPartyAssignmentUpsertRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.AllocationPercent is <= 0m or > 100m)
        {
            return Error.Validation(
                "Allocation must be greater than 0 and no more than 100 percent.",
                "crmhr.project-assignment.allocation-range");
        }

        if (request.StartsOn.HasValue &&
            request.EndsOn.HasValue &&
            request.StartsOn.Value > request.EndsOn.Value)
        {
            return Error.Validation(
                "Assignment end date must be on or after the start date.",
                "crmhr.project-assignment.date-range-invalid");
        }

        return null;
    }

    public static Error? ValidatePartyType(ProjectPartyAssignmentRole role, PartyType partyType)
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

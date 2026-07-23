using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Workbench;

public static class ProjectStructureTaskPricingResourcePolicy
{
    public static ProjectStructureTaskResourceSelection? Resolve(
        bool directAssigneeChanged,
        ProjectStructureTaskResourceSelection? proposedAssignee,
        ProjectStructureTaskAssigneeSelectionResult assigneeResolution,
        ProjectTaskExpectedCostBasis? costBasis)
    {
        ArgumentNullException.ThrowIfNull(assigneeResolution);
        if (directAssigneeChanged)
        {
            if (proposedAssignee is not null)
            {
                return proposedAssignee;
            }

            return costBasis?.ResourceKind is ProjectStructureTaskResourceKind.Workflow or
                ProjectStructureTaskResourceKind.Process
                    ? ProjectTaskExpectedCostBasisPolicy.ToResource(costBasis)
                    : null;
        }

        if (costBasis is not null)
        {
            var basisResource = ProjectTaskExpectedCostBasisPolicy.ToResource(costBasis);
            if (basisResource.Kind is ProjectStructureTaskResourceKind.Workflow or
                ProjectStructureTaskResourceKind.Process)
            {
                return basisResource;
            }

            var matchingPartyType = basisResource.Kind switch
            {
                ProjectStructureTaskResourceKind.Person => ProjectPartyType.Person,
                ProjectStructureTaskResourceKind.Agent => ProjectPartyType.AiAgent,
                _ => (ProjectPartyType?)null
            };
            if (matchingPartyType.HasValue &&
                assigneeResolution.DirectAssignments.Any(assignment =>
                    assignment.PartyId == basisResource.ResourceId &&
                    assignment.PartyType == matchingPartyType.Value))
            {
                return basisResource;
            }
        }

        return assigneeResolution.Representative;
    }
}

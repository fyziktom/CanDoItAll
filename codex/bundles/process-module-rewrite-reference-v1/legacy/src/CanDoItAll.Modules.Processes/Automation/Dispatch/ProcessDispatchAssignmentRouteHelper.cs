namespace CanDoItAll.Modules.Processes;

internal static class ProcessDispatchAssignmentRouteHelper
{
    public static ProcessRunAssignment? ResolveCurrentAssignment(
        ProcessStepRun stepRun,
        IReadOnlyList<ProcessStepRoleAssignmentRequirement> stepRoleRequirements,
        IReadOnlyList<ProcessRunAssignment> runAssignments)
    {
        ArgumentNullException.ThrowIfNull(stepRun);

        if (stepRoleRequirements.Count == 0 || runAssignments.Count == 0)
        {
            return null;
        }

        var roleIds = stepRoleRequirements
            .Select(item => item.RoleRequirementId)
            .ToHashSet();
        var candidates = runAssignments
            .Where(item => roleIds.Contains(item.RoleRequirementId))
            .Where(item => !item.StepDefinitionId.HasValue || item.StepDefinitionId == stepRun.StepDefinitionId)
            .ToList();

        if (stepRun.CurrentExecutorPartyId.HasValue)
        {
            var partyMatch = candidates
                .Where(item => item.PartyId == stepRun.CurrentExecutorPartyId.Value)
                .OrderByDescending(item => item.StepDefinitionId == stepRun.StepDefinitionId)
                .FirstOrDefault();
            if (partyMatch is not null)
            {
                return partyMatch;
            }
        }

        return candidates
            .OrderByDescending(item => item.StepDefinitionId == stepRun.StepDefinitionId)
            .ThenByDescending(HasDispatchExecutableTarget)
            .FirstOrDefault();
    }

    public static bool HasDispatchExecutableTarget(ProcessRunAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        return assignment.PartyId.HasValue ||
            assignment.WorkflowDefinitionId.HasValue && assignment.WorkflowVersionId.HasValue;
    }

    public static bool IsWorkflowDispatchAssignment(
        ProcessRunAssignment? assignment,
        ProcessRoleRequirement? role)
    {
        return assignment is not null &&
            (ProcessExecutorKindNames.IsWorkflow(assignment.ExecutorKind) ||
             assignment.WorkflowDefinitionId.HasValue ||
             ProcessExecutorKindNames.IsWorkflow(role?.PreferredExecutorKind) ||
             role?.PreferredWorkflowDefinitionId.HasValue == true);
    }
}

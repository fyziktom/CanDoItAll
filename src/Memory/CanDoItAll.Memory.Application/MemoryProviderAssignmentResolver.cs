using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Application;

internal static class MemoryProviderAssignmentResolver
{
    public static bool TryResolve(
        MemoryProviderSelectionPolicy policy,
        MemoryProviderSelectionContext context,
        out MemoryProviderInstanceId providerId)
    {
        foreach (var assignment in policy.Assignments)
        {
            if (Matches(assignment, context))
            {
                providerId = assignment.ProviderInstanceId;
                return true;
            }
        }

        providerId = default;
        return false;
    }

    private static bool Matches(
        MemoryProviderAssignment assignment,
        MemoryProviderSelectionContext context)
    {
        return assignment.Scope switch
        {
            MemoryProviderAssignmentScope.Agent => Matches(context.AgentId, assignment.Key),
            MemoryProviderAssignmentScope.AgentRole => Matches(context.AgentRole, assignment.Key),
            MemoryProviderAssignmentScope.Workflow => Matches(context.WorkflowId, assignment.Key),
            MemoryProviderAssignmentScope.WorkflowNode => Matches(context.WorkflowNodeId, assignment.Key),
            MemoryProviderAssignmentScope.Process => Matches(context.ProcessId, assignment.Key),
            _ => false
        };
    }

    private static bool Matches(
        string? actual,
        string expected)
    {
        return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
    }
}

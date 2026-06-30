namespace CanDoItAll.AgentFramework.Core;

internal static class ProcessToolOperationAuthorizer
{
    public static ToolInvocationPolicyDecision? Evaluate(
        ToolInvocationPolicyContext context,
        string signature,
        IReadOnlyList<OperationRequirement> requirements)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirements);

        if (!IsGovernedProcessRun(context))
        {
            return null;
        }

        var allowedOperations = NormalizeOperationNames(context.ProcessStepAllowedOperations);
        if (allowedOperations.Count == 0)
        {
            var requiredOperations = requirements
                .SelectMany(requirement => requirement.AnyOf)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (requiredOperations.Length == 0)
            {
                return null;
            }

            return ToolInvocationPolicyDecision.Deny(
                signature,
                $"This governed step is missing an operation contract and cannot use tool '{context.ToolName}'. Required operation: {string.Join(" or ", requiredOperations)}.");
        }

        foreach (var requirement in requirements)
        {
            if (requirement.AnyOf.Count == 0 ||
                requirement.AnyOf.Any(allowedOperations.Contains))
            {
                continue;
            }

            return ToolInvocationPolicyDecision.Deny(
                signature,
                $"This governed step target scope '{context.ProcessStepTargetScope}' is not authorized to use tool '{context.ToolName}'. Required operation: {string.Join(" or ", requirement.AnyOf)}. Allowed operations: {string.Join(", ", allowedOperations.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))}.");
        }

        return null;
    }

    private static bool IsGovernedProcessRun(ToolInvocationPolicyContext context)
    {
        return string.Equals(context.SourceKind, "process-step", StringComparison.OrdinalIgnoreCase) ||
               !string.IsNullOrWhiteSpace(context.ProcessRunId) ||
               !string.IsNullOrWhiteSpace(context.ProcessStepId);
    }

    private static HashSet<string> NormalizeOperationNames(IReadOnlyList<string>? operations)
    {
        return operations?
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
    }
}

using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public static class WorkflowRuntimePolicyValidator
{
    public static IReadOnlyList<WorkflowValidationIssue> ValidateRegisteredBackendAvailability(WorkflowRuntimePolicy policy)
        => ValidateRegisteredBackendAvailability(policy, new WorkflowRuntimeBackendCatalog());

    public static IReadOnlyList<WorkflowValidationIssue> ValidateRegisteredBackendAvailability(
        WorkflowRuntimePolicy policy,
        IWorkflowRuntimeBackendCatalog backendCatalog)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(backendCatalog);

        var issues = new List<WorkflowValidationIssue>();
        if (!Enum.IsDefined(policy.PreferredBackend))
        {
            return issues;
        }

        var preferredBackend = backendCatalog.GetRequiredBackend(policy.PreferredBackend);
        if (!preferredBackend.IsRunnable)
        {
            issues.Add(new WorkflowValidationIssue(
                WorkflowValidationIssueCode.UnsupportedRuntimeBackend,
                $"Workflow runtime backend '{preferredBackend.Kind}' is not registered in this host. {preferredBackend.AvailabilityReason}"));
        }

        if ((policy.ExposeAzureFunctionsStatusEndpoint || policy.ExposeAzureFunctionsMcpTool) &&
            !backendCatalog.GetRequiredBackend(WorkflowRuntimeBackendKind.AzureFunctions).IsRunnable)
        {
            issues.Add(new WorkflowValidationIssue(
                WorkflowValidationIssueCode.InvalidWorkflowSettings,
                "Azure Functions workflow endpoints require a registered AzureFunctions backend."));
        }

        return issues;
    }
}

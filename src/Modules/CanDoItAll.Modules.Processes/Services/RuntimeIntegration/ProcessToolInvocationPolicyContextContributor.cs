using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Modules.Processes;

/// <summary>
/// Contributes the governed process restrictions to the provider-neutral tool
/// invocation policy context. This is the only place process semantics enter
/// tool governance: the runtime adapter builds a neutral context without
/// interpreting process fields, and this contributor maps the run's typed
/// process facts (identity, mutation gates, allowed operations, target scope)
/// from the execution audit state onto the policy context. Absent facts stay
/// absent — this contributor never widens what the run recorded.
/// </summary>
public sealed class ProcessToolInvocationPolicyContextContributor : IToolInvocationPolicyContextContributor
{
    public ToolInvocationPolicyContext Contribute(
        ToolInvocationPolicyContext context,
        WorkspaceExecutionAuditContext.WorkspaceExecutionAuditScopeState? auditScope)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (auditScope is null || string.IsNullOrWhiteSpace(auditScope.ProcessRunId))
        {
            return context;
        }

        return context with
        {
            ProcessRunId = auditScope.ProcessRunId,
            ProcessStepId = auditScope.ProcessStepId,
            ProcessAllowsProductMutation = auditScope.ProcessAllowsProductMutation,
            ProcessRequiresProductMutationBeforeManagedOutput =
                auditScope.ProcessRequiresProductMutationBeforeManagedOutput,
            ProcessProductMutationToolNames = auditScope.ProcessProductMutationToolNames,
            ProcessProductMutationRequiredBranchOutcomeKeys =
                auditScope.ProcessProductMutationRequiredBranchOutcomeKeys,
            ProcessStepAllowedOperations = auditScope.ProcessStepAllowedOperations,
            ProcessStepTargetScope = auditScope.ProcessStepTargetScope
        };
    }
}

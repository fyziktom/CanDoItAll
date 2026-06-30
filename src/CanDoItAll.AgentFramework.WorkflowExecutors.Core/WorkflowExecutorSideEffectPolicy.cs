using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public static class WorkflowExecutorSideEffectPolicy
{
    public static bool IsRetryPolicySafe(
        WorkflowExecutorDescriptor descriptor,
        WorkflowExecutorExecutionPolicy policy)
        => policy.MaxRetryAttempts == 0 ||
           !descriptor.SideEffects.WritesExternalState ||
           descriptor.SideEffects.AllowsIdempotentRetry ||
           descriptor.PermissionPolicy.RequiredCapabilities.HasFlag(WorkflowExecutorCapabilityFlags.IdempotentExternalMarker);

    public static void ThrowIfUnsafeRetryPolicy(
        WorkflowExecutorDescriptor descriptor,
        WorkflowExecutorExecutionPolicy policy,
        WorkflowNodeId nodeId)
    {
        if (IsRetryPolicySafe(descriptor, policy))
        {
            return;
        }

        throw WorkflowExecutorFailureDiagnosticMapper.CreateUnsafeRetryPolicyException(descriptor, policy, nodeId);
    }

    public static string CreateUnsafeRetryPolicyMessage(
        WorkflowExecutorDescriptor descriptor,
        WorkflowExecutorExecutionPolicy policy,
        WorkflowNodeId nodeId)
        => $"Workflow executor '{descriptor.Id}' on node '{nodeId}' writes external state and cannot use MaxRetryAttempts={policy.MaxRetryAttempts} without an idempotent retry-safe side-effect contract.";
}

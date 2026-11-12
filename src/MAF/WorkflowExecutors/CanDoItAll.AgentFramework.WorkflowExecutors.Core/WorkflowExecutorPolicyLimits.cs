using CanDoItAll.AgentFramework.Workflows.Definitions;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public static class WorkflowExecutorPolicyLimits
{
    public const int MinTimeoutSeconds = WorkflowExecutionPolicyRules.MinTimeoutSeconds;
    public const int MaxTimeoutSeconds = WorkflowExecutionPolicyRules.MaxTimeoutSeconds;
    public const int MinRetryAttempts = WorkflowExecutionPolicyRules.MinRetryAttempts;
    public const int MaxRetryAttempts = WorkflowExecutionPolicyRules.MaxRetryAttempts;
    public const int MinRetryDelayMilliseconds = WorkflowExecutionPolicyRules.MinRetryDelayMilliseconds;
    public const int MaxRetryDelayMilliseconds = WorkflowExecutionPolicyRules.MaxRetryDelayMilliseconds;

    public static bool IsValid(WorkflowExecutorExecutionPolicy policy)
        => WorkflowExecutionPolicyRules.IsValid(policy);

    public static void ThrowIfInvalid(
        WorkflowExecutorExecutionPolicy policy,
        WorkflowNodeId nodeId,
        WorkflowExecutorId executorId)
    {
        if (IsValid(policy))
        {
            return;
        }

        throw WorkflowExecutorFailureDiagnosticMapper.CreateInvalidPolicyException(nodeId, executorId);
    }
}

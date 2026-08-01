using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public static class WorkflowExecutorPolicyLimits
{
    public const int MinTimeoutSeconds = 1;
    public const int MaxTimeoutSeconds = 3600;
    public const int MinRetryAttempts = 0;
    public const int MaxRetryAttempts = 10;
    public const int MinRetryDelayMilliseconds = 0;
    public const int MaxRetryDelayMilliseconds = 600000;

    public static bool IsValid(WorkflowExecutorExecutionPolicy policy)
        => policy.TimeoutSeconds is >= MinTimeoutSeconds and <= MaxTimeoutSeconds &&
           policy.MaxRetryAttempts is >= MinRetryAttempts and <= MaxRetryAttempts &&
           policy.RetryDelayMilliseconds is >= MinRetryDelayMilliseconds and <= MaxRetryDelayMilliseconds;

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

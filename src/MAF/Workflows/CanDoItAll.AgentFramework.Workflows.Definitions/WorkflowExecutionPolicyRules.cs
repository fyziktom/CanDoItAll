using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Workflows.Definitions;

public static class WorkflowExecutionPolicyRules {
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

    public static bool IsRetryPolicySafe(WorkflowExecutorDescriptor descriptor, WorkflowExecutorExecutionPolicy policy)
        => policy.MaxRetryAttempts == 0 || !descriptor.SideEffects.WritesExternalState ||
           descriptor.SideEffects.AllowsIdempotentRetry ||
           descriptor.PermissionPolicy.RequiredCapabilities.HasFlag(WorkflowExecutorCapabilityFlags.IdempotentExternalMarker);
}

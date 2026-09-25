namespace CanDoItAll.Processes.Drivers.Abstractions;

public static class ProcessExecutionAdapterDiagnosticCodes
{
    public const string AgentProviderRejected = "process.adapter.agent_provider_rejected";
    public const string AgentToolPermissionDenied = "process.adapter.agent_tool_permission_denied";
    public const string AgentExecutionFailed = "process.adapter.agent_execution_failed";
    public const string AgentExecutionCancelled = "process.adapter.agent_execution_cancelled";

    public const string SubprocessChildBlocked = "process.adapter.subprocess_child_blocked";
    public const string SubprocessChildFailed = "process.adapter.subprocess_child_failed";
    public const string AgentTransientExecutionRetry = "process.adapter.agent_transient_execution_retry";
    public const string AgentTransientExecutionBeforeSideEffects =
        "process.adapter.agent_transient_execution_before_side_effects";
    public const string AgentInterruptedExecutionReplayUnsafe =
        "process.adapter.agent_interrupted_execution_replay_unsafe";
}

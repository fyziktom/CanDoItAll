namespace CanDoItAll.AgentFramework.Workflows.Abstractions;

public enum WorkflowBackendResumeFailureKind
{
    ExactWorkflowVersionMissing,
    ExactWorkflowVersionMismatch,
    CompilationFailed,
    CompilerContractMismatch,
    TopologyMismatch,
    CheckpointMissing,
    CheckpointCorrupt,
    CheckpointIncompatible,
    RequestMismatch,
    PortMismatch,
    ResponseMismatch
}

public sealed class WorkflowBackendResumeException : InvalidOperationException
{
    public WorkflowBackendResumeException(
        WorkflowBackendResumeFailureKind kind,
        string safeMessage,
        Exception? innerException = null)
        : base(safeMessage, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        Kind = kind;
        SafeMessage = safeMessage.Trim();
    }

    public WorkflowBackendResumeFailureKind Kind { get; }

    public string SafeMessage { get; }
}

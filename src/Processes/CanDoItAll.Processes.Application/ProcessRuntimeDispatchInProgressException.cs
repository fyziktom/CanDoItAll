using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessRuntimeDispatchInProgressException(ProcessExecutionRunId executionRunId)
    : Exception($"Execution '{executionRunId.Value:D}' already owns this Process dispatch claim; its outcome is still pending.") {
    public ProcessExecutionRunId ExecutionRunId { get; } = executionRunId;
}

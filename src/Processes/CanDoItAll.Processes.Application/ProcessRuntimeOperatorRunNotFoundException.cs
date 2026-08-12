using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessRuntimeOperatorRunNotFoundException(ProcessRunId runId)
    : InvalidOperationException($"Process run '{runId}' was not found.")
{
    public ProcessRunId RunId { get; } = runId;
}

using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessRuntimeDispatchDeferredException : Exception
{
    public ProcessRuntimeDispatchDeferredException(
        string message,
        ProcessRunId? deferredRunId = null)
        : base(message)
    {
        DeferredRunId = deferredRunId;
    }

    public ProcessRunId? DeferredRunId { get; }
}

using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Application;

public sealed record ProcessRuntimeDispatchQueueRequest(
    ProcessRunId RunId,
    string RequestedBy,
    bool IsRecovery = false);

public interface IProcessRuntimeDispatchQueue
{
    ValueTask EnqueueAsync(
        ProcessRuntimeDispatchQueueRequest request,
        CancellationToken cancellationToken = default);
}

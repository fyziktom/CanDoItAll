using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Runtime;

public interface IProcessRuntimeStateStore
{
    ValueTask<bool> ExistsAsync(ProcessInstanceId instanceId, CancellationToken cancellationToken = default);
}

using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public sealed record ProcessCancellationRuntimeRun(ProcessRunId RunId, ProcessRuntimeStatus Status);

public interface IProcessCancellationRuntimeReader {
    Task<ProcessRunId?> NextAsync(ProcessRunId rootRunId, ProcessRunId? after, CancellationToken cancellationToken);
    Task<ProcessCancellationRuntimeRun?> ReadAsync(ProcessRunId rootRunId, ProcessRunId runId, CancellationToken cancellationToken);
}

public sealed record ProcessCancellationExecutionProgress(ProcessExecutionRunId? After, bool Exhausted,
    ProcessCancellationRecoveryPending Pending = ProcessCancellationRecoveryPending.None);

public interface IProcessExecutionRecovery {
    Task<ProcessCancellationExecutionProgress> InspectNextAsync(ProcessRunId rootRunId, ProcessRunId runId,
        ProcessExecutionRunId? after, CancellationToken cancellationToken);
}

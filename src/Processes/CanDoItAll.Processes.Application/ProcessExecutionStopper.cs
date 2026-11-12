using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Application;

public interface IProcessExecutionStopper {
    Task<ProcessCancellationRecoveryPending> StopAsync(ProcessRunId rootRunId, ProcessRunId runId,
        ProcessExecutionRunId executionRunId, CancellationToken cancellationToken = default);
}

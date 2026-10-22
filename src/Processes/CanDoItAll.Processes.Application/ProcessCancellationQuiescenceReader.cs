using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Application;

public interface IProcessCancellationQuiescenceReader {
    Task<ProcessCancellationRecoveryPending> ReadAsync(ProcessRunId rootRunId, CancellationToken cancellationToken);
}

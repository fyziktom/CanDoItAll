using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public enum ProcessCancellationTracking { NotRequested, ChildRun, LegacyUntracked, Pending, Completed, ReconciliationRequired }

public sealed record ProcessCancellationProgress(ProcessRunId RunId, ProcessRunId RootRunId,
    ProcessRuntimeStatus RuntimeStatus, DateTimeOffset RuntimeUpdatedAtUtc, ProcessCancellationTracking Tracking,
    ProcessCancellationRecoveryProgress? Recovery = null);

public sealed record ProcessCancellationRecoveryProgress(ProcessCancellationRecoveryOrigin Origin, RuntimeCommandId? BarrierCommandId,
    long Revision, DateTimeOffset UpdatedAtUtc, ProcessCancellationRecoveryStage? Stage, ProcessCancellationRecoveryPending Pending);

public interface IProcessCancellationProgressReader {
    Task<ProcessCancellationProgress?> ReadAsync(ProcessRunId runId, CancellationToken cancellationToken = default);
}

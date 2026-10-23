using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;

namespace CanDoItAll.Modules.Processes;

public interface IProcessCancellationClient {
    Task<ProcessCancellationClientView> ReadAsync(ProcessRunId runId, CancellationToken cancellationToken = default);
    Task<ProcessCancellationClientResult> RequestAsync(ProcessRunId runId, CancellationToken cancellationToken = default);
}

public sealed record ProcessCancellationClientView(ProcessCancellationProgress? Progress, bool CanRequest, bool RootRecoveryAvailable);

public abstract record ProcessCancellationClientResult {
    private ProcessCancellationClientResult() { }
    public abstract ProcessRunId RunId { get; }

    public sealed record Root(ProcessRootCancellationAdmissionResult Admission) : ProcessCancellationClientResult {
        public override ProcessRunId RunId => Admission.RunId;
    }

    public sealed record Child(ProcessRuntimeRunCancellationResult Commit) : ProcessCancellationClientResult {
        public override ProcessRunId RunId => Commit.RunId;
    }
}

public sealed class ProcessCancellationAccessDeniedException() : InvalidOperationException("Current Process access is required for this action.");

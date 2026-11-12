using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessRootCancellationAdmissionRequiredException(ProcessRunId runId)
    : InvalidOperationException("Root cancellation requires the finite admission and recovery contract.") {
    public ProcessRunId RunId { get; } = runId;
}

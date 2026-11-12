using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public enum ProcessDecisionRefreshCaptureInvocationStatus { Started, PreviouslyAdmitted, Stopped }

public sealed record ProcessDecisionRefreshCaptureInvocationResult(ProcessDecisionRefreshCaptureInvocationStatus Status,
    ProcessDecisionRefreshCaptureState State);

public interface IProcessDecisionRefreshCaptureStore {
    Task<ProcessDecisionRefreshCaptureState?> ReadAsync(ProcessRunId runId, RuntimeCommandId reservationId, CancellationToken cancellationToken = default);
    Task<ProcessDecisionRefreshCaptureReturned?> ReadReturnedAsync(ProcessRunId runId, RuntimeCommandId reservationId, CancellationToken cancellationToken = default);
    Task<ProcessDecisionRefreshCaptureInvocationResult> BeginAsync(ProcessDecisionRefreshCaptureReservation reservation,
        ProcessDecisionCaptureInvocationId invocation, CancellationToken cancellationToken = default);
    Task<ProcessDecisionRefreshCaptureState> ReturnAsync(ProcessDecisionRefreshCaptureReservation reservation,
        ProcessDecisionCaptureInvocationId invocation, ProcessDecisionRefreshCaptureReturned returned, CancellationToken cancellationToken = default);
    Task<ProcessDecisionRefreshCaptureState> CloseNeverInvokedAsync(ProcessDecisionRefreshCaptureReservation reservation, CancellationToken cancellationToken = default);
}

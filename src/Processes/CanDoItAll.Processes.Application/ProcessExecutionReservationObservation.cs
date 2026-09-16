namespace CanDoItAll.Processes.Application;

public sealed record ProcessExecutionReservationObservation(
    ProcessExecutionDispatchAuthority Dispatch, IReadOnlySet<Guid> AcknowledgedExecutionRunIds);

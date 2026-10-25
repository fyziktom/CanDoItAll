namespace CanDoItAll.Processes.Runtime;

public sealed record ProcessDecisionRefreshCaptureCommit(ProcessDecisionRefreshCaptureReservation Reservation,
    ProcessDecisionRefreshCaptureDisposition? Disposition = null);

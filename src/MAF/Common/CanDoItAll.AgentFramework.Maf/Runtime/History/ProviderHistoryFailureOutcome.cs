using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.Providers;

namespace CanDoItAll.AgentFramework.Maf;

internal static class ProviderHistoryFailureOutcome {
    public static HistoryOutcome Classify(Exception exception, CancellationToken callerToken) {
        var cancelled = false;
        var timedOut = false;
        for (Exception? cause = exception; cause is not null; cause = cause.InnerException) {
            cancelled |= cause is OperationCanceledException;
            timedOut |= cause is TimeoutException or ProviderFailureBoundaryException { IsTimeout: true };
        }
        if (cancelled && callerToken.IsCancellationRequested) {
            return HistoryOutcome.Cancelled;
        }
        if (timedOut) {
            return HistoryOutcome.TimedOut;
        }
        return cancelled ? HistoryOutcome.Cancelled : HistoryOutcome.Failed;
    }
}

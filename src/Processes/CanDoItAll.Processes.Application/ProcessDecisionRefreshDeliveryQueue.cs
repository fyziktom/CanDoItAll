using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public enum ProcessDecisionRefreshDeliveryDisposition { Ready, AwaitingCapture, ReviewRequired, Completed }
public enum ProcessDecisionRefreshDeliveryReason {
    Admitted, PendingStop, PendingChild, PendingCapture, Contended, UnknownInvocation,
    LegacyUnknown, SourceProjectionRequired, ReviewRequired, InvalidEvidence, Advanced, CompletedOriginal, Stopped
}
public enum ProcessDecisionRefreshDeliveryCheckpointStatus { Released, NotOwned }

public sealed record ProcessDecisionRefreshDeliveryClaim(ProcessRunId RootRunId, ProcessRunId RunId, RuntimeCommandId AdmissionId,
    Guid LeaseToken, long LeaseGeneration, long ProgressRevision, long WakeRevision, DateTimeOffset LeaseExpiresAtUtc) {
    public void Validate() {
        if (RootRunId.Value == Guid.Empty || RunId.Value == Guid.Empty || AdmissionId.Value == Guid.Empty || LeaseToken == Guid.Empty ||
                LeaseGeneration < 1 || ProgressRevision < 0 || WakeRevision < 0 || LeaseExpiresAtUtc == default || LeaseExpiresAtUtc.Offset != TimeSpan.Zero) {
            throw new InvalidDataException("Refresh delivery requires its original admission identity and a valid lease.");
        }
    }
}

public sealed record ProcessDecisionRefreshDeliveryCheckpoint(ProcessDecisionRefreshDeliveryReason Reason, TimeSpan? RetryAfter = null) {
    public ProcessDecisionRefreshDeliveryDisposition Disposition => Reason switch {
        ProcessDecisionRefreshDeliveryReason.Admitted or ProcessDecisionRefreshDeliveryReason.PendingStop or
            ProcessDecisionRefreshDeliveryReason.PendingChild or ProcessDecisionRefreshDeliveryReason.PendingCapture or
            ProcessDecisionRefreshDeliveryReason.Contended => ProcessDecisionRefreshDeliveryDisposition.Ready,
        ProcessDecisionRefreshDeliveryReason.UnknownInvocation => ProcessDecisionRefreshDeliveryDisposition.AwaitingCapture,
        ProcessDecisionRefreshDeliveryReason.LegacyUnknown or ProcessDecisionRefreshDeliveryReason.SourceProjectionRequired or
            ProcessDecisionRefreshDeliveryReason.ReviewRequired or ProcessDecisionRefreshDeliveryReason.InvalidEvidence => ProcessDecisionRefreshDeliveryDisposition.ReviewRequired,
        ProcessDecisionRefreshDeliveryReason.Advanced or ProcessDecisionRefreshDeliveryReason.CompletedOriginal or
            ProcessDecisionRefreshDeliveryReason.Stopped => ProcessDecisionRefreshDeliveryDisposition.Completed,
        _ => throw new InvalidDataException("Refresh delivery has an unknown outcome.")
    };

    public void Validate() {
        if (Disposition == ProcessDecisionRefreshDeliveryDisposition.Ready ? RetryAfter is null || RetryAfter < TimeSpan.Zero || RetryAfter > TimeSpan.FromHours(1)
                : RetryAfter is not null) {
            throw new InvalidDataException("Runnable refresh delivery requires a bounded retry delay; parked or completed delivery cannot retry automatically.");
        }
    }
}

public interface IProcessDecisionRefreshDeliveryQueue {
    Task<ProcessDecisionRefreshDeliveryClaim?> ClaimAsync(TimeSpan leaseDuration, CancellationToken cancellationToken = default);
    Task<ProcessDecisionRefreshDeliveryCheckpointStatus> CheckpointAsync(ProcessDecisionRefreshDeliveryClaim claim,
        ProcessDecisionRefreshDeliveryCheckpoint checkpoint, CancellationToken cancellationToken = default);
}

using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public sealed record ProcessHistoricalReadContext {
    public ProcessHistoricalReadContext(Guid databaseProfileId, string databaseFingerprint, long generation) {
        ArgumentOutOfRangeException.ThrowIfEqual(databaseProfileId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseFingerprint);
        ArgumentOutOfRangeException.ThrowIfNegative(generation);
        DatabaseProfileId = databaseProfileId;
        DatabaseFingerprint = databaseFingerprint;
        Generation = generation;
    }

    public Guid DatabaseProfileId { get; }
    public string DatabaseFingerprint { get; }
    public long Generation { get; }
}

public interface IProcessOriginalReceiptAuthorityReader {
    Task<ProcessExecutionDispatchAuthorityResult> ObserveOriginalForReceiptReadAsync(ProcessExecutionClaimEvidence original,
        CancellationToken cancellationToken = default);

    Task<ProcessExecutionDispatchAuthorityResult> ObserveOriginalDecisionForReceiptReadAsync(ProcessExecutionClaimEvidence original,
        ProcessDecisionChildMaterialBinding binding, ProcessDecisionExecutionContract supportedExecution,
        ProcessDecisionContentContract? supportedContent, CancellationToken cancellationToken = default);
}

public interface IProcessHistoricalSourceAuthorityObserver {
    Task<ProcessSourceAuthorityObservation> ObserveHistoricalReadAsync(ProcessLaunchAuthority authority,
        ProcessHistoricalReadContext currentContext, CancellationToken cancellationToken = default);
}

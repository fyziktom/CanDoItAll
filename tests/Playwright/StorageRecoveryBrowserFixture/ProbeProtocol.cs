namespace CanDoItAll.Tests.Playwright.StorageRecovery;

public enum RecoveryProbeCommand { Verify, Stop }
public sealed record RecoveryBrowserCase(Guid ProjectId, Guid IntentId, bool NativeCommitted, Guid RunId, string OriginalContentHash);
public sealed record RecoveryBrowserProof(Guid IntentId, string NodeId, string ContentHash, string PreparedCommandHash,
    bool StorageReceiptUnchanged, bool WorkflowRunUnchanged, int DriverResolutions, int AgentExecutions, int ProviderRequests);
public static class RecoveryProbeProtocol {
    public const string Ready = "RECOVERY-PROBE-READY:";
    public const string Verified = "RECOVERY-PROBE-VERIFIED:";
}

namespace CanDoItAll.Tests.ProcessRecovery;

public enum RecoveryHostMode { BeforeAccounting, AfterAccounting, Resume }
public enum RecoveryHostFrameKind { Held, Replayed, Completed }

public sealed record RecoveryHostFrame(RecoveryHostFrameKind Kind, Guid RootRunId, Guid? ActionId,
    long LeaseGeneration, Guid LeaseToken, long ProgressRevision, int ProcessId, DateTimeOffset ProcessStartedUtc);

public static class RecoveryHostProtocol {
    public const string EnvironmentRoot = "CANDOITALL_RECOVERY_TEST_ENVIRONMENT_ROOT";
    public const int MaximumFrameCharacters = 4096;
}

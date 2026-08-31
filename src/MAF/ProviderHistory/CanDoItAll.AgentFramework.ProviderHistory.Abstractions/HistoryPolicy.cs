namespace CanDoItAll.AgentFramework.ProviderHistory;

public sealed record HistoryPolicy {
    public HistoryCaptureMode CaptureMode { get; init; } = HistoryCaptureMode.Light;
    public int MetadataRetentionDays { get; init; } = 30;
    public int DetailRetentionDays { get; init; } = 7;
    public int MaximumTextBytes { get; init; } = 32 * 1024;
    public long DetailQuotaBytes { get; init; } = 256L * 1024 * 1024;
    public int BatchSize { get; init; } = 500;
}

public sealed record HistoryPolicySnapshot(HistoryPolicy Policy, long Version);
public sealed record HistoryPolicyUpdate(HistoryPolicy Policy, long ExpectedVersion, bool ApplyShorterRetention);

public sealed record HistoryRetentionPreview(int MetadataRecords, int DetailRecords, int Limit, bool ExceedsLimit);

public interface IProviderHistoryPolicyService {
    Task<HistoryPolicySnapshot> GetAsync(CancellationToken cancellationToken);
    Task<HistoryRetentionPreview> PreviewShorterRetentionAsync(HistoryPolicy policy, CancellationToken cancellationToken);
    Task<HistoryPolicySnapshot> UpdateAsync(HistoryPolicyUpdate update, CancellationToken cancellationToken);
}

namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

public enum HistoryDetailPart { Input, Response }

public sealed class HistoryDetailRow {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PartitionId { get; set; }
    public Guid RequestId { get; set; }
    public Guid? EntryId { get; set; }
    public long InputRevision { get; set; }
    public HistoryDetailPart Part { get; set; }
    public HistoryDetailState State { get; set; } = HistoryDetailState.Captured;
    public string ProtectedText { get; set; } = "";
    public int StoredBytes { get; set; }
    public int CapturedBytes { get; set; }
    public long OriginalBytes { get; set; }
    public HistoryDetailFlags Flags { get; set; }
    public DateTimeOffset CapturedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
}

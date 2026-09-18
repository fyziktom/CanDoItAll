namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Entities;

internal sealed class LlmChatDefinitionCreateReceiptRow {
    public string Producer { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public string HistoryNamespace { get; set; } = string.Empty;
    public Guid IntentId { get; set; }
    public int SemanticVersion { get; set; }
    public string SemanticFingerprint { get; set; } = string.Empty;
    public Guid DefinitionId { get; set; }
    public int DefinitionRevision { get; set; }
    public long OriginalConcurrencyToken { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

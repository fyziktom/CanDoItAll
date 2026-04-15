namespace CanDoItAll.AgentFramework.Models;

public sealed record AgentMemoryRecord(
    Guid Id,
    Guid AgentId,
    MemoryKind Kind,
    string Title,
    string Content,
    string Source,
    int Importance,
    string MetadataJson,
    DateTimeOffset CreatedAtUtc);

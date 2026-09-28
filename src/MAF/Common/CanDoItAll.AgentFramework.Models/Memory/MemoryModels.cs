namespace CanDoItAll.AgentFramework.Models;

/// <summary>
/// Stored workspace memory note of an agent, returned by <c>GET /api/agents/{agentId}/memory</c>. Notes are kept in
/// the agent catalog; they are not injected into agent runs and are separate from memory providers. Deleting the agent
/// deletes its notes.
/// </summary>
/// <param name="Id">Identifier of the note, used by <c>DELETE /api/agents/memory/{memoryId}</c>.</param>
/// <param name="AgentId">Identifier of the agent the note belongs to.</param>
/// <param name="Kind">
/// Category of the note, as a JSON integer: 0 Fact, 1 Preference, 2 Context, 3 FollowUp, 4 Architecture.
/// </param>
/// <param name="Title">Short title of the note.</param>
/// <param name="Content">Text of the note.</param>
/// <param name="Source">Free-text origin of the note, for example <c>manual</c>.</param>
/// <param name="Importance">Caller-assigned importance, as saved; the server applies no range.</param>
/// <param name="MetadataJson">Optional metadata as a JSON string containing JSON, as saved; may be empty.</param>
/// <param name="CreatedAtUtc">Instant, with offset, at which the note was first saved.</param>
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

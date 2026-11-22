using System.Collections.Frozen;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;

public sealed record LlmChatSharingSnapshot(IReadOnlySet<Guid> Published, IReadOnlySet<Guid> ApiUnavailable) {
    public static LlmChatSharingSnapshot Default { get; } = new(FrozenSet<Guid>.Empty, FrozenSet<Guid>.Empty);
}

public interface ILlmChatSharingReader {
    Task<LlmChatSharingSnapshot> ReadAsync(Guid? definitionId, CancellationToken token);
}

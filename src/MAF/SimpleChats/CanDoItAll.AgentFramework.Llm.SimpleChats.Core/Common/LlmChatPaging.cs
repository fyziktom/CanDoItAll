using CanDoItAll.AgentFramework.Llm.SimpleChats.Conversations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Definitions;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Common;

public sealed record LlmChatPage<TItem, TCursor>(
    IReadOnlyList<TItem> Items,
    TCursor? NextCursor)
    where TCursor : struct;

public readonly record struct LlmChatDefinitionCursor(
    DateTimeOffset UpdatedAtUtc,
    LlmChatDefinitionId DefinitionId);

public readonly record struct LlmChatConversationCursor(
    DateTimeOffset UpdatedAtUtc,
    LlmChatConversationId ConversationId);

public readonly record struct LlmChatTranscriptCursor
{
    public LlmChatTranscriptCursor(long sequence)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sequence, 1);
        Sequence = sequence;
    }

    public long Sequence { get; }
}

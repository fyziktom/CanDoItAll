namespace CanDoItAll.Modules.LlmChats.Common;

public sealed record LlmChatPage<T>(
    IReadOnlyList<T> Items,
    int? NextOffset);

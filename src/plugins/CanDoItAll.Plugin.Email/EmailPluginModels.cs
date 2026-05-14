namespace CanDoItAll.Modules.Plugins;

public sealed record PluginEmailMessageBatch(
    string Provider,
    string FilterKind,
    string FilterValue,
    int Count,
    IReadOnlyList<PluginEmailMessage> Messages);

public sealed record PluginEmailMessage(
    string Id,
    string ThreadId,
    string Subject,
    string From,
    string ReceivedAt,
    string Snippet,
    string BodyText,
    IReadOnlyList<string> Labels,
    string WebLink);

namespace CanDoItAll.Modules.Plugins;

public static class EmailWorkflowSideEffectConstants
{
    public const string ProcessedMarkerReceiptSchema = "workflow-email-processed-marker/v1";
    public const string ExternalReadReceiptSchema = "workflow-email-external-read/v1";
    public const string Operation = "processed-marker";
    public const string CommitMode = "Commit";
    public const string PreviewMode = "Preview";
    public const string GmailIdempotencyPrefix = "gmail:";
    public const string Office365IdempotencyPrefix = "office365:";
}

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

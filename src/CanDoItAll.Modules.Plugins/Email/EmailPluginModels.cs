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

public sealed record GmailWorkflowExecutorSettings
{
    public string ConnectionId { get; init; } = string.Empty;

    public string Label { get; init; } = GmailPluginConstants.DefaultSourceLabel;

    public string ProcessedLabel { get; init; } = GmailPluginConstants.DefaultProcessedLabel;

    public int MaxMessages { get; init; } = 1;
}

public sealed record GmailMarkProcessedWorkflowExecutorSettings
{
    public string ConnectionId { get; init; } = string.Empty;

    public string SourceLabel { get; init; } = GmailPluginConstants.DefaultSourceLabel;

    public string ProcessedLabel { get; init; } = GmailPluginConstants.DefaultProcessedLabel;

    public string MessageIdJsonPath { get; init; } = "$.inputPayload.runContext.gmailProcessing.messageIds[0]";
}

public sealed record Office365WorkflowExecutorSettings
{
    public string ConnectionId { get; init; } = string.Empty;

    public string Category { get; init; } = Office365PluginConstants.DefaultSourceCategory;

    public string ProcessedCategory { get; init; } = Office365PluginConstants.DefaultProcessedCategory;

    public int MaxMessages { get; init; } = 5;
}

public sealed record Office365MarkProcessedWorkflowExecutorSettings
{
    public string ConnectionId { get; init; } = string.Empty;

    public string SourceCategory { get; init; } = Office365PluginConstants.DefaultSourceCategory;

    public string ProcessedCategory { get; init; } = Office365PluginConstants.DefaultProcessedCategory;

    public string MessageIdJsonPath { get; init; } = "$.inputPayload.runContext.office365Processing.messageIds[0]";
}

public sealed record GmailMessageLabelMutationResult(
    string Provider,
    string MessageId,
    string SourceLabel,
    string ProcessedLabel,
    bool SourceLabelRemoved,
    bool ProcessedLabelAdded);

public sealed record Office365MessageCategoryMutationResult(
    string Provider,
    string MessageId,
    string SourceCategory,
    string ProcessedCategory,
    bool SourceCategoryRemoved,
    bool ProcessedCategoryAdded,
    bool ProcessedCategoryCreated,
    IReadOnlyList<string> Categories);

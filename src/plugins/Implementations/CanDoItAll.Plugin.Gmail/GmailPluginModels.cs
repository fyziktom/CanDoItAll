namespace CanDoItAll.Modules.Plugins;

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

public sealed record GmailMessageLabelMutationResult(
    string Provider,
    string MessageId,
    string SourceLabel,
    string ProcessedLabel,
    bool SourceLabelRemoved,
    bool ProcessedLabelAdded);

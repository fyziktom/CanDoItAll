namespace CanDoItAll.Modules.Plugins;

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

public sealed record Office365MessageCategoryMutationResult(
    string Provider,
    string MessageId,
    string SourceCategory,
    string ProcessedCategory,
    bool SourceCategoryRemoved,
    bool ProcessedCategoryAdded,
    bool ProcessedCategoryCreated,
    IReadOnlyList<string> Categories);

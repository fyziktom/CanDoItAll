namespace CanDoItAll.Modules.Plugins;

public sealed record Office365WorkflowExecutorSettings
{
    public string ConnectionId { get; init; } = string.Empty;

    public string Category { get; init; } = Office365PluginConstants.DefaultSourceCategory;

    public string ProcessedCategory { get; init; } = Office365PluginConstants.DefaultProcessedCategory;

    public int MaxMessages { get; init; } = 5;
}

public enum Office365EmailAddressMatchMode
{
    FromOrSenderEquals,
    FromEquals,
    SenderEquals
}

public enum Office365NoMessageBehavior
{
    SuccessNoMessages,
    Fail
}

public sealed record Office365MessageAddressWorkflowExecutorSettings
{
    public string ConnectionId { get; init; } = string.Empty;

    public string ConnectionIdJsonPath { get; init; } = string.Empty;

    public string EmailAddress { get; init; } = string.Empty;

    public string EmailAddressJsonPath { get; init; } = "$.emailAddress";

    public string ProcessedCategory { get; init; } = Office365PluginConstants.DefaultEmailWatchProcessedCategory;

    public string ProcessedCategoryJsonPath { get; init; } = string.Empty;

    public string MailFolderId { get; init; } = string.Empty;

    public Office365EmailAddressMatchMode MatchMode { get; init; } = Office365EmailAddressMatchMode.FromOrSenderEquals;

    public int MaxCandidateMessages { get; init; } = 25;

    public int LookbackHours { get; init; } = 336;

    public string LookbackHoursJsonPath { get; init; } = string.Empty;

    public int MaxBodyCharacters { get; init; } = 60000;

    public bool IncludeBody { get; init; } = true;

    public Office365NoMessageBehavior NoMessageBehavior { get; init; } = Office365NoMessageBehavior.SuccessNoMessages;
}

public sealed record Office365MessageAddressFilterSettings
{
    public string EmailAddress { get; init; } = string.Empty;

    public string ProcessedCategory { get; init; } = Office365PluginConstants.DefaultEmailWatchProcessedCategory;

    public string MailFolderId { get; init; } = string.Empty;

    public Office365EmailAddressMatchMode MatchMode { get; init; } = Office365EmailAddressMatchMode.FromOrSenderEquals;

    public int MaxCandidateMessages { get; init; } = 25;

    public int LookbackHours { get; init; } = 336;

    public int MaxBodyCharacters { get; init; } = 60000;

    public bool IncludeBody { get; init; } = true;
}

public sealed record Office365MarkProcessedWorkflowExecutorSettings
{
    public string ConnectionId { get; init; } = string.Empty;

    public string ConnectionIdJsonPath { get; init; } = string.Empty;

    public string SourceCategory { get; init; } = Office365PluginConstants.DefaultSourceCategory;

    public string ProcessedCategory { get; init; } = Office365PluginConstants.DefaultProcessedCategory;

    public string ProcessedCategoryJsonPath { get; init; } = string.Empty;

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

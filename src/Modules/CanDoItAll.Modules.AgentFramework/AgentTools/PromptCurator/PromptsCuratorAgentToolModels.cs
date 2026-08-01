using System.Text.Json.Serialization;
using CanDoItAll.Modules.Prompts;

namespace CanDoItAll.Modules.AgentFramework;

public sealed record PromptsCuratorCatalogSearchInput
{
    [JsonConstructor]
    public PromptsCuratorCatalogSearchInput(
        string? text = null,
        IReadOnlyList<string>? tags = null,
        PromptGalleryItemKind? kind = null,
        PromptArtifactStatus? status = null,
        bool includeArchived = false,
        int pageIndex = 0,
        int pageSize = 25)
    {
        var query = new PromptGalleryQuery(
            text,
            tags,
            kind,
            status,
            includeArchived,
            Provider: null,
            Model: null,
            pageIndex,
            pageSize);
        query.Validate();

        Text = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        Tags = tags?
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
        Kind = kind;
        Status = status;
        IncludeArchived = includeArchived;
        PageIndex = pageIndex;
        PageSize = pageSize;
    }

    public string? Text { get; }

    public IReadOnlyList<string> Tags { get; }

    public PromptGalleryItemKind? Kind { get; }

    public PromptArtifactStatus? Status { get; }

    public bool IncludeArchived { get; }

    public int PageIndex { get; }

    public int PageSize { get; }
}

public sealed record PromptsCuratorItemEditorInput
{
    [JsonConstructor]
    public PromptsCuratorItemEditorInput(Guid promptArtifactId)
    {
        if (promptArtifactId == Guid.Empty)
        {
            throw new ArgumentException("Prompt artifact id cannot be empty.", nameof(promptArtifactId));
        }

        PromptArtifactId = promptArtifactId;
    }

    public Guid PromptArtifactId { get; }
}

public sealed record PromptsCuratorDraftCreateInput
{
    [JsonConstructor]
    public PromptsCuratorDraftCreateInput(
        Guid? projectId,
        Guid? collectionId,
        string title,
        string summary,
        PromptGalleryItemKind kind,
        string phase,
        string content,
        IReadOnlyList<string>? tags = null,
        IReadOnlyList<PromptProviderModel>? supportedModels = null,
        IReadOnlyList<PromptGalleryConsumer>? supportedConsumers = null,
        PromptModelRecommendations? recommendations = null)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(phase);
        ArgumentNullException.ThrowIfNull(content);

        ProjectId = projectId;
        CollectionId = collectionId;
        Title = title;
        Summary = summary;
        Kind = kind;
        Phase = phase;
        Content = content;
        Tags = tags ?? [];
        SupportedModels = supportedModels ?? [];
        SupportedConsumers = supportedConsumers ?? [];
        Recommendations = recommendations;
    }

    public Guid? ProjectId { get; }

    public Guid? CollectionId { get; }

    public string Title { get; }

    public string Summary { get; }

    public PromptGalleryItemKind Kind { get; }

    public string Phase { get; }

    public string Content { get; }

    public IReadOnlyList<string> Tags { get; }

    public IReadOnlyList<PromptProviderModel> SupportedModels { get; }

    public IReadOnlyList<PromptGalleryConsumer> SupportedConsumers { get; }

    public PromptModelRecommendations? Recommendations { get; }
}

public sealed record PromptsCuratorDraftUpdateInput
{
    [JsonConstructor]
    public PromptsCuratorDraftUpdateInput(
        Guid promptArtifactId,
        DateTimeOffset expectedUpdatedAtUtc,
        Guid? projectId,
        Guid? collectionId,
        string title,
        string summary,
        PromptGalleryItemKind kind,
        string phase,
        string content,
        IReadOnlyList<string>? tags = null,
        IReadOnlyList<PromptProviderModel>? supportedModels = null,
        IReadOnlyList<PromptGalleryConsumer>? supportedConsumers = null,
        PromptModelRecommendations? recommendations = null)
    {
        if (promptArtifactId == Guid.Empty)
        {
            throw new ArgumentException("Prompt artifact id cannot be empty.", nameof(promptArtifactId));
        }

        if (expectedUpdatedAtUtc == default)
        {
            throw new ArgumentException(
                "ExpectedUpdatedAtUtc is required when updating a Prompt Gallery item.",
                nameof(expectedUpdatedAtUtc));
        }

        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(phase);
        ArgumentNullException.ThrowIfNull(content);

        PromptArtifactId = promptArtifactId;
        ExpectedUpdatedAtUtc = expectedUpdatedAtUtc;
        ProjectId = projectId;
        CollectionId = collectionId;
        Title = title;
        Summary = summary;
        Kind = kind;
        Phase = phase;
        Content = content;
        Tags = tags ?? [];
        SupportedModels = supportedModels ?? [];
        SupportedConsumers = supportedConsumers ?? [];
        Recommendations = recommendations;
    }

    public Guid PromptArtifactId { get; }

    public DateTimeOffset ExpectedUpdatedAtUtc { get; }

    public Guid? ProjectId { get; }

    public Guid? CollectionId { get; }

    public string Title { get; }

    public string Summary { get; }

    public PromptGalleryItemKind Kind { get; }

    public string Phase { get; }

    public string Content { get; }

    public IReadOnlyList<string> Tags { get; }

    public IReadOnlyList<PromptProviderModel> SupportedModels { get; }

    public IReadOnlyList<PromptGalleryConsumer> SupportedConsumers { get; }

    public PromptModelRecommendations? Recommendations { get; }
}

public sealed record PromptsCuratorVersionCreateInput
{
    [JsonConstructor]
    public PromptsCuratorVersionCreateInput(
        Guid promptArtifactId,
        DateTimeOffset expectedUpdatedAtUtc,
        string creationReason,
        string outputFormat = "Markdown")
    {
        if (promptArtifactId == Guid.Empty)
        {
            throw new ArgumentException("Prompt artifact id cannot be empty.", nameof(promptArtifactId));
        }

        if (expectedUpdatedAtUtc == default)
        {
            throw new ArgumentException(
                "ExpectedUpdatedAtUtc is required when creating a Prompt Gallery version.",
                nameof(expectedUpdatedAtUtc));
        }

        ArgumentNullException.ThrowIfNull(creationReason);
        ArgumentNullException.ThrowIfNull(outputFormat);
        PromptArtifactId = promptArtifactId;
        ExpectedUpdatedAtUtc = expectedUpdatedAtUtc;
        CreationReason = creationReason;
        OutputFormat = outputFormat;
    }

    public Guid PromptArtifactId { get; }

    public DateTimeOffset ExpectedUpdatedAtUtc { get; }

    public string CreationReason { get; }

    public string OutputFormat { get; }
}

public sealed record PromptsCuratorCatalogSearchItem(
    Guid PromptArtifactId,
    string Title,
    string Summary,
    PromptGalleryItemKind Kind,
    string Phase,
    PromptArtifactStatus Status,
    bool IsArchived,
    string? CollectionName,
    IReadOnlyList<string> Tags,
    int CurrentVersionNumber,
    DateTimeOffset UpdatedAtUtc,
    bool IsFavorite);

public sealed record PromptsCuratorCatalogSearchResult(
    IReadOnlyList<PromptsCuratorCatalogSearchItem> Items,
    int PageIndex,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record PromptsCuratorItemEditorResult(
    Guid PromptArtifactId,
    Guid? ProjectId,
    Guid? CollectionId,
    string Title,
    string Summary,
    PromptGalleryItemKind Kind,
    string Phase,
    PromptArtifactStatus Status,
    bool IsArchived,
    string DraftContent,
    int CurrentVersionNumber,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> TemplateTokens,
    IReadOnlyList<PromptProviderModel> SupportedModels,
    IReadOnlyList<PromptGalleryConsumer> SupportedConsumers,
    PromptModelRecommendations Recommendations,
    PromptGallerySourceInfo Source,
    IReadOnlyList<PromptGalleryVersionInfo> Versions,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    bool IsFavorite);

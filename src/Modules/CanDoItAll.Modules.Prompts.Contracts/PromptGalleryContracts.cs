using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Prompts;

public static class PromptGalleryLimits
{
    public const int MaximumContentLength = 64_000;
}

public sealed record PromptGalleryQuery(
    string? Text = null,
    IReadOnlyList<string>? Tags = null,
    PromptGalleryItemKind? Kind = null,
    PromptArtifactStatus? Status = null,
    bool IncludeArchived = false,
    string? Provider = null,
    string? Model = null,
    int PageIndex = 0,
    int PageSize = 25,
    PromptGalleryConsumer? Consumer = null,
    bool FavoritesOnly = false)
{
    public const int MaximumPageSize = 100;

    public void Validate()
    {
        if (PageIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(PageIndex), PageIndex, "Page index cannot be negative.");
        }

        if (PageSize is < 1 or > MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(PageSize),
                PageSize,
                $"Page size must be between 1 and {MaximumPageSize}.");
        }

        if (Text?.Length > 500)
        {
            throw new ArgumentException("Search text cannot exceed 500 characters.", nameof(Text));
        }

        if (Tags is { Count: > 20 })
        {
            throw new ArgumentException("A Gallery query cannot contain more than 20 tag filters.", nameof(Tags));
        }

        if (Tags?.Any(tag => string.IsNullOrWhiteSpace(tag) || tag.Length > 120) == true)
        {
            throw new ArgumentException("Tag filters must contain 1 to 120 non-whitespace characters.", nameof(Tags));
        }

        if (Kind.HasValue && !Enum.IsDefined(Kind.Value))
        {
            throw new ArgumentException("Gallery item kind filter is invalid.", nameof(Kind));
        }

        if (Status.HasValue && !Enum.IsDefined(Status.Value))
        {
            throw new ArgumentException("Gallery status filter is invalid.", nameof(Status));
        }

        if (Provider?.Length > 120)
        {
            throw new ArgumentException("Provider filter cannot exceed 120 characters.", nameof(Provider));
        }

        if (Model?.Length > 200)
        {
            throw new ArgumentException("Model filter cannot exceed 200 characters.", nameof(Model));
        }

        if (Consumer.HasValue && !Enum.IsDefined(Consumer.Value))
        {
            throw new ArgumentException("Gallery consumer filter is invalid.", nameof(Consumer));
        }

        _ = checked(PageIndex * PageSize);
    }
}

/// <summary>
/// One page of Prompt Gallery search results.
/// </summary>
/// <typeparam name="T">Type of the items on the page.</typeparam>
/// <param name="Items">
/// Items on this page: favorites first, then by last update (newest first), title and identifier.
/// </param>
/// <param name="PageIndex">Zero-based index of this page, as requested.</param>
/// <param name="PageSize">Maximum number of items per page, as requested.</param>
/// <param name="TotalCount">Number of items matching the filters across all pages.</param>
public sealed record PromptGalleryPage<T>(
    IReadOnlyList<T> Items,
    int PageIndex,
    int PageSize,
    int TotalCount)
{
    /// <summary>
    /// Number of pages at this page size; 0 when nothing matches.
    /// </summary>
    public int TotalPages => TotalCount == 0
        ? 0
        : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>
/// A provider and model combination that a Prompt Gallery item declares as supported. An item without any entry has no
/// model restriction.
/// </summary>
/// <param name="Provider">
/// Provider name, for example <c>OpenAi</c>; 1 to 120 characters, trimmed. Compared case-insensitively.
/// </param>
/// <param name="Model">Model name of that provider; 1 to 200 characters, trimmed. Compared case-insensitively.</param>
/// <param name="IsPreferred">
/// True for the preferred combination; at most one entry of an item can be preferred. Omitted means false.
/// </param>
public sealed record PromptProviderModel(
    string Provider,
    string Model,
    bool IsPreferred = false);

/// <summary>
/// Recommended model parameters for running a Prompt Gallery item. Every value is optional; null or omitted means no
/// recommendation.
/// </summary>
/// <param name="Temperature">Recommended sampling temperature, from 0 through 2.</param>
/// <param name="MaxOutputTokens">Recommended maximum number of output tokens, from 1 through 1,000,000.</param>
/// <param name="TopP">Recommended nucleus sampling (top-p) value, from 0 through 1.</param>
public sealed record PromptModelRecommendations(
    double? Temperature = null,
    int? MaxOutputTokens = null,
    double? TopP = null);

/// <summary>
/// One Prompt Gallery item in search results, with a preview of its current draft.
/// </summary>
/// <param name="Id">
/// Identifier of the item (its prompt artifact identifier); use it with
/// <c>GET /api/prompt-gallery/items/{promptId}</c>.
/// </param>
/// <param name="Title">Title of the item.</param>
/// <param name="Summary">Summary of the item; may be empty.</param>
/// <param name="ContentPreview">The first 280 characters of the current draft, not of a version.</param>
/// <param name="Kind">Kind of item, as a JSON integer: 0 FullPrompt, 1 Part.</param>
/// <param name="Phase">Free-text phase label of the item; may be empty.</param>
/// <param name="Status">Status of the item, as a JSON integer: 0 Draft, 1 Final.</param>
/// <param name="IsArchived">True when the item is archived.</param>
/// <param name="CollectionName">
/// Name of the collection the item is filed under, when a collection with the item's collection identifier exists;
/// otherwise null.
/// </param>
/// <param name="Tags">Tag names of the item, alphabetical.</param>
/// <param name="SupportedModels">
/// Declared supported models, preferred first; an empty array means no restriction.
/// </param>
/// <param name="Recommendations">Recommended model parameters of the current draft.</param>
/// <param name="CurrentVersionNumber">Number of the latest version; 0 when the item has no version.</param>
/// <param name="UpdatedAtUtc">When the item last changed, in UTC.</param>
/// <param name="IsFavorite">True when the item is marked as a favorite.</param>
public sealed record PromptGallerySearchItem(
    Guid Id,
    string Title,
    string Summary,
    string ContentPreview,
    PromptGalleryItemKind Kind,
    string Phase,
    PromptArtifactStatus Status,
    bool IsArchived,
    string? CollectionName,
    IReadOnlyList<string> Tags,
    IReadOnlyList<PromptProviderModel> SupportedModels,
    PromptModelRecommendations Recommendations,
    int CurrentVersionNumber,
    DateTimeOffset UpdatedAtUtc,
    bool IsFavorite = false);

/// <summary>
/// One immutable version of a Prompt Gallery item as listed in the item read, without its content.
/// </summary>
/// <param name="Id">
/// Identifier of the version; use it with <c>GET /api/prompt-gallery/items/{promptId}/versions/{versionId}</c> to read
/// its content.
/// </param>
/// <param name="VersionNumber">Sequential number of the version within the item, starting at 1.</param>
/// <param name="CreationReason">Reason given when the version was created.</param>
/// <param name="OutputFormat">Output format recorded with the version, for example <c>Markdown</c>.</param>
/// <param name="CreatedAtUtc">When the version was created, in UTC.</param>
public sealed record PromptGalleryVersionInfo(
    Guid Id,
    int VersionNumber,
    string CreationReason,
    string OutputFormat,
    DateTimeOffset CreatedAtUtc);

/// <summary>
/// Where a Prompt Gallery item came from. The catalog fields are filled for imported items and null for items created
/// through the Gallery or this API.
/// </summary>
/// <param name="Provenance">
/// Origin of the item, as a JSON integer: 0 User, 1 PackagedComponentCatalog, 2 LegacyFactoryMigration,
/// 3 WorkflowMigration, 4 ExternalImport, 5 WorkflowCreated.
/// </param>
/// <param name="Catalog">Name of the source catalog of an imported item; null when not imported.</param>
/// <param name="Key">Stable key of the item in its source catalog; null when not imported.</param>
/// <param name="GroupKey">Key of the group the item belongs to in its source catalog; null when not provided.</param>
/// <param name="GroupName">Name of that group; null when not provided.</param>
/// <param name="ItemKind">Kind of item as named by the source catalog; null when not provided.</param>
/// <param name="OrderIndex">Position of the item in its source group; null when not provided.</param>
public sealed record PromptGallerySourceInfo(
    PromptArtifactProvenance Provenance,
    string? Catalog,
    string? Key,
    string? GroupKey,
    string? GroupName,
    string? ItemKind,
    int? OrderIndex);

/// <summary>
/// A saved suppression of one compatibility warning of an item for one consumer, created with
/// <c>POST /api/prompt-gallery/warning-suppressions</c>.
/// </summary>
/// <param name="Consumer">
/// Consumer the suppression applies to, as a JSON integer: 0 Workflow, 1 AgentRuntime, 2 Chat, 3 ProjectWorkbench.
/// </param>
/// <param name="IssueCode">
/// Suppressed warning, as a JSON integer: 3 ItemKindMismatch or 4 ProviderModelNotSupported.
/// </param>
public sealed record PromptWarningSuppression(
    PromptGalleryConsumer Consumer,
    PromptCompatibilityIssueCode IssueCode);

/// <summary>
/// A Prompt Gallery item with its current draft, metadata and list of versions, returned by
/// <c>GET /api/prompt-gallery/items/{promptId}</c>.
/// </summary>
/// <param name="Id">Identifier of the item (its prompt artifact identifier).</param>
/// <param name="ProjectId">
/// Identifier of the project the item is associated with; null when none. Not validated.
/// </param>
/// <param name="CollectionId">
/// Identifier of the collection the item is filed under; null when none. Not validated.
/// </param>
/// <param name="Title">Title of the item.</param>
/// <param name="Summary">Summary of the item; may be empty.</param>
/// <param name="Kind">Kind of item, as a JSON integer: 0 FullPrompt, 1 Part.</param>
/// <param name="Phase">Free-text phase label of the item; may be empty.</param>
/// <param name="Status">Status of the item, as a JSON integer: 0 Draft, 1 Final.</param>
/// <param name="IsArchived">True when the item is archived.</param>
/// <param name="DraftContent">
/// The current editable draft. It can differ from every version; read a version for immutable content.
/// </param>
/// <param name="CurrentVersionNumber">Number of the latest version; 0 when the item has no version.</param>
/// <param name="Tags">Tag names of the item, alphabetical.</param>
/// <param name="TemplateTokens">
/// Names of the template placeholders recorded when the item was imported from the packaged catalog, alphabetical.
/// Draft saves do not update them, so items created through this API have none.
/// </param>
/// <param name="SupportedModels">
/// Declared supported models, preferred first; an empty array means no restriction.
/// </param>
/// <param name="SupportedConsumers">
/// Declared supported consumers, each as a JSON integer: 0 Workflow, 1 AgentRuntime, 2 Chat, 3 ProjectWorkbench. An
/// empty array means every consumer.
/// </param>
/// <param name="WarningSuppressions">Saved warning suppressions of the item, by consumer and issue code.</param>
/// <param name="Recommendations">Recommended model parameters of the current draft.</param>
/// <param name="Source">Where the item came from.</param>
/// <param name="Versions">The item's immutable versions, newest first, without content.</param>
/// <param name="CreatedAtUtc">When the item was created, in UTC.</param>
/// <param name="UpdatedAtUtc">
/// When the item last changed, in UTC. Send it as <c>expectedUpdatedAtUtc</c> of the next draft save or version.
/// </param>
/// <param name="IsFavorite">True when the item is marked as a favorite.</param>
public sealed record PromptGalleryItemDetails(
    Guid Id,
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
    IReadOnlyList<PromptWarningSuppression> WarningSuppressions,
    PromptModelRecommendations Recommendations,
    PromptGallerySourceInfo Source,
    IReadOnlyList<PromptGalleryVersionInfo> Versions,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    bool IsFavorite = false);

public sealed record PromptGalleryCompatibilitySnapshot(
    Guid PromptArtifactId,
    PromptGalleryItemKind Kind,
    bool IsArchived,
    int CurrentVersionNumber,
    IReadOnlyList<PromptProviderModel> SupportedModels,
    IReadOnlyList<PromptGalleryConsumer> SupportedConsumers);

/// <summary>
/// Request of <c>POST /api/prompt-gallery/items</c>: the draft content and metadata of a new item, or of an existing
/// item whose draft is overwritten. Send <c>id</c>, <c>projectId</c> and <c>collectionId</c> explicitly (null when not
/// used).
/// </summary>
/// <param name="Id">
/// Identifier of the item to update; null to create an item. An identifier that no item has is rejected with 404.
/// </param>
/// <param name="ProjectId">
/// Identifier of a project to associate the item with; null for none. Stored as sent without a check.
/// </param>
/// <param name="CollectionId">
/// Identifier of a collection to file the item under; null for none. Stored as sent without a check.
/// </param>
/// <param name="Title">Title, 1 to 200 characters after trimming.</param>
/// <param name="Summary">Summary, at most 10,000 characters; required but can be empty. Trimmed.</param>
/// <param name="Kind">Kind of item, as a JSON integer: 0 FullPrompt, 1 Part.</param>
/// <param name="Phase">Free-text phase label, at most 80 characters after trimming; required but can be empty.</param>
/// <param name="Content">
/// Draft content, at most 64,000 characters; required but can be empty (an empty draft cannot get a version). Stored as
/// sent, without trimming.
/// </param>
/// <param name="Tags">
/// Tag names: at most 50, each 1 to 120 characters; trimmed and merged ignoring case. The list replaces the item's
/// tags; null or omitted clears them.
/// </param>
/// <param name="SupportedModels">
/// Supported provider and model combinations: at most 100, unique ignoring case, at most one preferred. The list
/// replaces the declared models; null or omitted clears them, which removes the model restriction.
/// </param>
/// <param name="SupportedConsumers">
/// Consumers that can use the item, each as a JSON integer: 0 Workflow, 1 AgentRuntime, 2 Chat, 3 ProjectWorkbench. The
/// list replaces the declared consumers; null, omitted or empty means every consumer.
/// </param>
/// <param name="Recommendations">
/// Recommended model parameters; null or omitted clears all of them.
/// </param>
/// <param name="ExpectedUpdatedAtUtc">
/// Required when <c>id</c> is set: the item's <c>updatedAtUtc</c> from the last read or save, compared exactly. A
/// different value is rejected with <c>prompts.gallery.concurrency-conflict</c>. Ignored when creating.
/// </param>
public sealed record PromptGalleryDraft(
    Guid? Id,
    Guid? ProjectId,
    Guid? CollectionId,
    string Title,
    string Summary,
    PromptGalleryItemKind Kind,
    string Phase,
    string Content,
    IReadOnlyList<string>? Tags = null,
    IReadOnlyList<PromptProviderModel>? SupportedModels = null,
    IReadOnlyList<PromptGalleryConsumer>? SupportedConsumers = null,
    PromptModelRecommendations? Recommendations = null,
    DateTimeOffset? ExpectedUpdatedAtUtc = null);

/// <summary>
/// Result of a Prompt Gallery draft save.
/// </summary>
/// <param name="PromptArtifactId">Identifier of the saved item; for a new item, its new identifier.</param>
/// <param name="UpdatedAtUtc">
/// The item's new <c>updatedAtUtc</c>; send it as <c>expectedUpdatedAtUtc</c> of the next draft save or version.
/// </param>
public readonly record struct PromptDraftSaveReceipt(
    Guid PromptArtifactId,
    DateTimeOffset UpdatedAtUtc);

/// <summary>
/// Request of <c>POST /api/prompt-gallery/items/{promptId}/versions</c>: create the next immutable version from the
/// item's current draft.
/// </summary>
/// <param name="CreationReason">Why the version is created, 1 to 200 characters after trimming.</param>
/// <param name="ExpectedUpdatedAtUtc">
/// The item's <c>updatedAtUtc</c> from the read or draft save you reviewed; required and compared exactly.
/// </param>
/// <param name="OutputFormat">
/// Format that the prompt content is written in, for example <c>Markdown</c>; 1 to 80 characters, trimmed. Omitted
/// means <c>Markdown</c>.
/// </param>
public sealed record PromptVersionCreateRequest(
    string CreationReason,
    DateTimeOffset ExpectedUpdatedAtUtc,
    string OutputFormat = "Markdown");

public sealed record PromptImportVersionRequest(
    string CreationReason,
    string OutputFormat = "Markdown");

public sealed record PromptGalleryImportRequest(
    PromptArtifactProvenance Provenance,
    string SourceKey,
    string SourceCatalog,
    PromptGalleryDraft Draft,
    PromptImportVersionRequest Version);

/// <summary>
/// An immutable version of a Prompt Gallery item with its content, as created or read.
/// </summary>
/// <param name="PromptArtifactId">Identifier of the item the version belongs to.</param>
/// <param name="PromptVersionId">Identifier of the version.</param>
/// <param name="VersionNumber">Sequential number of the version within the item, starting at 1.</param>
/// <param name="Title">Title of the item when the version was created.</param>
/// <param name="Summary">Summary of the item when the version was created.</param>
/// <param name="Kind">
/// Kind of the item when the version was created, as a JSON integer: 0 FullPrompt, 1 Part.
/// </param>
/// <param name="Content">The immutable content of the version.</param>
/// <param name="OutputFormat">Output format recorded with the version, for example <c>Markdown</c>.</param>
/// <param name="Recommendations">Model recommendations captured with the version.</param>
/// <param name="CreatedAtUtc">When the version was created, in UTC.</param>
public sealed record PromptVersionSnapshot(
    Guid PromptArtifactId,
    Guid PromptVersionId,
    int VersionNumber,
    string Title,
    string Summary,
    PromptGalleryItemKind Kind,
    string Content,
    string OutputFormat,
    PromptModelRecommendations Recommendations,
    DateTimeOffset CreatedAtUtc);

public interface IPromptGallerySearchDriver
{
    Task<PromptGalleryPage<PromptGallerySearchItem>> SearchAsync(
        PromptGalleryQuery query,
        CancellationToken cancellationToken = default);
}

public interface IPromptGalleryService
{
    Task<PromptGalleryPage<PromptGallerySearchItem>> SearchAsync(
        PromptGalleryQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<PromptGalleryItemDetails>> GetItemAsync(
        Guid promptArtifactId,
        CancellationToken cancellationToken = default);

    Task<Result<PromptDraftSaveReceipt>> SaveDraftAsync(
        PromptGalleryDraft draft,
        CancellationToken cancellationToken = default);

    Task<Result<PromptVersionSnapshot>> CreateVersionAsync(
        Guid promptArtifactId,
        PromptVersionCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PromptVersionSnapshot>> GetVersionSnapshotAsync(
        Guid promptVersionId,
        CancellationToken cancellationToken = default);

    Task<Result<PromptVersionSnapshot>> GetVersionSnapshotAsync(
        Guid promptArtifactId,
        int versionNumber,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<PromptVersionSnapshot>>> GetVersionSnapshotsAsync(
        IReadOnlyCollection<Guid> promptVersionIds,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyDictionary<Guid, PromptGalleryCompatibilitySnapshot>>> GetCompatibilitySnapshotsAsync(
        IReadOnlyCollection<Guid> promptArtifactIds,
        CancellationToken cancellationToken = default);

    Task<Result> ArchiveAsync(
        Guid promptArtifactId,
        bool archived,
        CancellationToken cancellationToken = default);

    Task<Result> SetFavoriteAsync(
        Guid promptArtifactId,
        bool favorite,
        CancellationToken cancellationToken = default);

    Task<Result<PromptCompatibilityResult>> EvaluateCompatibilityAsync(
        Guid promptArtifactId,
        PromptGalleryConsumerContext context,
        CancellationToken cancellationToken = default);

    Task<Result> SetWarningSuppressionAsync(
        Guid promptArtifactId,
        PromptGalleryConsumer consumer,
        PromptCompatibilityIssueCode issueCode,
        bool suppressed,
        CancellationToken cancellationToken = default);
}

public interface IPromptGalleryImportService
{
    Task<Result<PromptVersionSnapshot>> ImportVersionAsync(
        PromptGalleryImportRequest request,
        CancellationToken cancellationToken = default);
}

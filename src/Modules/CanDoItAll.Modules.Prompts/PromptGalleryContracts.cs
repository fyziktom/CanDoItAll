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
    PromptGalleryConsumer? Consumer = null)
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

public sealed record PromptGalleryPage<T>(
    IReadOnlyList<T> Items,
    int PageIndex,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => TotalCount == 0
        ? 0
        : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed record PromptProviderModel(string Provider, string Model);

public sealed record PromptModelRecommendations(
    double? Temperature = null,
    int? MaxOutputTokens = null,
    double? TopP = null);

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
    DateTimeOffset UpdatedAtUtc);

public sealed record PromptGalleryVersionInfo(
    Guid Id,
    int VersionNumber,
    string CreationReason,
    string OutputFormat,
    DateTimeOffset CreatedAtUtc);

public sealed record PromptGallerySourceInfo(
    PromptArtifactProvenance Provenance,
    string? Catalog,
    string? Key,
    string? GroupKey,
    string? GroupName,
    string? ItemKind,
    int? OrderIndex);

public sealed record PromptWarningSuppression(
    PromptGalleryConsumer Consumer,
    PromptCompatibilityIssueCode IssueCode);

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
    DateTimeOffset UpdatedAtUtc);

public sealed record PromptGalleryCompatibilitySnapshot(
    Guid PromptArtifactId,
    PromptGalleryItemKind Kind,
    bool IsArchived,
    int CurrentVersionNumber,
    IReadOnlyList<PromptProviderModel> SupportedModels,
    IReadOnlyList<PromptGalleryConsumer> SupportedConsumers);

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
    PromptModelRecommendations? Recommendations = null);

public sealed record PromptVersionCreateRequest(
    string CreationReason,
    string OutputFormat = "Markdown");

public sealed record PromptGalleryImportRequest(
    PromptArtifactProvenance Provenance,
    string SourceKey,
    string SourceCatalog,
    PromptGalleryDraft Draft,
    PromptVersionCreateRequest Version);

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

    Task<Result<Guid>> SaveDraftAsync(
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

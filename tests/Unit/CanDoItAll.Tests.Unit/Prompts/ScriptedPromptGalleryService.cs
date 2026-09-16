using CanDoItAll.Modules.Prompts;
using CanDoItAll.Prompts.UI.Editor;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit.Prompts;

// Scripted IPromptGalleryService: every operation is a replaceable delegate and every call is recorded.
internal sealed class ScriptedPromptGalleryService : IPromptGalleryService
{
    public List<PromptGalleryQuery> Queries { get; } = [];

    public List<CancellationToken> SearchTokens { get; } = [];

    public List<Guid> GetItemCalls { get; } = [];

    public List<PromptGalleryDraft> SavedDrafts { get; } = [];

    public List<(Guid ItemId, PromptVersionCreateRequest Request)> VersionRequests { get; } = [];

    public List<(Guid ItemId, bool Favorite)> FavoriteWrites { get; } = [];

    public List<(Guid ItemId, bool Archived)> ArchiveWrites { get; } = [];

    public List<(Guid ItemId, PromptGalleryConsumer Consumer, PromptCompatibilityIssueCode Code, bool Suppressed)> SuppressionWrites { get; } = [];

    public Func<PromptGalleryQuery, CancellationToken, Task<PromptGalleryPage<PromptGallerySearchItem>>> Search { get; set; }
        = (query, _) => Task.FromResult(Page(query, [Item()]));

    public Func<Guid, bool, Task<Result>> SetFavorite { get; set; }
        = (_, _) => Task.FromResult(Result.Success());

    public Func<Guid, Task<Result<PromptGalleryItemDetails>>> GetItem { get; set; }
        = id => Task.FromResult(Result<PromptGalleryItemDetails>.Success(Details(id)));

    public Func<PromptGalleryDraft, Task<Result<PromptDraftSaveReceipt>>> SaveDraft { get; set; }
        = draft => Task.FromResult(Result<PromptDraftSaveReceipt>.Success(
            new PromptDraftSaveReceipt(draft.Id ?? Guid.NewGuid(), DateTimeOffset.UnixEpoch.AddMinutes(1))));

    public Func<Guid, PromptVersionCreateRequest, Task<Result<PromptVersionSnapshot>>> CreateVersion { get; set; }
        = (id, request) => Task.FromResult(Result<PromptVersionSnapshot>.Success(Snapshot(id, 1)));

    public Func<Guid, bool, Task<Result>> Archive { get; set; }
        = (_, _) => Task.FromResult(Result.Success());

    public Func<Guid, PromptGalleryConsumer, PromptCompatibilityIssueCode, bool, Task<Result>> SetWarningSuppression { get; set; }
        = (_, _, _, _) => Task.FromResult(Result.Success());

    public Func<Guid, int, Task<Result<PromptVersionSnapshot>>> GetVersionSnapshotByNumber { get; set; }
        = (id, number) => Task.FromResult(Result<PromptVersionSnapshot>.Success(Snapshot(id, number)));

    public Func<Guid, PromptGalleryConsumerContext, Task<Result<PromptCompatibilityResult>>> EvaluateCompatibility { get; set; }
        = (_, _) => Task.FromResult(Result<PromptCompatibilityResult>.Success(new PromptCompatibilityResult([])));

    public Task<PromptGalleryPage<PromptGallerySearchItem>> SearchAsync(
        PromptGalleryQuery query,
        CancellationToken cancellationToken = default)
    {
        Queries.Add(query);
        SearchTokens.Add(cancellationToken);
        return Search(query, cancellationToken);
    }

    public Task<Result<PromptGalleryItemDetails>> GetItemAsync(
        Guid promptArtifactId,
        CancellationToken cancellationToken = default)
    {
        GetItemCalls.Add(promptArtifactId);
        return GetItem(promptArtifactId);
    }

    public Task<Result<PromptDraftSaveReceipt>> SaveDraftAsync(
        PromptGalleryDraft draft,
        CancellationToken cancellationToken = default)
    {
        SavedDrafts.Add(draft);
        return SaveDraft(draft);
    }

    public Task<Result<PromptVersionSnapshot>> CreateVersionAsync(
        Guid promptArtifactId,
        PromptVersionCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        VersionRequests.Add((promptArtifactId, request));
        return CreateVersion(promptArtifactId, request);
    }

    public Task<Result<PromptVersionSnapshot>> GetVersionSnapshotAsync(
        Guid promptVersionId,
        CancellationToken cancellationToken = default)
        => throw Unused();

    public Task<Result<PromptVersionSnapshot>> GetVersionSnapshotAsync(
        Guid promptArtifactId,
        int versionNumber,
        CancellationToken cancellationToken = default)
        => GetVersionSnapshotByNumber(promptArtifactId, versionNumber);

    public Task<Result<IReadOnlyList<PromptVersionSnapshot>>> GetVersionSnapshotsAsync(
        IReadOnlyCollection<Guid> promptVersionIds,
        CancellationToken cancellationToken = default)
        => throw Unused();

    public Task<Result<IReadOnlyDictionary<Guid, PromptGalleryCompatibilitySnapshot>>> GetCompatibilitySnapshotsAsync(
        IReadOnlyCollection<Guid> promptArtifactIds,
        CancellationToken cancellationToken = default)
        => throw Unused();

    public Task<Result> ArchiveAsync(
        Guid promptArtifactId,
        bool archived,
        CancellationToken cancellationToken = default)
    {
        ArchiveWrites.Add((promptArtifactId, archived));
        return Archive(promptArtifactId, archived);
    }

    public Task<Result> SetFavoriteAsync(
        Guid promptArtifactId,
        bool favorite,
        CancellationToken cancellationToken = default)
    {
        FavoriteWrites.Add((promptArtifactId, favorite));
        return SetFavorite(promptArtifactId, favorite);
    }

    public Task<Result<PromptCompatibilityResult>> EvaluateCompatibilityAsync(
        Guid promptArtifactId,
        PromptGalleryConsumerContext context,
        CancellationToken cancellationToken = default)
        => EvaluateCompatibility(promptArtifactId, context);

    public Task<Result> SetWarningSuppressionAsync(
        Guid promptArtifactId,
        PromptGalleryConsumer consumer,
        PromptCompatibilityIssueCode issueCode,
        bool suppressed,
        CancellationToken cancellationToken = default)
    {
        SuppressionWrites.Add((promptArtifactId, consumer, issueCode, suppressed));
        return SetWarningSuppression(promptArtifactId, consumer, issueCode, suppressed);
    }

    public static PromptGallerySearchItem Item(Guid? id = null, string title = "Reusable prompt", bool favorite = false)
        => new(
            id ?? Guid.NewGuid(),
            title,
            "Summary",
            "Preview content",
            PromptGalleryItemKind.FullPrompt,
            "design",
            PromptArtifactStatus.Draft,
            IsArchived: false,
            CollectionName: null,
            Tags: ["architecture"],
            SupportedModels: [],
            Recommendations: new PromptModelRecommendations(),
            CurrentVersionNumber: 0,
            UpdatedAtUtc: DateTimeOffset.UnixEpoch,
            IsFavorite: favorite);

    public static PromptGalleryPage<PromptGallerySearchItem> Page(
        PromptGalleryQuery query,
        IReadOnlyList<PromptGallerySearchItem> items,
        int? totalCount = null)
        => new(items, query.PageIndex, query.PageSize, totalCount ?? items.Count);

    // Persisted details; when a submission is supplied the editable content mirrors that submission the way the
    // persistence owner normalizes it (trimmed scalars), so a read-back of an own write can be scripted exactly.
    public static PromptGalleryItemDetails Details(
        Guid id,
        DateTimeOffset? updatedAtUtc = null,
        int currentVersionNumber = 0,
        bool isArchived = false,
        IReadOnlyList<PromptWarningSuppression>? suppressions = null,
        PromptGalleryEditorSubmission? content = null)
    {
        var updated = updatedAtUtc ?? DateTimeOffset.UnixEpoch;
        var versions = Enumerable.Range(1, currentVersionNumber)
            .Select(number => new PromptGalleryVersionInfo(Guid.NewGuid(), number, "Ready for reuse", "Markdown", updated))
            .ToArray();
        return new PromptGalleryItemDetails(
            id,
            ProjectId: null,
            CollectionId: null,
            content?.Title.Trim() ?? "Loaded prompt",
            content?.Summary.Trim() ?? "Loaded summary",
            content?.Kind ?? PromptGalleryItemKind.FullPrompt,
            content?.Phase.Trim() ?? "workflow",
            currentVersionNumber > 0 ? PromptArtifactStatus.Final : PromptArtifactStatus.Draft,
            isArchived,
            content?.Content ?? "Loaded content",
            currentVersionNumber,
            Tags: content?.Tags ?? ["workflow"],
            TemplateTokens: [],
            SupportedModels: content?.SupportedModels ?? [new PromptProviderModel("OpenAI", "gpt-5.4-mini", IsPreferred: true)],
            SupportedConsumers: content?.SupportedConsumers ?? [PromptGalleryConsumer.Workflow],
            WarningSuppressions: suppressions ?? [],
            content?.Recommendations ?? new PromptModelRecommendations(0.2, 800, 0.9),
            new PromptGallerySourceInfo(PromptArtifactProvenance.User, null, null, null, null, null, null),
            versions,
            updated,
            updated);
    }

    public static PromptVersionSnapshot Snapshot(Guid artifactId, int number)
        => new(
            artifactId,
            Guid.NewGuid(),
            number,
            "Final title",
            "Final summary",
            PromptGalleryItemKind.FullPrompt,
            "Final content",
            "Markdown",
            new PromptModelRecommendations(0.1, 400, 0.8),
            DateTimeOffset.UnixEpoch);

    private static NotSupportedException Unused()
        => new("This gallery member is not scripted for the current test.");
}

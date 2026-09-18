using CanDoItAll.Modules.Prompts;

namespace CanDoItAll.Prompts.UI.Gallery;

public sealed record PromptGalleryFilterValues
{
    public string Text { get; init; } = string.Empty;

    public IReadOnlyList<string> Tags { get; init; } = [];

    public PromptGalleryItemKind? Kind { get; init; }

    public PromptArtifactStatus? Status { get; init; }

    public string ProviderFilter { get; init; } = string.Empty;

    public string ModelFilter { get; init; } = string.Empty;

    public bool FavoritesOnly { get; init; }

    public bool IncludeArchived { get; init; }

    // The pinned chat provider/model filter starts enabled; "Clear filters" switches it off.
    public bool ActualChatModelOnly { get; init; } = true;

    public static PromptGalleryFilterValues Initial { get; } = new();

    public static PromptGalleryFilterValues Cleared { get; } = new() { ActualChatModelOnly = false };
}

public sealed record PromptGallerySearchPresentation(
    PromptGalleryFilterValues Filters,
    IReadOnlyList<PromptGallerySearchItem> Items,
    int PageIndex,
    int TotalPages,
    int TotalCount,
    bool IsLoading,
    string? LoadError,
    IReadOnlyList<string> AvailableTags,
    string? ContextProvider,
    string? ContextModel,
    bool ShowActualChatModelFilter,
    Guid? FavoriteBusyItemId)
{
    public static PromptGallerySearchPresentation Initial { get; } = new(
        PromptGalleryFilterValues.Initial,
        [],
        PageIndex: 0,
        TotalPages: 0,
        TotalCount: 0,
        IsLoading: true,
        LoadError: null,
        AvailableTags: [],
        ContextProvider: null,
        ContextModel: null,
        ShowActualChatModelFilter: false,
        FavoriteBusyItemId: null);

    public bool HasContextModel => !string.IsNullOrWhiteSpace(ContextModel);

    public bool HasContextProvider => !string.IsNullOrWhiteSpace(ContextProvider);

    public bool OffersActualChatModelFilter => ShowActualChatModelFilter && HasContextModel;

    // The pinned context provider/model applies unless the optional checkbox is offered and switched off.
    public bool UsesContextProviderModel => !OffersActualChatModelFilter || Filters.ActualChatModelOnly;

    public bool ShowsContextBadges
        => !OffersActualChatModelFilter && UsesContextProviderModel && (HasContextProvider || HasContextModel);
}

public abstract record PromptGallerySearchIntent
{
    private PromptGallerySearchIntent()
    {
    }

    public sealed record ChangeFilters(PromptGalleryFilterValues Filters, bool Debounce) : PromptGallerySearchIntent;

    public sealed record ClearFilters : PromptGallerySearchIntent;

    public sealed record LoadPage(int PageIndex) : PromptGallerySearchIntent;

    public sealed record Retry : PromptGallerySearchIntent;

    public sealed record ToggleFavorite(PromptGallerySearchItem Item) : PromptGallerySearchIntent;

    public sealed record Select(PromptGallerySearchItem Item) : PromptGallerySearchIntent;

    public sealed record Edit(Guid ItemId) : PromptGallerySearchIntent;
}

using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.Modules.Prompts.Components;
using CanDoItAll.Prompts.UI.Gallery;

namespace CanDoItAll.Modules.Prompts.Pages;

// Tags hold a list, so compare states with the codec (SameState), not with record equality.
public sealed record PromptGalleryUrlState(
    Guid? PromptId = null,
    string? Search = null,
    IReadOnlyList<string>? Tags = null,
    PromptGalleryItemKind? Kind = null,
    PromptArtifactStatus? Status = null,
    string? Provider = null,
    string? Model = null,
    bool FavoritesOnly = false,
    bool IncludeArchived = false,
    bool ChatModelOnly = true,
    int Page = 1) {
    public PromptGallerySearchRequest ToSearchRequest() => new(new PromptGalleryFilterValues {
        Text = Search ?? string.Empty,
        Tags = Tags ?? [],
        Kind = Kind,
        Status = Status,
        ProviderFilter = Provider ?? string.Empty,
        ModelFilter = Model ?? string.Empty,
        FavoritesOnly = FavoritesOnly,
        IncludeArchived = IncludeArchived,
        ActualChatModelOnly = ChatModelOnly
    }, Page - 1);

    public PromptGalleryUrlState WithSearch(PromptGalleryFilterValues filters, int pageIndex) => this with {
        Search = filters.Text,
        Tags = filters.Tags,
        Kind = filters.Kind,
        Status = filters.Status,
        Provider = filters.ProviderFilter,
        Model = filters.ModelFilter,
        FavoritesOnly = filters.FavoritesOnly,
        IncludeArchived = filters.IncludeArchived,
        ChatModelOnly = filters.ActualChatModelOnly,
        Page = pageIndex + 1
    };
}

public sealed class PromptGalleryUrlCodec : IUrlStateCodec<PromptGalleryUrlState> {
    public const string PromptKey = "promptId";
    public const string SearchKey = "q";
    public const string TagsKey = "tags";
    public const string KindKey = "kind";
    public const string StatusKey = "status";
    public const string ProviderKey = "provider";
    public const string ModelKey = "model";
    public const string FavoritesKey = "favorites";
    public const string ArchivedKey = "archived";
    public const string ChatModelKey = "chatModel";
    public const string PageKey = "page";
    public const int MaximumSearchLength = 500;
    public const int MaximumTagCount = 20;
    public const int MaximumTagLength = 120;
    public const int MaximumProviderLength = 120;
    public const int MaximumModelLength = 200;

    public static PromptGalleryUrlCodec Instance { get; } = new();

    public static UrlTokenMap<PromptGalleryItemKind> Kinds { get; } = new(
        (PromptGalleryItemKind.FullPrompt, "full-prompt"),
        (PromptGalleryItemKind.Part, "part"));

    public static UrlTokenMap<PromptArtifactStatus> Statuses { get; } = new(
        (PromptArtifactStatus.Draft, "draft"),
        (PromptArtifactStatus.Final, "final"));

    public IReadOnlyList<string> Keys { get; } = [
        PromptKey, SearchKey, TagsKey, KindKey, StatusKey, ProviderKey, ModelKey, FavoritesKey, ArchivedKey, ChatModelKey, PageKey
    ];

    public PromptGalleryUrlState Read(UrlQuery query) => new(
        query.GetId(PromptKey),
        query.GetText(SearchKey, MaximumSearchLength),
        ReadTags(query),
        query.GetToken(KindKey, Kinds),
        query.GetToken(StatusKey, Statuses),
        query.GetText(ProviderKey, MaximumProviderLength),
        query.GetText(ModelKey, MaximumModelLength),
        query.GetFlag(FavoritesKey),
        query.GetFlag(ArchivedKey),
        query.GetFlag(ChatModelKey, defaultValue: true),
        query.GetPage(PageKey));

    public void Write(PromptGalleryUrlState state, UrlQueryWriter query) {
        query.Id(PromptKey, state.PromptId);
        query.Text(SearchKey, state.Search);
        if (state.Tags is { Count: > 0 } tags) {
            query.Text(TagsKey, string.Join(',', tags));
        }
        query.Token(KindKey, state.Kind, Kinds);
        query.Token(StatusKey, state.Status, Statuses);
        query.Text(ProviderKey, state.Provider);
        query.Text(ModelKey, state.Model);
        query.Flag(FavoritesKey, state.FavoritesOnly);
        query.Flag(ArchivedKey, state.IncludeArchived);
        query.Flag(ChatModelKey, state.ChatModelOnly, defaultValue: true);
        query.Page(PageKey, state.Page);
    }

    // Tags are comma-separated; the tag editor never produces a comma inside a tag.
    private static IReadOnlyList<string> ReadTags(UrlQuery query) {
        var tags = new List<string>();
        foreach (var raw in query.GetAll(TagsKey)) {
            foreach (var tag in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
                if (tag.Length <= MaximumTagLength && !tags.Contains(tag, StringComparer.OrdinalIgnoreCase)) {
                    tags.Add(tag);
                }
            }
        }
        return tags.Count > MaximumTagCount ? tags.GetRange(0, MaximumTagCount) : tags;
    }
}

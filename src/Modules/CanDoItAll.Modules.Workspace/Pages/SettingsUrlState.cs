using CanDoItAll.AppComponents.Navigation;

namespace CanDoItAll.Modules.Workspace.Pages;

public sealed record SettingsUrlState(
    WorkspaceSection Tab = WorkspaceSection.Workspace,
    Guid? SecretId = null,
    Guid? ProfileId = null,
    Guid? StorageId = null,
    string? Search = null,
    int Page = 1);

public readonly record struct SettingsRouteChange<T>(T Value, UrlHistoryMode Mode);

public sealed class SettingsUrlCodec : IUrlStateCodec<SettingsUrlState> {
    public const string Path = "/settings";
    public const string TabKey = "tab";
    public const string SecretKey = "secretId";
    public const string ProfileKey = "profileId";
    public const string StorageKey = "storageId";
    public const string SearchKey = "q";
    public const string PageKey = "page";
    public const int SearchMaximumLength = 128;

    public static SettingsUrlCodec Instance { get; } = new();

    public static UrlTokenMap<WorkspaceSection> Tabs { get; } = new(
        (WorkspaceSection.Workspace, "workspace"),
        (WorkspaceSection.DataSources, "data-sources"),
        (WorkspaceSection.Storage, "storage"),
        (WorkspaceSection.Files, "files"),
        (WorkspaceSection.ProviderHistory, "provider-history"),
        (WorkspaceSection.Secrets, "secrets"),
        (WorkspaceSection.Providers, "providers"),
        (WorkspaceSection.ApiAccess, "api-access"));

    public IReadOnlyList<string> Keys { get; } = [TabKey, SecretKey, ProfileKey, StorageKey, SearchKey, PageKey];

    public SettingsUrlState Read(UrlQuery query) {
        var tab = query.GetToken(TabKey, Tabs, WorkspaceSection.Workspace);
        return tab switch {
            WorkspaceSection.Secrets => new(tab, SecretId: query.GetId(SecretKey)),
            WorkspaceSection.DataSources => new(tab, ProfileId: query.GetId(ProfileKey)),
            WorkspaceSection.Storage => new(tab, StorageId: query.GetId(StorageKey)),
            WorkspaceSection.ApiAccess => new(tab, Search: query.GetText(SearchKey, SearchMaximumLength), Page: query.GetPage(PageKey)),
            _ => new(tab)
        };
    }

    public void Write(SettingsUrlState state, UrlQueryWriter query) {
        query.Token(TabKey, state.Tab, Tabs, WorkspaceSection.Workspace);
        switch (state.Tab) {
            case WorkspaceSection.Secrets:
                query.Id(SecretKey, state.SecretId);
                break;
            case WorkspaceSection.DataSources:
                query.Id(ProfileKey, state.ProfileId);
                break;
            case WorkspaceSection.Storage:
                query.Id(StorageKey, state.StorageId);
                break;
            case WorkspaceSection.ApiAccess:
                query.Text(SearchKey, state.Search);
                query.Page(PageKey, state.Page);
                break;
        }
    }
}

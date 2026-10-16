using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AppComponents.FileTools;
using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Resources.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CanDoItAll.Modules.Resources.Pages;

public partial class ResourcesPage : IAsyncDisposable {
    private const int RegistryTabIndex = 0;
    private const int BrowseTabIndex = 1;
    private static readonly TimeSpan SearchCommitDelay = TimeSpan.FromMilliseconds(300);

    [SupplyParameterFromQuery(Name = ResourcesUrlCodec.ViewKey)] public string? ViewQuery { get; set; }
    [SupplyParameterFromQuery(Name = ResourcesUrlCodec.ResourceKey)] public string? ResourceIdQuery { get; set; }
    [SupplyParameterFromQuery(Name = ResourcesUrlCodec.ProjectKey)] public string? ProjectIdQuery { get; set; }
    [SupplyParameterFromQuery(Name = ResourcesUrlCodec.SearchKey)] public string? SearchQuery { get; set; }
    [SupplyParameterFromQuery(Name = ResourcesUrlCodec.ProjectFilterKey)] public string? ProjectFilterQuery { get; set; }
    [SupplyParameterFromQuery(Name = ResourcesUrlCodec.ConnectorKey)] public string? ConnectorQuery { get; set; }
    [SupplyParameterFromQuery(Name = ResourcesUrlCodec.ValidationKey)] public string? ValidationQuery { get; set; }
    [SupplyParameterFromQuery(Name = ResourcesUrlCodec.SourceKey)] public string? SourceQuery { get; set; }
    [Inject] public IResourceRegistryOwner RegistryOwner { get; set; } = default!;
    [Inject] public IResourceBrowseOwner BrowseOwner { get; set; } = default!;
    [Inject] public NotificationService NotificationService { get; set; } = default!;
    [Inject] public NavigationManager Navigation { get; set; } = default!;
    [Inject] public IJSRuntime JSRuntime { get; set; } = default!;
    public ResourceRegistryController Registry { get; private set; } = default!;
    public ResourceBrowseController Browse { get; private set; } = default!;
    private UrlStateBinding<ResourcesUrlState> url = default!;
    private CancellationTokenSource? searchCommit;
    private bool tabPushedInTurn;
    private bool disposed;
    private int TabIndex => url.State.View == ResourcesView.Browse ? BrowseTabIndex : RegistryTabIndex;
    private AgentChatContextSurface AgentChatSurface => ResourcesAgentChatContextBuilder.Build(
        url.State.View == ResourcesView.Browse ? ResourcesAgentChatView.Browse : ResourcesAgentChatView.Registry, Registry.Draft.Editor,
        Registry.SelectedProjectName, Registry.SelectedManifest?.DisplayName ?? "Unavailable connector",
        Browse.Position is { } position ? new(new(position.SourceId.Value), position.SourceClass.ToString(), position.DisplayName, position.ProjectId, position.ProjectName) : null);
    private AgentChatContextAccessState CurrentAccess => (url.State.View == ResourcesView.Browse ? Browse.Access : Registry.Access) switch {
        ResourceViewAccess.Ready => AgentChatContextAccessState.Ready,
        ResourceViewAccess.Failed => AgentChatContextAccessState.Failed,
        _ => AgentChatContextAccessState.Loading
    };
    // The agent-chat position is matched with the shell's identity of the arrived location, so it never follows an intent in flight.
    private AgentChatNavigationIdentity AgentChatNavigationFence {
        get {
            var located = ResourcesUrlCodec.Instance.Read(Navigation.Uri);
            return AgentChatNavigationIdentity.CreateForLocation(Navigation.BaseUri, Navigation.Uri,
                [new(ResourcesUrlCodec.ResourceKey, located.ResourceId?.ToString("D")), new(ResourcesUrlCodec.ProjectKey, located.ProjectId?.ToString("D"))]);
        }
    }
    protected override void OnInitialized() {
        url = new(Navigation, ResourcesUrlCodec.Instance, null, typeof(ResourcesPage));
        Registry = new(RegistryOwner);
        Browse = new(BrowseOwner, new(JSRuntime)) { Promoted = _ => Registry.RefreshAfterPromotionAsync() };
        Registry.Changed += OnChanged;
        Browse.Changed += OnChanged;
        Registry.Completed += NotifyOutcome;
        Registry.RouteChanged += HandleRegistryRouteChanged;
        Registry.FiltersChanged += HandleFiltersChanged;
        Browse.SourceSelected += HandleSourceSelected;
    }
    protected override async Task OnParametersSetAsync() {
        var change = url.Apply(canonicalize: RendererInfo.IsInteractive);
        if (!change.IsChanged) {
            return;
        }
        var previous = change.Previous;
        var current = change.Current;
        if (change.IsInitial || !previous.HasSameFilters(current)) {
            CancelSearchCommit();
            Registry.ApplyRouteFilters(current.Search, current.ProjectFilter, current.Connector, current.Validation);
        }
        if (change.IsInitial || previous.ResourceId != current.ResourceId || previous.ProjectId != current.ProjectId) {
            await Registry.LoadRouteAsync(current.ResourceId, current.ProjectId);
        }
        if (Browse.Catalog is null) {
            await Browse.RefreshAsync();
        }
        if (change.IsInitial || previous.Source != current.Source) {
            await Browse.ApplyRouteSourceAsync(url.State.Source);
        }
    }
    private void SetTab(int selected) {
        var view = selected switch {
            RegistryTabIndex => ResourcesView.Registry,
            BrowseTabIndex => ResourcesView.Browse,
            _ => throw new ArgumentOutOfRangeException(nameof(selected), selected, "The Resources tab is undefined.")
        };
        CommitPendingSearch();
        if (url.Navigate(url.State with { View = view }, UrlHistoryMode.Push)) {
            tabPushedInTurn = true;
            _ = EndTurnAsync();
        }
    }
    // "New resource" switches to the registry and clears the selection in one user action: one history entry.
    private async Task EndTurnAsync() {
        await Task.Yield();
        tabPushedInTurn = false;
    }
    private void HandleRegistryRouteChanged(ResourceRegistryRoute route, ResourceRegistryRouteChangeKind kind) {
        if (disposed) {
            return;
        }
        var mode = kind switch {
            ResourceRegistryRouteChangeKind.Selected => UrlHistoryMode.Push,
            ResourceRegistryRouteChangeKind.Cleared => tabPushedInTurn ? UrlHistoryMode.Replace : UrlHistoryMode.Push,
            ResourceRegistryRouteChangeKind.Created => UrlHistoryMode.Replace,
            ResourceRegistryRouteChangeKind.Deleted => UrlHistoryMode.Replace,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The registry route change is undefined.")
        };
        CommitPendingSearch();
        url.Navigate(url.State with { ResourceId = route.ResourceId, ProjectId = route.ProjectId }, mode);
    }
    // Filters apply instantly; the URL follows with Replace, and typed search text only once typing pauses.
    private void HandleFiltersChanged(ResourceRegistryFilterChangeKind kind) {
        if (disposed) {
            return;
        }
        var next = url.State with {
            Search = Registry.Search,
            ProjectFilter = Registry.ProjectFilter,
            Connector = Registry.ConnectorFilter,
            Validation = Registry.ValidationFilter
        };
        if (kind == ResourceRegistryFilterChangeKind.Edited && next.ProjectFilter == url.State.ProjectFilter &&
            next.Connector == url.State.Connector && next.Validation == url.State.Validation) {
            ScheduleSearchCommit();
            return;
        }
        CancelSearchCommit();
        url.Navigate(next, kind == ResourceRegistryFilterChangeKind.Reset ? UrlHistoryMode.Push : UrlHistoryMode.Replace);
    }
    private void ScheduleSearchCommit() {
        CancelSearchCommit();
        var pending = new CancellationTokenSource();
        searchCommit = pending;
        _ = CommitSearchAsync(pending);
    }
    private async Task CommitSearchAsync(CancellationTokenSource pending) {
        try {
            await Task.Delay(SearchCommitDelay, pending.Token);
        } catch (OperationCanceledException) {
            return;
        }
        await InvokeAsync(() => {
            if (disposed || !ReferenceEquals(searchCommit, pending)) {
                return;
            }
            searchCommit = null;
            url.Navigate(url.State with { Search = Registry.Search }, UrlHistoryMode.Replace);
        });
    }
    // A pushed entry must carry the search text the operator already typed.
    private void CommitPendingSearch() {
        if (searchCommit is null) {
            return;
        }
        CancelSearchCommit();
        url.Navigate(url.State with { Search = Registry.Search }, UrlHistoryMode.Replace);
    }
    private void CancelSearchCommit() {
        var pending = searchCommit;
        searchCommit = null;
        pending?.Cancel();
    }
    private void HandleSourceSelected(ResourceFileSourceKey key) {
        if (disposed) {
            return;
        }
        CommitPendingSearch();
        url.Navigate(url.State with { Source = key }, UrlHistoryMode.Push);
    }
    private void OnChanged() {
        if (!disposed) {
            _ = InvokeAsync(StateHasChanged);
        }
    }
    private void NotifyOutcome(ResourceMutationReceipt receipt) {
        var saved = receipt.Kind == ResourceMutationKind.Save;
        var action = saved ? "saved" : "deleted";
        if (receipt.State == ResourceEffectState.Committed) {
            NotificationService.Success($"Resource {action}", receipt.Message);
        } else if (receipt.State == ResourceEffectState.CommittedWarning) {
            NotificationService.Warning($"Resource {action}; refresh incomplete", $"Resource '{receipt.ResourceId:D}' was {action}. {receipt.Message}");
        } else if (receipt.State == ResourceEffectState.Unknown) {
            NotificationService.Warning(saved ? "Resource save outcome unknown" : "Resource delete outcome unknown", receipt.Message);
        } else {
            NotificationService.Error(saved ? "Resource save failed" : "Resource delete failed", receipt.Message);
        }
    }
    public async ValueTask DisposeAsync() {
        if (disposed) {
            return;
        }
        disposed = true;
        CancelSearchCommit();
        Registry.Changed -= OnChanged;
        Browse.Changed -= OnChanged;
        Registry.Completed -= NotifyOutcome;
        Registry.RouteChanged -= HandleRegistryRouteChanged;
        Registry.FiltersChanged -= HandleFiltersChanged;
        Browse.SourceSelected -= HandleSourceSelected;
        Registry.Dispose();
        await Browse.DisposeAsync();
    }
}

using CanDoItAll.AppComponents;
using CanDoItAll.Application.UI.Profiles;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.SharedKernel;
using CanDoItAll.Web.Composition;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.JSInterop;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Web.Components.Layout;

public partial class MainLayout
{
    private readonly IReadOnlyList<ShellWorkspaceItem> workspaces =
    [
        new("delivery", "Delivery Workspace", "Project authoring, structure, calendars, and prompt drafts.", "/projects"),
        new("quality", "Quality Desk", "Test plans and evidence review.", "/test-lab"),
        new("operations", "Operations Desk", "Scheduler, runtime settings, and environment status.", "/scheduler")
    ];

    private string activeWorkspaceId = "delivery";
    private DotNetObjectReference<MainLayout>? databaseSwitchListenerReference;
    private IReadOnlyList<DatabaseProfileSummary> databaseProfiles = [];
    private DatabaseSelectionStateModel? databaseSelection;
    private DatabaseProfileEditorModel? currentDatabaseEditor;
    private Guid? selectedDatabaseProfileId;
    private string pendingCreatedDatabaseName = string.Empty;
    private IReadOnlyList<DatabaseTransferSourceSummary> createdDatabaseTransferSources = [];
    private IReadOnlyList<DatabaseTransferItemPreview> createdDatabaseTransferItems = [];
    private readonly HashSet<string> createdDatabaseSelectedTransferItemKeys = new(StringComparer.OrdinalIgnoreCase);
    private bool databaseDialogOpen;
    private bool databaseDialogStartupMode;
    private bool databaseDialogBusy;
    private DatabaseProfileMessage? databaseProfileMessage;
    private Guid databaseDialogOpeningId = Guid.NewGuid();
    private long databaseProfileReadGeneration;
    private bool databaseActivationUncertain;
    private long lastObservedDatabaseSwitchGeneration;
    private string? databaseSwitchAlert;
    private int collaborationUnreadCount;
    private long collaborationReadGeneration;
    private bool collaborationDisposed;

    [Inject]
    private ILogger<MainLayout> CollaborationLogger { get; set; } = default!;

    [Inject]
    private IEnumerable<IShellNavigationContributor> ShellNavigationContributors { get; set; } = [];

    /// <summary>
    /// Available only during the static prerender pass — used to read the AppToolbar help/stats
    /// visibility cookies synchronously in <c>OnInitialized</c>. The prerender pass and the interactive
    /// circuit that follows run in separate DI scopes (a fresh <see cref="AppToolbarState"/> instance
    /// each), so the resolved value must be handed off via <see cref="ApplicationState"/>
    /// (<see cref="PersistentComponentState"/>) rather than assumed to carry over — see
    /// <see cref="InitializeToolbarVisibility"/>.
    /// </summary>
    [CascadingParameter]
    private HttpContext? HttpContext { get; set; }

    [Inject]
    private PersistentComponentState ApplicationState { get; set; } = default!;

    private PersistingComponentStateSubscription toolbarVisibilityPersistingSubscription;

    private const string ToolbarVisibilityPersistenceKey = "app-toolbar-visibility";

    private void InitializeToolbarVisibility()
    {
        if (ApplicationState.TryTakeFromJson<ToolbarVisibilitySnapshot>(ToolbarVisibilityPersistenceKey, out var persisted))
        {
            // Interactive circuit resuming after prerender: HttpContext is null here (a fresh DI scope,
            // no HTTP request), so use the value the prerender pass already resolved and persisted.
            ToolbarState.InitializeFromCookies(
                persisted!.HelpVisible ? "true" : "false",
                persisted.StatsVisible ? "true" : "false");
        }
        else
        {
            // Static prerender pass (or a non-prerendered request): read the cookies directly.
            ToolbarState.InitializeFromCookies(
                HttpContext?.Request.Cookies[AppToolbarState.HelpVisibleCookieName],
                HttpContext?.Request.Cookies[AppToolbarState.StatsVisibleCookieName]);
        }

        toolbarVisibilityPersistingSubscription = ApplicationState.RegisterOnPersisting(() =>
        {
            ApplicationState.PersistAsJson(
                ToolbarVisibilityPersistenceKey,
                new ToolbarVisibilitySnapshot(ToolbarState.HelpVisible, ToolbarState.StatsVisible));
            return Task.CompletedTask;
        });
    }

    private sealed record ToolbarVisibilitySnapshot(bool HelpVisible, bool StatsVisible);

    private static readonly TimeSpan ShellMenuTooltipDelay = TimeSpan.FromSeconds(2);

    private string CurrentLocation => Navigation.ToBaseRelativePath(Navigation.Uri);

    private Uri CurrentUri => Navigation.ToAbsoluteUri(Navigation.Uri);

    private string CurrentRouteBase => CurrentUri.AbsolutePath.TrimStart('/');

    private string CurrentRouteDisplay => string.IsNullOrWhiteSpace(CurrentLocation) ? "/" : $"/{CurrentLocation}";

    private ShellNavigationItem ActiveNavigation => ShellNavigation.MatchRoute(CurrentRouteBase, ShellNavigationContributors);

    private string ActiveWorkspaceTitle => workspaces.FirstOrDefault(item => item.Id == activeWorkspaceId)?.Title ?? "Workspace";

    private string ActiveTabTitle => Workbench.GetActiveTab()?.Title ?? ActiveNavigation.Title;

    private string? ActiveProjectTitle => Workbench.GetActiveTab()?.ProjectName;

    private string? ActiveProjectContextText =>
        CurrentRouteBase.StartsWith("projects/", StringComparison.OrdinalIgnoreCase)
            ? ActiveProjectTitle
            : null;

    private string? ActivePhaseTitle => Workbench.GetActiveTab()?.PhaseName;

    private AppShellMode ShellMode => IsFocusWorkbenchRoute ? AppShellMode.FocusWorkbench : AppShellMode.StandardPage;

    private bool IsFocusWorkbenchRoute
        => CurrentUri.AbsolutePath.EndsWith("/structure", StringComparison.OrdinalIgnoreCase) ||
           CurrentUri.AbsolutePath.EndsWith("/processes", StringComparison.OrdinalIgnoreCase) ||
           CurrentUri.AbsolutePath.EndsWith("/processes/live", StringComparison.OrdinalIgnoreCase);

    private IReadOnlyList<WorkbenchTabState> OpenedProjectTabs => Workbench.Tabs
        .Where(tab => tab.ProjectId.HasValue && tab.TabKind is WorkbenchTabKinds.ProjectOverview or WorkbenchTabKinds.ProjectStructure or WorkbenchTabKinds.ProjectCalendar)
        .GroupBy(tab => tab.ProjectId)
        .Select(group => group.First())
        .ToList();

    private IReadOnlyList<WorkbenchTabState> OpenedPromptTabs => Workbench.Tabs
        .Where(tab => string.Equals(tab.TabKind, WorkbenchTabKinds.PromptDetail, StringComparison.Ordinal))
        .ToList();

    private bool CanManageDatabases => databaseSelection?.IsRuntimeLocked != true;

    private bool SelectedDialogProfileIsActive
        => selectedDatabaseProfileId.HasValue &&
           databaseSelection is not null &&
           selectedDatabaseProfileId.Value == databaseSelection.RuntimeProfileId;

    private bool SelectedDialogProfileIsPendingRestart
        => selectedDatabaseProfileId.HasValue &&
           databaseSelection?.PendingRestartProfileId == selectedDatabaseProfileId.Value;

    private bool CanSwitchSelectedProfile
        => CanManageDatabases &&
           !databaseActivationUncertain &&
           selectedDatabaseProfileId.HasValue &&
           !SelectedDialogProfileIsActive &&
           !SelectedDialogProfileIsPendingRestart;

    private IReadOnlyList<ShellNavigationItem> NavigationItems => ShellNavigation.GetItems(collaborationUnreadCount, ShellNavigationContributors);

    protected override void OnInitialized()
    {
        originalProfile = LayoutProfileResolver.ResolveCurrentProfile();
        Navigation.LocationChanged += HandleLocationChanged;
        Workbench.Changed += HandleWorkbenchChanged;
        DatabaseSwitchNotificationService.Changed += HandleDatabaseSwitchChanged;
        CollaborationService.Changed += HandleCollaborationChanged;
        ThemeState.Changed += HandleThemeStateChanged;
        InitializeToolbarVisibility();
        activeWorkspaceId = ResolveWorkspaceId(CurrentUri.AbsolutePath);
        _ = RunLayoutWorkAsync(nameof(LoadCollaborationShellStateAsync), LoadCollaborationShellStateAsync);
    }

    protected override Task OnAfterRenderAsync(bool firstRender)
        => firstRender ? RunLayoutWorkAsync(nameof(InitializeLayoutAsync), InitializeLayoutAsync) : Task.CompletedTask;

    private async Task InitializeLayoutAsync() {
        if (layoutInitializationBusy) {
            return;
        }
        layoutInitializationBusy = true;
        layoutInitializationFailed = false;
        try {
            await Workbench.InitializeAsync(
                [
                    new WorkbenchTabState(
                        "route:dashboard",
                        "Dashboard",
                        "/",
                        IsPinned: true,
                        CanClose: false,
                        TabKind: WorkbenchTabKinds.Page,
                        ArtifactKey: "/",
                        RestoreKey: "/",
                        TabGroup: "Workspace")
                ], layoutLifetime.Token);
            EnsureLayoutCurrent();
            workbenchInitialized = true;
            await TrackNavigationAsync(BeginNavigationRead(Navigation.HistoryEntryState), pruneDeletedTabs: true);
            EnsureLayoutCurrent();
            await RegisterDatabaseSwitchListenerAsync();
            EnsureLayoutCurrent();
            await RestoreDatabaseSwitchAlertAsync();
            EnsureLayoutCurrent();
            await LoadDatabaseProfileUiAsync(showStartupPrompt: true);
            EnsureLayoutCurrent();
            StateHasChanged();
        } finally {
            layoutInitializationBusy = false;
        }
    }

    public void Dispose()
    {
        if (collaborationDisposed) {
            return;
        }
        collaborationDisposed = true;
        layoutLifetime.Cancel();
        navigationCancellation?.Cancel();
        ++collaborationReadGeneration;
        Navigation.LocationChanged -= HandleLocationChanged;
        Workbench.Changed -= HandleWorkbenchChanged;
        DatabaseSwitchNotificationService.Changed -= HandleDatabaseSwitchChanged;
        CollaborationService.Changed -= HandleCollaborationChanged;
        ThemeState.Changed -= HandleThemeStateChanged;
        toolbarVisibilityPersistingSubscription.Dispose();
        _ = ReleaseDatabaseSwitchListenerAsync();
        if (pendingLayoutWork == 0) {
            layoutLifetime.Dispose();
        }
    }

    private void HandleThemeStateChanged()
        => InvokeAsync(StateHasChanged);

}

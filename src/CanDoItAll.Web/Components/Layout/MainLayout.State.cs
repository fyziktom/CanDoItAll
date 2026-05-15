using CanDoItAll.Components;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.SharedKernel;
using CanDoItAll.Web.Composition;
using CanDoItAll.Web.Infrastructure;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CanDoItAll.Web.Components.Layout;

public partial class MainLayout
{
    private readonly IReadOnlyList<ShellWorkspaceItem> workspaces =
    [
        new("delivery", "Delivery Workspace", "Project authoring, structure, calendars, and prompt sessions.", "/projects"),
        new("quality", "Quality Desk", "Validation runs, test plans, and evidence review.", "/validation"),
        new("automation", "Automation Ops", "Activity, automation status, and environment settings.", "/automation")
    ];

    private string activeWorkspaceId = "delivery";
    private string tuningInstruction = string.Empty;
    private string? tuningMessage;
    private bool tuningBusy;
    private readonly List<TuningAttachmentRequest> tuningAttachments = [];
    private DevelopmentTuningRequestResult? pendingTuningRequest;
    private DotNetObjectReference<MainLayout>? databaseSwitchListenerReference;
    private IReadOnlyList<DatabaseProfileSummary> databaseProfiles = [];
    private DatabaseSelectionStateModel? databaseSelection;
    private Guid? selectedDatabaseProfileId;
    private Guid? pendingCreatedDatabaseProfileId;
    private string pendingCreatedDatabaseName = string.Empty;
    private IReadOnlyList<DatabaseTransferSourceSummary> createdDatabaseTransferSources = [];
    private IReadOnlyList<DatabaseTransferItemPreview> createdDatabaseTransferItems = [];
    private readonly HashSet<string> createdDatabaseSelectedTransferItemKeys = new(StringComparer.OrdinalIgnoreCase);
    private Guid? createdDatabaseTransferSourceProfileId;
    private bool databaseDialogOpen;
    private bool databaseDialogStartupMode;
    private bool databaseDialogBusy;
    private bool createdDatabaseTransferBusy;
    private string? databaseProfileMessage;
    private long lastObservedDatabaseSwitchGeneration;
    private string? databaseSwitchAlert;
    private int collaborationUnreadCount;

    private string CurrentLocation => Navigation.ToBaseRelativePath(Navigation.Uri);

    private Uri CurrentUri => Navigation.ToAbsoluteUri(Navigation.Uri);

    private string CurrentRouteBase => CurrentUri.AbsolutePath.TrimStart('/');

    private string CurrentRouteDisplay => string.IsNullOrWhiteSpace(CurrentLocation) ? "/" : $"/{CurrentLocation}";

    private ShellNavigationItem ActiveNavigation => ShellNavigation.MatchRoute(CurrentRouteBase);

    private string ActiveWorkspaceTitle => workspaces.FirstOrDefault(item => item.Id == activeWorkspaceId)?.Title ?? "Workspace";

    private string ActiveTabTitle => Workbench.GetActiveTab()?.Title ?? ActiveNavigation.Title;

    private string? ActiveProjectTitle => Workbench.GetActiveTab()?.ProjectName;

    private string? ActivePhaseTitle => Workbench.GetActiveTab()?.PhaseName;

    private AppShellMode ShellMode => IsFocusWorkbenchRoute ? AppShellMode.FocusWorkbench : AppShellMode.StandardPage;

    private bool ShowStandardRail => ShellMode == AppShellMode.StandardPage && DevelopmentManagerOptions.Value.TuningModeEnabled;

    private bool ShowCollapsedRail => ShellMode == AppShellMode.FocusWorkbench && DevelopmentManagerOptions.Value.TuningModeEnabled;

    private bool IsFocusWorkbenchRoute
        => CurrentUri.AbsolutePath.EndsWith("/structure", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(CurrentUri.AbsolutePath, "/processes", StringComparison.OrdinalIgnoreCase) ||
           CurrentUri.AbsolutePath.EndsWith("/processes/live", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(CurrentUri.AbsolutePath, "/prompt-factory", StringComparison.OrdinalIgnoreCase);

    private IReadOnlyList<WorkbenchTabState> OpenedProjectTabs => Workbench.Tabs
        .Where(tab => tab.ProjectId.HasValue && tab.TabKind is WorkbenchTabKinds.ProjectOverview or WorkbenchTabKinds.ProjectStructure or WorkbenchTabKinds.ProjectCalendar)
        .GroupBy(tab => tab.ProjectId)
        .Select(group => group.First())
        .ToList();

    private IReadOnlyList<WorkbenchTabState> OpenedPromptSessionTabs => Workbench.Tabs
        .Where(tab => string.Equals(tab.TabKind, WorkbenchTabKinds.PromptWizardSession, StringComparison.Ordinal))
        .ToList();

    private bool TuningEnabled => DevelopmentManagerOptions.Value.TuningModeEnabled && CurrentTuningSurface is not null;

    private TuningSurfaceDefinition? CurrentTuningSurface => ResolveTuningSurface(CurrentUri.AbsolutePath);

    private bool CanManageDatabases => databaseSelection?.IsRuntimeLocked != true;

    private bool SelectedDialogProfileIsActive
        => selectedDatabaseProfileId.HasValue &&
           databaseSelection is not null &&
           selectedDatabaseProfileId.Value == databaseSelection.ActiveProfileId;

    private bool CanSwitchSelectedProfile
        => CanManageDatabases &&
           selectedDatabaseProfileId.HasValue &&
           !SelectedDialogProfileIsActive;

    private IReadOnlyList<ShellNavigationItem> NavigationItems => ShellNavigation.GetItems(collaborationUnreadCount);

    protected override void OnInitialized()
    {
        Navigation.LocationChanged += HandleLocationChanged;
        Workbench.Changed += HandleWorkbenchChanged;
        TuningCoordinator.Changed += HandleWorkbenchChanged;
        DatabaseSwitchNotificationService.Changed += HandleDatabaseSwitchChanged;
        CollaborationService.Changed += HandleCollaborationChanged;
        activeWorkspaceId = ResolveWorkspaceId(CurrentUri.AbsolutePath);
        _ = LoadCollaborationShellStateAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

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
            ]);

        await ResolveAndTrackCurrentTabAsync();
        databaseSwitchListenerReference = DotNetObjectReference.Create(this);
        await JS.InvokeVoidAsync("CanDoItAll.browserState.registerDatabaseSwitchListener", databaseSwitchListenerReference);
        await RestoreDatabaseSwitchAlertAsync();
        await LoadDatabaseProfileUiAsync(showStartupPrompt: true);
        StateHasChanged();
    }

    public void Dispose()
    {
        Navigation.LocationChanged -= HandleLocationChanged;
        Workbench.Changed -= HandleWorkbenchChanged;
        TuningCoordinator.Changed -= HandleWorkbenchChanged;
        DatabaseSwitchNotificationService.Changed -= HandleDatabaseSwitchChanged;
        CollaborationService.Changed -= HandleCollaborationChanged;
        databaseSwitchListenerReference?.Dispose();
    }

    private sealed record TuningSurfaceDefinition(string CapsuleKey, string ComponentName, string ContextSummary);
}

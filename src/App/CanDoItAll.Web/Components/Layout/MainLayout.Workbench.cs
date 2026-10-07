using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.WebUtilities;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using CanDoItAll.Web.Composition;

namespace CanDoItAll.Web.Components.Layout;

public partial class MainLayout
{
    private Task HandleNavigateAsync(string route)
    {
        if (IsLayoutCurrent) {
            Navigation.NavigateTo(route);
        }
        return Task.CompletedTask;
    }

    private Task OpenSettingsFromShellAsync()
        => HandleNavigateAsync("/settings");

    private Task OpenRuntimeCapabilitiesFromShellAsync()
        => HandleNavigateAsync("/settings/runtime-capabilities");

    private Task HandleOpenWorkbenchItemAsync(string tabId) => HandleSelectTabAsync(tabId);

    private Task HandleSelectWorkspaceAsync(string workspaceId)
    {
        if (!IsLayoutCurrent) {
            return Task.CompletedTask;
        }
        activeWorkspaceId = workspaceId;
        var workspace = workspaces.FirstOrDefault(item => item.Id == workspaceId);
        if (workspace is not null)
        {
            Navigation.NavigateTo(workspace.DefaultRoute);
        }

        return Task.CompletedTask;
    }

    private Task HandleSelectTabAsync(string tabId) => RunLayoutWorkAsync(nameof(HandleSelectTabAsync), async () => {
        var tab = Workbench.Tabs.FirstOrDefault(candidate => string.Equals(candidate.TabId, tabId, StringComparison.Ordinal));
        if (tab is null)
        {
            return;
        }

        Navigation.NavigateTo(tab.Route);
        if (IsLayoutCurrent) {
            await Workbench.ActivateAsync(tab.TabId, layoutLifetime.Token);
        }
    });

    private Task HandleCloseTabAsync(string tabId) => RunLayoutWorkAsync(nameof(HandleCloseTabAsync), async () => {
        var originalUri = CurrentUri;
        await Workbench.CloseAsync(tabId, layoutLifetime.Token);
        if (!IsLayoutCurrent || CurrentUri != originalUri) {
            return;
        }
        var activeTab = Workbench.GetActiveTab();
        if (activeTab is not null && !string.Equals(CurrentRouteDisplay, activeTab.Route, StringComparison.OrdinalIgnoreCase))
        {
            Navigation.NavigateTo(activeTab.Route);
        }
    });

    private Task HandleMoveLeftAsync(string tabId) => RunLayoutWorkAsync(nameof(Workbench.MoveAsync), () => Workbench.MoveAsync(tabId, -1, layoutLifetime.Token));

    private Task HandleMoveRightAsync(string tabId) => RunLayoutWorkAsync(nameof(Workbench.MoveAsync), () => Workbench.MoveAsync(tabId, 1, layoutLifetime.Token));

    private Task HandleTogglePinAsync(string tabId) => RunLayoutWorkAsync(nameof(Workbench.TogglePinAsync), () => Workbench.TogglePinAsync(tabId, layoutLifetime.Token));

    private Task HandleToggleSleepAsync(string tabId) => RunLayoutWorkAsync(nameof(Workbench.ToggleSleepAsync), () => Workbench.ToggleSleepAsync(tabId, layoutLifetime.Token));

    private Task HandleCloseOthersAsync(string tabId) => RunLayoutWorkAsync(nameof(Workbench.CloseOthersAsync), () => Workbench.CloseOthersAsync(tabId, layoutLifetime.Token));

    private Task HandleCloseRightAsync(string tabId) => RunLayoutWorkAsync(nameof(Workbench.CloseRightAsync), () => Workbench.CloseRightAsync(tabId, layoutLifetime.Token));

    private Task HandleCloseBackgroundAsync(string tabId) => RunLayoutWorkAsync(nameof(Workbench.CloseAllBackgroundAsync), () => Workbench.CloseAllBackgroundAsync(tabId, layoutLifetime.Token));

    private Task HandleReopenRecentAsync(string tabId) => RunLayoutWorkAsync(nameof(HandleReopenRecentAsync), async () => {
        var originalUri = CurrentUri;
        await Workbench.ReopenRecentAsync(tabId, layoutLifetime.Token);
        if (!IsLayoutCurrent || CurrentUri != originalUri) {
            return;
        }
        var reopened = Workbench.GetActiveTab();
        if (reopened is not null)
        {
            Navigation.NavigateTo(reopened.Route);
        }
    });

    private Task HandleClearRecentTabsAsync() => RunLayoutWorkAsync(nameof(Workbench.ClearRecentTabsAsync), () => Workbench.ClearRecentTabsAsync(layoutLifetime.Token));

    private void HandleWorkbenchChanged()
        => _ = RunLayoutWorkAsync(nameof(HandleWorkbenchChanged), () => InvokeAsync(() => {
            if (IsLayoutCurrent) {
                StateHasChanged();
            }
        }));

    private void HandleLocationChanged(object? sender, LocationChangedEventArgs e) {
        if (!IsLayoutCurrent || !workbenchInitialized) {
            return;
        }
        var read = BeginNavigationRead();
        NavigationCompletion = RunLayoutWorkAsync(nameof(HandleLocationChanged), () => InvokeAsync(() => TrackNavigationAsync(read)));
        _ = RunLayoutWorkAsync(nameof(LoadCollaborationShellStateAsync), LoadCollaborationShellStateAsync);
    }

    private void HandleCollaborationChanged(object? sender, EventArgs e) => _ = RefreshCollaborationBadgeAsync();

    private async Task RefreshCollaborationBadgeAsync() {
        try {
            await InvokeAsync(LoadCollaborationShellStateAsync);
        } catch (Exception exception) {
            CollaborationLogger.LogWarning(exception, "Unable to dispatch the Collaboration shell badge refresh.");
        }
    }

    private async Task CloseDeletedProjectTabsAsync(NavigationRead read)
    {
        var projectIds = (await ProjectsService.ListAsync(read.Cancellation.Token))
            .Select(project => project.Id)
            .ToHashSet();
        EnsureCurrent(read);
        var staleProjectTabIds = Workbench.Tabs
            .Where(tab => IsProjectScopedWorkbenchTab(tab) && !projectIds.Contains(tab.ProjectId!.Value))
            .Select(tab => tab.TabId)
            .ToArray();

        if (staleProjectTabIds.Length == 0)
        {
            return;
        }

        var staleActiveTab = Workbench.ActiveTabId is not null &&
            staleProjectTabIds.Contains(Workbench.ActiveTabId, StringComparer.Ordinal);
        await Workbench.CloseTabsAsync(staleProjectTabIds, rememberRecent: false, cancellationToken: read.Cancellation.Token);
        EnsureCurrent(read);

        if (staleActiveTab)
        {
            Navigation.NavigateTo(Workbench.GetActiveTab()?.Route ?? "/projects", replace: true);
        }
    }

    private async Task<WorkbenchTabDescriptor> ResolveCurrentTabDescriptorAsync(NavigationRead read)
    {
        var uri = read.Uri;
        var path = uri.AbsolutePath;
        var query = QueryHelpers.ParseQuery(uri.Query);
        var route = $"{path}{uri.Query}";

        if (string.Equals(path, "/projects", StringComparison.OrdinalIgnoreCase) &&
            TryReadGuid(query, "projectId", out var projectId))
        {
            var project = await LoadExistingProjectForWorkbenchAsync(projectId, read);
            if (project is null)
            {
                Navigation.NavigateTo("/projects", replace: true);
                return BuildPageDescriptor("/projects", "/projects");
            }

            return new WorkbenchTabDescriptor(
                $"project:{projectId:N}",
                string.IsNullOrWhiteSpace(project.Name) ? "Project Overview" : project.Name,
                route,
                WorkbenchTabKinds.ProjectOverview,
                ProjectId: projectId,
                ArtifactId: projectId,
                ArtifactKind: "project",
                ArtifactKey: $"project:{projectId:N}",
                RestoreKey: $"project:{projectId:N}",
                ProjectScope: projectId.ToString(),
                ProjectName: project.Name,
                PhaseName: project.CurrentPhase,
                Description: "Wizard-first project overview and editing session.",
                TabGroup: "Projects");
        }

        if (TryReadProjectSurface(path, "structure", out var structureProjectId))
        {
            var project = await LoadExistingProjectForWorkbenchAsync(structureProjectId, read);
            if (project is null)
            {
                Navigation.NavigateTo("/projects", replace: true);
                return BuildPageDescriptor("/projects", "/projects");
            }

            return new WorkbenchTabDescriptor(
                $"project-structure:{structureProjectId:N}",
                $"{project.Name} · Structure",
                route,
                WorkbenchTabKinds.ProjectStructure,
                ProjectId: structureProjectId,
                ArtifactId: structureProjectId,
                ArtifactKind: "project-structure",
                ArtifactKey: $"project-structure:{structureProjectId:N}",
                RestoreKey: $"project-structure:{structureProjectId:N}",
                ProjectScope: structureProjectId.ToString(),
                ProjectName: project.Name,
                PhaseName: project.CurrentPhase,
                Description: "Canvas-driven project structure workbench.",
                TabGroup: "Projects");
        }

        if (TryReadProjectSurface(path, "calendar", out var calendarProjectId))
        {
            var project = await LoadExistingProjectForWorkbenchAsync(calendarProjectId, read);
            if (project is null)
            {
                Navigation.NavigateTo("/projects", replace: true);
                return BuildPageDescriptor("/projects", "/projects");
            }

            return new WorkbenchTabDescriptor(
                $"project-calendar:{calendarProjectId:N}",
                $"{project.Name} · Calendar",
                route,
                WorkbenchTabKinds.ProjectCalendar,
                ProjectId: calendarProjectId,
                ArtifactId: calendarProjectId,
                ArtifactKind: "project-calendar",
                ArtifactKey: $"project-calendar:{calendarProjectId:N}",
                RestoreKey: $"project-calendar:{calendarProjectId:N}",
                ProjectScope: calendarProjectId.ToString(),
                ProjectName: project.Name,
                PhaseName: project.CurrentPhase,
                Description: "Project calendar and scheduled artifact surface.",
                TabGroup: "Projects");
        }

        if (TryReadProjectProcessesSurface(path, out var processProjectId, out var isProjectLiveProcesses))
        {
            var project = await LoadExistingProjectForWorkbenchAsync(processProjectId, read);
            if (project is null)
            {
                Navigation.NavigateTo("/projects", replace: true);
                return BuildPageDescriptor("/projects", "/projects");
            }

            var title = isProjectLiveProcesses
                ? $"{project.Name} · Live Processes"
                : $"{project.Name} · Processes";
            var artifactKind = isProjectLiveProcesses
                ? "process-live-dashboard"
                : "process-workspace";

            return new WorkbenchTabDescriptor(
                isProjectLiveProcesses
                    ? $"project-processes-live:{processProjectId:N}"
                    : $"project-processes:{processProjectId:N}",
                title,
                route,
                WorkbenchTabKinds.Processes,
                ProjectId: processProjectId,
                ArtifactId: processProjectId,
                ArtifactKind: artifactKind,
                ArtifactKey: $"{artifactKind}:{processProjectId:N}",
                RestoreKey: $"{artifactKind}:{processProjectId:N}",
                ProjectScope: processProjectId.ToString("D"),
                ProjectName: project.Name,
                PhaseName: project.CurrentPhase,
                Description: isProjectLiveProcesses
                    ? "Project-scoped live process projection shell."
                    : "Project-scoped process workspace projection shell.",
                TabGroup: "Processes");
        }

        if (string.Equals(path, "/processes", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, "/processes/live", StringComparison.OrdinalIgnoreCase))
        {
            var isLiveProcesses = path.EndsWith("/live", StringComparison.OrdinalIgnoreCase);
            var artifactKind = isLiveProcesses
                ? "process-live-dashboard"
                : "process-workspace";

            return new WorkbenchTabDescriptor(
                isLiveProcesses ? "route:processes-live" : "route:processes",
                isLiveProcesses ? "Live Processes" : "Processes",
                route,
                WorkbenchTabKinds.Processes,
                ArtifactKind: artifactKind,
                ArtifactKey: artifactKind,
                RestoreKey: artifactKind,
                Description: isLiveProcesses
                    ? "Global live process projection shell."
                    : "Global process workspace projection shell.",
                TabGroup: "Processes",
                IsPinned: false);
        }

        if (string.Equals(path, "/test-lab", StringComparison.OrdinalIgnoreCase) &&
            TryReadGuid(query, "planId", out var testPlanId))
        {
            return new WorkbenchTabDescriptor(
                $"test-plan:{testPlanId:N}",
                "Test Plan",
                route,
                WorkbenchTabKinds.TestPlan,
                ArtifactId: testPlanId,
                ArtifactKind: "test-plan",
                ArtifactKey: $"test-plan:{testPlanId:N}",
                RestoreKey: $"test-plan:{testPlanId:N}",
                Description: "Test plan artifact session.",
                TabGroup: "Testing");
        }

        if (string.Equals(path, "/prompt-gallery", StringComparison.OrdinalIgnoreCase) &&
            TryReadGuid(query, "promptId", out var promptId))
        {
            return new WorkbenchTabDescriptor(
                $"prompt:{promptId:N}",
                "Prompt Detail",
                route,
                WorkbenchTabKinds.PromptDetail,
                ArtifactId: promptId,
                ArtifactKind: "prompt",
                ArtifactKey: $"prompt:{promptId:N}",
                RestoreKey: $"prompt:{promptId:N}",
                Description: "Prompt detail artifact.",
                TabGroup: "Prompt Gallery");
        }

        return BuildPageDescriptor(path, route);
    }

    private static bool TryReadGuid(IDictionary<string, Microsoft.Extensions.Primitives.StringValues> query, string key, out Guid value)
        => Guid.TryParse(query.TryGetValue(key, out var raw) ? raw.ToString() : null, out value);

    private static bool TryReadProjectSurface(string path, string surfaceSegment, out Guid projectId)
    {
        projectId = Guid.Empty;
        var segments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length == 3 &&
               string.Equals(segments[0], "projects", StringComparison.OrdinalIgnoreCase) &&
               Guid.TryParse(segments[1], out projectId) &&
               string.Equals(segments[2], surfaceSegment, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryReadProjectProcessesSurface(string path, out Guid projectId, out bool isLive)
    {
        projectId = Guid.Empty;
        isLive = false;
        var segments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length is not 3 and not 4 ||
            !string.Equals(segments[0], "projects", StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParse(segments[1], out projectId) ||
            !string.Equals(segments[2], "processes", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (segments.Length == 3)
        {
            return true;
        }

        isLive = string.Equals(segments[3], "live", StringComparison.OrdinalIgnoreCase);
        return isLive;
    }

    private async Task<ProjectEditorModel?> LoadExistingProjectForWorkbenchAsync(Guid projectId, NavigationRead read)
    {
        var project = await ProjectsService.GetAsync(projectId, read.Cancellation.Token);
        EnsureCurrent(read);
        return project.Id.HasValue ? project : null;
    }

    internal WorkbenchTabDescriptor BuildPageDescriptor(string path, string route)
    {
        var navigation = ShellNavigation.MatchRoute(path.TrimStart('/'), ShellNavigationContributors);
        var title = CrmHrRouteCatalog.TryResolve(path, out var crmHrRoute)
            ? crmHrRoute.WorkbenchTitle
            : navigation.Title;

        return new WorkbenchTabDescriptor(
            string.Equals(path, "/settings", StringComparison.OrdinalIgnoreCase) ? "route:settings" : BuildPageTabId(path),
            title,
            route,
            string.Equals(path, "/settings", StringComparison.OrdinalIgnoreCase) ? WorkbenchTabKinds.Settings : WorkbenchTabKinds.Page,
            ArtifactKind: navigation.Title,
            ArtifactKey: NormalizeArtifactKey(path),
            RestoreKey: NormalizeArtifactKey(path),
            Description: navigation.Description,
            TabGroup: ResolvePageGroup(path),
            IsPinned: navigation.PinnedByDefault,
            CanClose: !navigation.PinnedByDefault);
    }

    private static bool IsProjectScopedWorkbenchTab(WorkbenchTabState tab)
        => tab.ProjectId.HasValue &&
           tab.TabKind is WorkbenchTabKinds.ProjectOverview or WorkbenchTabKinds.ProjectStructure or WorkbenchTabKinds.ProjectCalendar or WorkbenchTabKinds.Processes;

    private static string ResolveWorkspaceId(string path)
        => path.Trim().ToLowerInvariant() switch
        {
            "/test-lab" => "quality",
            "/scheduler" or "/settings" => "operations",
            _ => "delivery"
        };

    private static string ResolvePageGroup(string path)
    {
        var normalized = path.Trim().ToLowerInvariant();
        if (normalized.StartsWith("/projects/", StringComparison.Ordinal) &&
            (normalized.EndsWith("/processes", StringComparison.Ordinal) ||
             normalized.EndsWith("/processes/live", StringComparison.Ordinal)))
        {
            return "Processes";
        }

        return normalized switch
        {
            var candidate when candidate.StartsWith("/crm-hr", StringComparison.Ordinal) => "CRM / HR",
            var candidate when candidate.StartsWith("/processes", StringComparison.Ordinal) => "Processes",
            "/test-lab" => "Testing",
            "/scheduler" => "Scheduler",
            "/settings" => "Settings",
            _ => "Workspace"
        };
    }

    private static string NormalizeArtifactKey(string path)
        => string.IsNullOrWhiteSpace(path) || string.Equals(path, "/", StringComparison.Ordinal) ? "/" : path.Trim();

    private static string BuildPageTabId(string path)
    {
        var normalized = string.IsNullOrWhiteSpace(path) || string.Equals(path, "/", StringComparison.Ordinal)
            ? "dashboard"
            : path.Trim('/').Replace('/', ':');
        return $"route:{normalized}";
    }
}

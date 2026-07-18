using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.WebUtilities;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using CanDoItAll.Web.Composition;

namespace CanDoItAll.Web.Components.Layout;

public partial class MainLayout
{
    private async Task HandleNavigateAsync(string route)
    {
        Navigation.NavigateTo(route);
        await ResolveAndTrackCurrentTabAsync();
    }

    private Task OpenSettingsFromShellAsync()
        => HandleNavigateAsync("/settings");

    private Task HandleOpenWorkbenchItemAsync(string tabId) => HandleSelectTabAsync(tabId);

    private Task HandleSelectWorkspaceAsync(string workspaceId)
    {
        activeWorkspaceId = workspaceId;
        var workspace = workspaces.FirstOrDefault(item => item.Id == workspaceId);
        if (workspace is not null)
        {
            Navigation.NavigateTo(workspace.DefaultRoute);
        }

        return Task.CompletedTask;
    }

    private async Task HandleSelectTabAsync(string tabId)
    {
        var tab = Workbench.Tabs.FirstOrDefault(candidate => string.Equals(candidate.TabId, tabId, StringComparison.Ordinal));
        if (tab is null)
        {
            return;
        }

        Navigation.NavigateTo(tab.Route);
        await Workbench.ActivateAsync(tab.TabId);
    }

    private async Task HandleCloseTabAsync(string tabId)
    {
        await Workbench.CloseAsync(tabId);
        var activeTab = Workbench.GetActiveTab();
        if (activeTab is not null && !string.Equals(CurrentRouteDisplay, activeTab.Route, StringComparison.OrdinalIgnoreCase))
        {
            Navigation.NavigateTo(activeTab.Route);
        }
    }

    private Task HandleMoveLeftAsync(string tabId) => Workbench.MoveAsync(tabId, -1);

    private Task HandleMoveRightAsync(string tabId) => Workbench.MoveAsync(tabId, 1);

    private Task HandleTogglePinAsync(string tabId) => Workbench.TogglePinAsync(tabId);

    private Task HandleToggleSleepAsync(string tabId) => Workbench.ToggleSleepAsync(tabId);

    private Task HandleCloseOthersAsync(string tabId) => Workbench.CloseOthersAsync(tabId);

    private Task HandleCloseRightAsync(string tabId) => Workbench.CloseRightAsync(tabId);

    private Task HandleCloseBackgroundAsync(string tabId) => Workbench.CloseAllBackgroundAsync(tabId);

    private async Task HandleReopenRecentAsync(string tabId)
    {
        await Workbench.ReopenRecentAsync(tabId);
        var reopened = Workbench.GetActiveTab();
        if (reopened is not null)
        {
            Navigation.NavigateTo(reopened.Route);
        }
    }

    private Task HandleClearRecentTabsAsync() => Workbench.ClearRecentTabsAsync();

    private void HandleWorkbenchChanged() => _ = InvokeAsync(StateHasChanged);

    private void HandleLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        _ = InvokeAsync(async () =>
        {
            activeWorkspaceId = ResolveWorkspaceId(CurrentUri.AbsolutePath);
            await ResolveAndTrackCurrentTabAsync();
            await LoadCollaborationShellStateAsync();
            StateHasChanged();
        });
    }

    private void HandleCollaborationChanged(object? sender, EventArgs e)
    {
        _ = InvokeAsync(async () =>
        {
            await LoadCollaborationShellStateAsync();
            StateHasChanged();
        });
    }

    private async Task ResolveAndTrackCurrentTabAsync()
    {
        var descriptor = await ResolveCurrentTabDescriptorAsync(CurrentUri);
        await Workbench.TrackTabAsync(descriptor);
    }

    private async Task CloseDeletedProjectTabsAsync()
    {
        var projectIds = (await ProjectsService.ListAsync())
            .Select(project => project.Id)
            .ToHashSet();
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
        await Workbench.CloseTabsAsync(staleProjectTabIds, rememberRecent: false);

        if (staleActiveTab)
        {
            Navigation.NavigateTo(Workbench.GetActiveTab()?.Route ?? "/projects", replace: true);
        }
    }

    private async Task<WorkbenchTabDescriptor> ResolveCurrentTabDescriptorAsync(Uri uri)
    {
        var path = uri.AbsolutePath;
        var query = QueryHelpers.ParseQuery(uri.Query);
        var route = $"{path}{uri.Query}";

        if (string.Equals(path, "/projects", StringComparison.OrdinalIgnoreCase) &&
            TryReadGuid(query, "projectId", out var projectId))
        {
            var project = await LoadExistingProjectForWorkbenchAsync(projectId);
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
            var project = await LoadExistingProjectForWorkbenchAsync(structureProjectId);
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
            var project = await LoadExistingProjectForWorkbenchAsync(calendarProjectId);
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
            var project = await LoadExistingProjectForWorkbenchAsync(processProjectId);
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

        if (string.Equals(path, "/prompt-factory", StringComparison.OrdinalIgnoreCase) &&
            (TryReadGuid(query, "sessionId", out var sessionId) || TryReadGuid(query, "runId", out sessionId)))
        {
            var editor = await PromptFactoryService.GetEditorAsync(
                query.ContainsKey("sessionId") ? sessionId : null,
                query.ContainsKey("runId") ? sessionId : null);
            var project = editor.ProjectId.HasValue ? await ProjectsService.GetAsync(editor.ProjectId.Value) : null;
            var sessionLabel = string.IsNullOrWhiteSpace(editor.Phase) ? "Prompt Session" : $"Prompt Session · {editor.Phase}";
            return new WorkbenchTabDescriptor(
                $"prompt-session:{sessionId:N}",
                sessionLabel,
                route,
                WorkbenchTabKinds.PromptWizardSession,
                ProjectId: editor.ProjectId,
                ArtifactId: sessionId,
                ArtifactKind: "prompt-session",
                ArtifactKey: $"prompt-session:{sessionId:N}",
                RestoreKey: $"prompt-session:{sessionId:N}",
                ProjectScope: editor.ProjectId?.ToString(),
                ProjectName: project?.Name,
                PhaseName: editor.Phase,
                Description: "Prompt wizard session with branchable flow state.",
                TabGroup: "Prompt Sessions");
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
                TabGroup: "Prompt Sessions");
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

    private async Task<ProjectEditorModel?> LoadExistingProjectForWorkbenchAsync(Guid projectId)
    {
        var project = await ProjectsService.GetAsync(projectId);
        return project.Id.HasValue ? project : null;
    }

    private WorkbenchTabDescriptor BuildPageDescriptor(string path, string route)
    {
        var navigation = ShellNavigation.MatchRoute(path.TrimStart('/'), ShellNavigationContributors);
        return new WorkbenchTabDescriptor(
            string.Equals(path, "/settings", StringComparison.OrdinalIgnoreCase) ? "route:settings" : BuildPageTabId(path),
            navigation.Title,
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

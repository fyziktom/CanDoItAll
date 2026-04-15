using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.WebUtilities;
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

    private async Task<WorkbenchTabDescriptor> ResolveCurrentTabDescriptorAsync(Uri uri)
    {
        var path = uri.AbsolutePath;
        var query = QueryHelpers.ParseQuery(uri.Query);
        var route = $"{path}{uri.Query}";

        if (string.Equals(path, "/projects", StringComparison.OrdinalIgnoreCase) &&
            TryReadGuid(query, "projectId", out var projectId))
        {
            var project = await ProjectsService.GetAsync(projectId);
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
            var project = await ProjectsService.GetAsync(structureProjectId);
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
            var project = await ProjectsService.GetAsync(calendarProjectId);
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

        if (string.Equals(path, "/validation", StringComparison.OrdinalIgnoreCase) &&
            TryReadGuid(query, "runId", out var validationRunId))
        {
            return new WorkbenchTabDescriptor(
                $"validation:{validationRunId:N}",
                "Validation Run",
                route,
                WorkbenchTabKinds.ValidationRun,
                ArtifactId: validationRunId,
                ArtifactKind: "validation-run",
                ArtifactKey: $"validation:{validationRunId:N}",
                RestoreKey: $"validation:{validationRunId:N}",
                Description: "Validation review artifact.",
                TabGroup: "Validation");
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

        var navigation = ShellNavigation.MatchRoute(path.TrimStart('/'));
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

    private static TuningSurfaceDefinition? ResolveTuningSurface(string path)
    {
        var normalized = path.Trim().ToLowerInvariant();
        if (normalized.EndsWith("/structure", StringComparison.Ordinal))
        {
            return new TuningSurfaceDefinition("page-projectstructurepage", "ProjectStructurePage", "Project structure canvas and outline coordination.");
        }

        if (normalized.EndsWith("/calendar", StringComparison.Ordinal))
        {
            return new TuningSurfaceDefinition("page-projectcalendarpage", "ProjectCalendarPage", "Project events calendar surface and artifact navigation.");
        }

        return normalized switch
        {
            "/" => new TuningSurfaceDefinition("page-home", "Home", "Dashboard summary and startup guidance."),
            "/projects" => new TuningSurfaceDefinition("page-projectspage", "ProjectsPage", "Project list, editor, and phase planning."),
            "/crm-hr" => new TuningSurfaceDefinition("page-crmhrhomepage", "CrmHrHomePage", "CRM / HR module summary and route hub."),
            "/crm-hr/directory" => new TuningSurfaceDefinition("page-crmhrdirectorypage", "CrmHrDirectoryPage", "Unified party directory and editor."),
            "/crm-hr/crm" => new TuningSurfaceDefinition("page-crmhrcrmpage", "CrmHrCrmPage", "CRM workspace shell for accounts and opportunities."),
            "/crm-hr/workforce" => new TuningSurfaceDefinition("page-crmhrworkforcepage", "CrmHrWorkforcePage", "Workforce shell for delivery units and staffing supply."),
            "/crm-hr/recruiting" => new TuningSurfaceDefinition("page-crmhrrecruitingpage", "CrmHrRecruitingPage", "Recruiting shell for candidates and lifecycle tasks."),
            "/crm-hr/agents" => new TuningSurfaceDefinition("page-crmhragentspage", "CrmHrAgentsPage", "AI agent directory and governance shell."),
            "/crm-hr/assignments" => new TuningSurfaceDefinition("page-crmhrassignmentspage", "CrmHrAssignmentsPage", "Project-linked assignment shell."),
            "/resources" => new TuningSurfaceDefinition("page-resourcespage", "ResourcesPage", "Resource registry and connector editing."),
            "/prompt-gallery" => new TuningSurfaceDefinition("page-promptgallerypage", "PromptGalleryPage", "Prompt library, versions, and usage history."),
            "/prompt-factory" => new TuningSurfaceDefinition("page-promptfactorypage", "PromptFactoryPage", "Prompt assembly, flow templates, and provider sends."),
            "/validation" => new TuningSurfaceDefinition("page-validationcenterpage", "ValidationCenterPage", "Validation runs, findings, and review decisions."),
            "/test-lab" => new TuningSurfaceDefinition("page-testlabpage", "TestLabPage", "Test plans, evidence, and execution records."),
            "/activity" => new TuningSurfaceDefinition("page-activitypage", "ActivityPage", "Activity timeline and cross-entity search."),
            "/automation" => new TuningSurfaceDefinition("page-automationpage", "AutomationPage", "Background job visibility and diagnostics."),
            _ => null
        };
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

    private static string ResolveWorkspaceId(string path)
        => path.Trim().ToLowerInvariant() switch
        {
            "/validation" or "/test-lab" => "quality",
            "/automation" or "/activity" or "/settings" => "automation",
            _ => "delivery"
        };

    private static string ResolvePageGroup(string path)
        => path.Trim().ToLowerInvariant() switch
        {
            var normalized when normalized.StartsWith("/crm-hr", StringComparison.Ordinal) => "CRM / HR",
            "/validation" => "Validation",
            "/test-lab" => "Testing",
            "/settings" => "Settings",
            "/automation" or "/activity" => "Operations",
            _ => "Workspace"
        };

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

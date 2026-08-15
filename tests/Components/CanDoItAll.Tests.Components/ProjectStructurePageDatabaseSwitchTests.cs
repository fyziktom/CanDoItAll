using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.Modules.Workbench.Pages.Components.ProjectStructure;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructurePageDatabaseSwitchTests
{
    [Fact]
    public async Task Manager_summary_tab_query_selects_an_explicitly_lazy_report()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var projectId = await CreateProjectAsync(projectsService, "Lazy manager summary project");
        navigation.NavigateTo($"http://localhost/projects/{projectId:D}/structure?tab=manager-summary");

        var cut = harness.Context.Render<ProjectStructurePage>(parameters => parameters
            .Add(page => page.ProjectId, projectId));

        cut.WaitForElement("[data-testid='project-manager-summary']");
        AssertSelectedStructureTab(cut, "Manager Summary");
        Assert.NotNull(cut.Find("[data-testid='manager-summary-load']"));
        Assert.NotNull(cut.Find("[data-testid='manager-summary-empty']"));
        Assert.Empty(cut.FindAll("[data-testid='manager-summary-loading']"));
        Assert.Empty(cut.FindAll("[data-testid='manager-summary-metrics']"));
        Assert.Empty(cut.FindAll("[data-testid='manager-summary-activity-dialog']"));
    }

    [Fact]
    public async Task Manager_summary_snapshot_survives_server_rendered_tab_disposal()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var stateStore = harness.Context.Services.GetRequiredService<ProjectManagerSummaryStateStore>();
        var projectId = await CreateProjectAsync(projectsService, "Retained manager summary project");
        var options = new ProjectManagerSummaryOptions();
        stateStore.GetOrCreate(projectId).Snapshot = CreateManagerSummarySnapshot(
            projectId,
            "Retained manager summary project",
            options);
        navigation.NavigateTo($"http://localhost/projects/{projectId:D}/structure?tab=manager-summary");
        var cut = harness.Context.Render<ProjectStructurePage>(parameters => parameters
            .Add(page => page.ProjectId, projectId));

        cut.WaitForElement("[data-testid='manager-summary-metrics']");
        await cut.InvokeAsync(() => cut.FindAll(".cad-tabs__tab")
            .Single(tab => tab.TextContent.Contains("Canvas", StringComparison.Ordinal))
            .Click());
        cut.WaitForAssertion(() =>
            Assert.Empty(cut.FindAll("[data-testid='project-manager-summary']")));

        await cut.InvokeAsync(() => cut.FindAll(".cad-tabs__tab")
            .Single(tab => tab.TextContent.Contains("Manager Summary", StringComparison.Ordinal))
            .Click());

        cut.WaitForElement("[data-testid='manager-summary-metrics']");
        Assert.Contains("Loaded", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("[data-testid='manager-summary-open-warnings']"));
        Assert.Equal(
            options,
            stateStore.GetOrCreate(projectId).Snapshot?.Options);
    }

    [Fact]
    public async Task Manager_summary_recursive_scope_requires_confirmation_before_report_loading()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var dbContextFactory = harness.Context.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var projectId = await CreateProjectAsync(projectsService, "Large manager summary project");
        var descendantIds = Enumerable.Range(
                0,
                ProjectManagerSummaryScopePolicy.ConfirmationDescendantCount)
            .Select(_ => Guid.NewGuid())
            .ToArray();
        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            dbContext.Set<Project>().AddRange(descendantIds.Select((descendantId, index) => new Project
            {
                Id = descendantId,
                Name = $"Manager summary descendant {index + 1}",
                Slug = $"manager-summary-descendant-{descendantId:N}"
            }));
            dbContext.Set<ProjectHierarchyLink>().AddRange(descendantIds.Select(descendantId =>
                new ProjectHierarchyLink
                {
                    ParentProjectId = projectId,
                    ChildProjectId = descendantId
                }));
            await dbContext.SaveChangesAsync();
        }

        navigation.NavigateTo($"http://localhost/projects/{projectId:D}/structure?tab=manager-summary");
        var cut = harness.Context.Render<ProjectStructurePage>(parameters => parameters
            .Add(page => page.ProjectId, projectId));

        cut.WaitForElement("[data-testid='project-manager-summary']");
        await cut.InvokeAsync(() =>
        {
            cut.Find("[data-testid='manager-summary-project-scope']")
                .Change(ProjectManagerSummaryScope.ProjectAndDescendants.ToString());
            cut.Find("[data-testid='manager-summary-load']").Click();
        });

        cut.WaitForElement("[data-testid='manager-summary-large-scope-warning']");
        cut.WaitForAssertion(() =>
        {
            Assert.Contains(
                $"{descendantIds.Length:N0} subprojects",
                cut.Find("[data-testid='manager-summary-large-scope-warning']").TextContent,
                StringComparison.Ordinal);
            Assert.NotNull(cut.Find("[data-testid='manager-summary-large-scope-continue']"));
            Assert.Empty(cut.FindAll("[data-testid='manager-summary-metrics']"));
            Assert.Empty(cut.FindAll("[data-testid='manager-summary-loading']"));
        });
    }

    [Fact]
    public async Task Manager_summary_activity_dialog_is_created_only_after_explicit_open()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var stateStore = harness.Context.Services.GetRequiredService<ProjectManagerSummaryStateStore>();
        var projectId = await CreateProjectAsync(projectsService, "Lazy activity dialog project");
        stateStore.GetOrCreate(projectId).Snapshot = CreateManagerSummarySnapshot(
            projectId,
            "Lazy activity dialog project",
            new ProjectManagerSummaryOptions());
        navigation.NavigateTo($"http://localhost/projects/{projectId:D}/structure?tab=manager-summary");
        var cut = harness.Context.Render<ProjectStructurePage>(parameters => parameters
            .Add(page => page.ProjectId, projectId));

        var openActivityButton = cut.WaitForElement("[data-testid='manager-summary-open-activity']");
        Assert.Equal("Open all activity", openActivityButton.GetAttribute("aria-label"));
        Assert.Equal("Open all activity", openActivityButton.GetAttribute("title"));
        Assert.DoesNotContain(
            "Open all activity",
            openActivityButton.TextContent,
            StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("[data-testid='manager-summary-activity-dialog']"));

        await cut.InvokeAsync(() =>
            cut.Find("[data-testid='manager-summary-open-activity']").Click());

        cut.WaitForElement("[data-testid='manager-summary-activity-dialog']");
        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll("[data-testid='manager-activity-loading']"));
            Assert.NotNull(cut.Find("[data-testid='manager-activity-previous']"));
            Assert.NotNull(cut.Find("[data-testid='manager-activity-next']"));
            var activityKind = cut.Find("[data-testid='manager-activity-kind']");
            Assert.Contains("Agents", activityKind.TextContent, StringComparison.Ordinal);
            Assert.DoesNotContain(
                "Conversations",
                activityKind.TextContent,
                StringComparison.Ordinal);
            Assert.Single(
                cut.FindAll("button"),
                button =>
                    string.Equals(
                        button.GetAttribute("aria-label"),
                        "Close",
                        StringComparison.Ordinal) ||
                    button.TextContent.Contains("Close", StringComparison.Ordinal));
        });
    }

    [Fact]
    public async Task Manager_summary_warnings_are_disclosed_only_on_demand()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var stateStore = harness.Context.Services.GetRequiredService<ProjectManagerSummaryStateStore>();
        var projectId = await CreateProjectAsync(projectsService, "Manager summary warning project");
        var snapshot = CreateManagerSummarySnapshot(
            projectId,
            "Manager summary warning project",
            new ProjectManagerSummaryOptions()) with
        {
            OtherCurrencyFutureCosts =
            [
                new ProjectManagerCurrencyCostTotal("EUR", 125m, 2)
            ],
            Warnings =
            [
                "Historical workforce costs are not available.",
                "The forecast uses planned completion dates."
            ]
        };
        stateStore.GetOrCreate(projectId).Snapshot = snapshot;
        navigation.NavigateTo($"http://localhost/projects/{projectId:D}/structure?tab=manager-summary");
        var cut = harness.Context.Render<ProjectStructurePage>(parameters => parameters
            .Add(page => page.ProjectId, projectId));

        var openWarningsButton = cut.WaitForElement("[data-testid='manager-summary-open-warnings']");
        Assert.Equal(
            "Open report notes and warnings",
            openWarningsButton.GetAttribute("aria-label"));
        Assert.Empty(cut.FindAll("[data-testid='manager-summary-warnings-dialog']"));
        Assert.DoesNotContain(snapshot.Warnings[0], cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(snapshot.Warnings[1], cut.Markup, StringComparison.Ordinal);

        await cut.InvokeAsync(() => openWarningsButton.Click());

        var dialog = cut.WaitForElement("[data-testid='manager-summary-warnings-dialog']");
        var warningItems = cut.FindAll("[data-testid='manager-summary-warning-item']");
        Assert.Equal(2, warningItems.Count);
        Assert.Contains(snapshot.Warnings[0], dialog.TextContent, StringComparison.Ordinal);
        Assert.Contains(snapshot.Warnings[1], dialog.TextContent, StringComparison.Ordinal);
        Assert.Contains("125", dialog.TextContent, StringComparison.Ordinal);
        Assert.Contains("EUR", dialog.TextContent, StringComparison.Ordinal);

        await cut.InvokeAsync(() =>
            dialog.QuerySelector("button[aria-label='Close']")!.Click());

        cut.WaitForAssertion(() =>
            Assert.Empty(cut.FindAll("[data-testid='manager-summary-warnings-dialog']")));
    }

    [Fact]
    public async Task Gantt_tab_query_selects_the_gantt_view_on_initial_load()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.AddScoped<ProjectStructureGanttTaskEditCoordinator>());
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var projectId = await CreateProjectAsync(projectsService, "Query-selected Gantt project");
        navigation.NavigateTo($"http://localhost/projects/{projectId:D}/structure?tab=gantt");

        var cut = harness.Context.Render<ProjectStructurePage>(parameters => parameters
            .Add(page => page.ProjectId, projectId));

        cut.WaitForElement("[data-testid='project-structure-gantt-panel']");
        cut.WaitForAssertion(() =>
        {
            var selectedTab = Assert.Single(cut.FindAll(".cad-tabs__tab[aria-selected='true']"));
            Assert.Contains("Gantt", selectedTab.TextContent, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Reapplying_the_same_navigation_identity_preserves_a_manual_tab_choice()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.AddScoped<ProjectStructureGanttTaskEditCoordinator>());
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var projectId = await CreateProjectAsync(projectsService, "Manual tab project");
        navigation.NavigateTo($"http://localhost/projects/{projectId:D}/structure?tab=gantt");
        var cut = harness.Context.Render<ProjectStructurePage>(parameters => parameters
            .Add(page => page.ProjectId, projectId));

        cut.WaitForElement("[data-testid='project-structure-gantt-panel']");
        await cut.InvokeAsync(() => cut.FindAll(".cad-tabs__tab")
            .Single(tab => tab.TextContent.Contains("Canvas", StringComparison.Ordinal))
            .Click());

        cut.Render(parameters => parameters.Add(page => page.ProjectId, projectId));

        AssertSelectedStructureTab(cut, "Canvas");
        Assert.Empty(cut.FindAll("[data-testid='project-structure-gantt-panel']"));
    }

    [Fact]
    public async Task Changing_project_identity_reapplies_the_requested_gantt_tab()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.AddScoped<ProjectStructureGanttTaskEditCoordinator>());
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var firstProjectId = await CreateProjectAsync(projectsService, "First query identity project");
        var secondProjectId = await CreateProjectAsync(projectsService, "Second query identity project");
        navigation.NavigateTo($"http://localhost/projects/{firstProjectId:D}/structure?tab=gantt");
        var cut = harness.Context.Render<ProjectStructurePage>(parameters => parameters
            .Add(page => page.ProjectId, firstProjectId));

        cut.WaitForElement("[data-testid='project-structure-gantt-panel']");
        await cut.InvokeAsync(() => cut.FindAll(".cad-tabs__tab")
            .Single(tab => tab.TextContent.Contains("Canvas", StringComparison.Ordinal))
            .Click());

        navigation.NavigateTo($"http://localhost/projects/{secondProjectId:D}/structure?tab=gantt");
        cut.Render(parameters => parameters.Add(page => page.ProjectId, secondProjectId));

        cut.WaitForElement("[data-testid='project-structure-gantt-panel']");
        AssertSelectedStructureTab(cut, "Gantt");
    }

    [Fact]
    public async Task Missing_project_routes_render_a_safe_structure_recovery_state()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var registry = harness.Context.Services.GetRequiredService<IAgentChatContextRegistry>();
        var missingProjectId = Guid.NewGuid();

        var cut = harness.Context.Render<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, missingProjectId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Project structure unavailable", cut.Markup);
            Assert.Contains("does not exist in the active database profile", cut.Markup);
            var provider = cut.FindComponent<ProjectStructureAgentChatContextProvider>();
            Assert.Equal(missingProjectId, provider.Instance.ProjectId);
            Assert.Equal(
                AgentChatContextAccessState.Failed,
                Assert.IsType<AgentChatContextSnapshot>(registry.Capture()).Scope.AccessState);
        });

        await Assert.ThrowsAsync<AgentChatContextUnavailableException>(
            async () => await registry.CaptureAsync());

        cut.FindAll("button")
            .First(button => button.TextContent.Contains("Open projects", StringComparison.Ordinal))
            .Click();

        cut.WaitForAssertion(() =>
            Assert.EndsWith("/projects", navigation.Uri, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Changing_projects_resets_to_canvas_and_keeps_gantt_uninitialized_until_selected()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.AddScoped<ProjectStructureGanttTaskEditCoordinator>());
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var firstProjectId = await CreateProjectAsync(projectsService, "First lazy Gantt project");
        var secondProjectId = await CreateProjectAsync(projectsService, "Second lazy Gantt project");

        var cut = harness.Context.Render<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, firstProjectId));

        cut.WaitForElement(".cad-tabs__tab");
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='project-structure-gantt-panel']")));
        cut.WaitForAssertion(() =>
        {
            cut.FindAll(".cad-tabs__tab")
                .Single(tab => tab.TextContent.Contains("Gantt", StringComparison.Ordinal))
                .Click();
        });
        cut.WaitForElement("[data-testid='project-structure-gantt-panel']");

        cut.Render(parameters => parameters.Add(page => page.ProjectId, secondProjectId));

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll("[data-testid='project-structure-gantt-panel']"));
            var selectedTab = Assert.Single(cut.FindAll(".cad-tabs__tab[aria-selected='true']"));
            Assert.Contains("Canvas", selectedTab.TextContent, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Late_previous_project_reload_cannot_replace_the_current_project_surface()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(RegisterDelayedDbContextFactory);
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var firstProjectId = await CreateProjectAsync(projectsService, "First delayed project");
        var secondProjectId = await CreateProjectAsync(projectsService, "Second current project");
        var delayedFactory = harness.Context.Services.GetRequiredService<DelayedFirstDbContextFactory>();
        delayedFactory.Arm();
        var cut = harness.Context.Render<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, firstProjectId));

        await delayedFactory.WaitForFirstDelayedRequestAsync();
        cut.Render(parameters => parameters.Add(page => page.ProjectId, secondProjectId));

        try
        {
            AssertCurrentProjectContext(cut, secondProjectId, "Second current project");
        }
        finally
        {
            delayedFactory.ReleaseFirstDelayedRequest();
        }

        await Task.Yield();
        AssertCurrentProjectContext(cut, secondProjectId, "Second current project");
    }

    [Fact]
    public async Task Context_fence_uses_the_actual_route_identity_for_casing_and_trailing_slash_variants()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var projectId = await CreateProjectAsync(projectsService, "Route identity project");
        navigation.NavigateTo($"/Projects/{projectId:D}/Structure/");

        var cut = harness.Context.Render<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() =>
        {
            var provider = cut.FindComponent<ProjectStructureAgentChatContextProvider>();
            Assert.Equal(
                AgentChatNavigationIdentity.CreateForLocation(navigation.BaseUri, navigation.Uri),
                provider.Instance.ContextNavigationIdentity);
        });
    }

    private static void AssertCurrentProjectContext(
        IRenderedComponent<ProjectStructurePage> cut,
        Guid expectedProjectId,
        string expectedProjectName)
    {
        cut.WaitForAssertion(() =>
        {
            var contextProvider = cut.FindComponent<ProjectStructureAgentChatContextProvider>();
            Assert.Equal(expectedProjectId, contextProvider.Instance.ProjectId);
            Assert.Equal(expectedProjectName, contextProvider.Instance.ProjectName);
            var canvasLoadedIndicator = cut.Find("[data-testid='project-structure-canvas-loaded']");
            Assert.Contains("Nodes", canvasLoadedIndicator.TextContent, StringComparison.Ordinal);
            Assert.Contains("Selection", canvasLoadedIndicator.TextContent, StringComparison.Ordinal);
            Assert.DoesNotContain("Links", canvasLoadedIndicator.TextContent, StringComparison.Ordinal);
            Assert.DoesNotContain($"{expectedProjectName} workbench", cut.Markup, StringComparison.Ordinal);
        });
    }

    private static void AssertSelectedStructureTab(
        IRenderedComponent<ProjectStructurePage> cut,
        string expectedTab)
    {
        cut.WaitForAssertion(() =>
        {
            var selectedTab = Assert.Single(cut.FindAll(".cad-tabs__tab[aria-selected='true']"));
            Assert.Contains(expectedTab, selectedTab.TextContent, StringComparison.Ordinal);
        });
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projectsService, string name)
    {
        var project = await projectsService.GetAsync(null);
        project.Name = name;
        project.Description = $"{name} description";
        project.Objective = $"{name} objective";
        project.CurrentPhase = "Execution";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        return saveResult.Value;
    }

    private static ProjectManagerSummarySnapshot CreateManagerSummarySnapshot(
        Guid projectId,
        string projectName,
        ProjectManagerSummaryOptions options)
    {
        var generatedAtUtc = new DateTimeOffset(2026, 7, 28, 12, 0, 0, TimeSpan.Zero);
        var scope = new ProjectManagerSummaryScopeResolution(
            projectId,
            projectName,
            options.Scope,
            [projectId],
            DescendantCount: 0,
            RequiresConfirmation: false);
        return new ProjectManagerSummarySnapshot(
            projectId,
            projectName,
            options,
            scope,
            generatedAtUtc.AddMonths(-1),
            generatedAtUtc,
            generatedAtUtc,
            new ProjectManagerTaskSchedule(
                2,
                generatedAtUtc.AddDays(-2),
                generatedAtUtc.AddDays(2),
                96m,
                24m),
            new ProjectManagerCostTotals(1.25m, 0.5m, 0m, 0),
            Enum.GetValues<ProjectManagerCostCategory>()
                .Select(category => new ProjectManagerCostBreakdown(
                    category,
                    category == ProjectManagerCostCategory.ChatsAndAgents ? 1.25m : 0m,
                    category == ProjectManagerCostCategory.Processes ? 0.5m : 0m,
                    FuturePlannedUsd: 0m,
                    UnknownHistoricalCostCount: 0))
                .ToArray(),
            [],
            [],
            [],
            []);
    }

    private static void RegisterDelayedDbContextFactory(IServiceCollection services)
    {
        var descriptor = services.Last(candidate =>
            candidate.ServiceType == typeof(IDbContextFactory<AppDbContext>));
        services.Remove(descriptor);
        services.Add(new ServiceDescriptor(
            typeof(DelayedFirstDbContextFactory),
            serviceProvider => new DelayedFirstDbContextFactory(
                (IDbContextFactory<AppDbContext>)CreateService(serviceProvider, descriptor)),
            descriptor.Lifetime));
        services.Add(new ServiceDescriptor(
            typeof(IDbContextFactory<AppDbContext>),
            serviceProvider => serviceProvider.GetRequiredService<DelayedFirstDbContextFactory>(),
            descriptor.Lifetime));
    }

    private static object CreateService(IServiceProvider serviceProvider, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is not null)
        {
            return descriptor.ImplementationInstance;
        }

        if (descriptor.ImplementationFactory is not null)
        {
            return descriptor.ImplementationFactory(serviceProvider);
        }

        if (descriptor.ImplementationType is not null)
        {
            return ActivatorUtilities.GetServiceOrCreateInstance(
                serviceProvider,
                descriptor.ImplementationType);
        }

        throw new InvalidOperationException(
            $"Service descriptor for '{descriptor.ServiceType}' does not expose an implementation.");
    }

    private sealed class DelayedFirstDbContextFactory(
        IDbContextFactory<AppDbContext> innerFactory) : IDbContextFactory<AppDbContext>
    {
        private readonly TaskCompletionSource firstDelayedRequest = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource releaseFirstDelayedRequest = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int armed;
        private int delayedRequestCount;

        public void Arm()
            => Interlocked.Exchange(ref armed, 1);

        public Task WaitForFirstDelayedRequestAsync()
            => firstDelayedRequest.Task.WaitAsync(TimeSpan.FromSeconds(10));

        public void ReleaseFirstDelayedRequest()
            => releaseFirstDelayedRequest.TrySetResult();

        public AppDbContext CreateDbContext()
            => innerFactory.CreateDbContext();

        public async Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            if (Volatile.Read(ref armed) != 0 &&
                Interlocked.Increment(ref delayedRequestCount) == 1)
            {
                firstDelayedRequest.TrySetResult();
                await releaseFirstDelayedRequest.Task.WaitAsync(cancellationToken);
            }

            return await innerFactory.CreateDbContextAsync(cancellationToken);
        }
    }
}

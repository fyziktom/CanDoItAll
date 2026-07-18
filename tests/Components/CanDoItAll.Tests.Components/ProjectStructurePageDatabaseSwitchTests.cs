using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.Modules.Workbench.Pages.Components.ProjectStructure;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructurePageDatabaseSwitchTests
{
    [Fact]
    public async Task Missing_project_routes_render_a_safe_structure_recovery_state()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var registry = harness.Context.Services.GetRequiredService<IAgentChatContextRegistry>();
        var missingProjectId = Guid.NewGuid();

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
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

        Assert.EndsWith("/projects", navigation.Uri, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Changing_projects_resets_to_canvas_and_keeps_gantt_uninitialized_until_selected()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.AddScoped<ProjectStructureGanttTaskEditCoordinator>());
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var firstProjectId = await CreateProjectAsync(projectsService, "First lazy Gantt project");
        var secondProjectId = await CreateProjectAsync(projectsService, "Second lazy Gantt project");

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
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

        cut.SetParametersAndRender(parameters => parameters.Add(page => page.ProjectId, secondProjectId));

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
        var referenceDataProvider = new DelayedFirstImageProviderReferenceDataProvider();
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
        {
            services.RemoveAll<IAgentReferenceDataProvider>();
            services.AddSingleton<IAgentReferenceDataProvider>(referenceDataProvider);
        });
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var firstProjectId = await CreateProjectAsync(projectsService, "First delayed project");
        var secondProjectId = await CreateProjectAsync(projectsService, "Second current project");
        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, firstProjectId));

        await referenceDataProvider.WaitForFirstImageProviderRequestAsync();
        cut.SetParametersAndRender(parameters => parameters.Add(page => page.ProjectId, secondProjectId));

        try
        {
            AssertCurrentProjectContext(cut, secondProjectId, "Second current project");
        }
        finally
        {
            referenceDataProvider.ReleaseFirstImageProviderRequest();
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

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
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
            Assert.Contains($"{expectedProjectName} workbench", cut.Markup, StringComparison.Ordinal);
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

    private sealed class DelayedFirstImageProviderReferenceDataProvider : IAgentReferenceDataProvider
    {
        private readonly TaskCompletionSource firstImageProviderRequest = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource releaseFirstImageProviderRequest = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int imageProviderRequestCount;

        public Task WaitForFirstImageProviderRequestAsync()
            => firstImageProviderRequest.Task.WaitAsync(TimeSpan.FromSeconds(10));

        public void ReleaseFirstImageProviderRequest()
            => releaseFirstImageProviderRequest.TrySetResult();

        public async Task<AgentReferenceDataSnapshot> GetAsync(
            AgentReferenceDataRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request.ProviderPurpose == ProviderProfilePurpose.ImageGeneration &&
                Interlocked.Increment(ref imageProviderRequestCount) == 1)
            {
                firstImageProviderRequest.TrySetResult();
                await releaseFirstImageProviderRequest.Task.WaitAsync(cancellationToken);
            }

            return new AgentReferenceDataSnapshot(
                request.Sections,
                [],
                [],
                new Dictionary<Guid, ProviderProfile>(),
                DateTimeOffset.UtcNow,
                TimeSpan.Zero);
        }
    }
}

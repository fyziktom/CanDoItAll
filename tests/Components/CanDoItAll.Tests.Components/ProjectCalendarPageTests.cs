using System.Text.Json;
using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.Modules.Workbench.Pages.Components.ProjectStructure;
using CanDoItAll.Modules.Workbench.ProjectStructure;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectCalendarPageTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Page_renders_calendar_boundary_validation_cards()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var project = await projectsService.GetAsync(null);
        project.Name = "Calendar Test Project";
        project.Description = "Calendar preview coverage";
        project.Objective = "Render the extracted calendar boundaries";
        project.CurrentPhase = "Review";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        var start = new DateTimeOffset(2026, 3, 24, 14, 0, 0, TimeSpan.Zero);
        await workbenchService.SeedProjectObjectsAsync(
            projectId,
            [
                new ProjectObjectSeedRequest(
                    ProjectObjectType.Note,
                    "Weekly review",
                    "Calendar validation",
                    "Drive the Project Calendar preview cards.",
                    start,
                    start.AddHours(1))
            ]);

        var cut = harness.Context.RenderComponent<ProjectCalendarPage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Calendar boundary validation", cut.Markup);
            Assert.Single(cut.FindAll("[data-testid='calendar-selection-panel']"));
            Assert.Single(cut.FindAll("[data-testid='calendar-event-editor-modal']"));
            Assert.Single(cut.FindAll("[data-testid='calendar-crud-bridge']"));
            Assert.Single(cut.FindAll("[data-testid='calendar-mini-month-navigator']"));
            Assert.Single(cut.FindAll("[data-testid='calendar-export-menu']"));
            Assert.Single(cut.FindAll("[data-testid='project-calendar-state-parser']"));
            Assert.Single(cut.FindAll("[data-testid='calendar-time-grid-renderer']"));
        });
    }

    [Fact]
    public async Task Agent_context_tracks_the_exact_calendar_route_and_canonical_selected_node()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var registry = harness.Context.Services.GetRequiredService<IAgentChatContextRegistry>();
        var notificationHub = harness.Context.Services.GetRequiredService<IAgentChatExecutionNotificationHub>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var projectId = await CreateProjectAsync(projectsService, "Calendar context project");
        var start = new DateTimeOffset(2026, 7, 17, 14, 0, 0, TimeSpan.Zero);
        await workbenchService.SeedProjectObjectsAsync(
            projectId,
            [
                new ProjectObjectSeedRequest(
                    ProjectObjectType.Note,
                    "Machine overview",
                    "Calendar context A",
                    "First canonical calendar node.",
                    start,
                    start.AddHours(1)),
                new ProjectObjectSeedRequest(
                    ProjectObjectType.WorkItem,
                    "Machine details",
                    "Calendar context B",
                    "Second canonical calendar node.",
                    start.AddHours(2),
                    start.AddHours(3))
            ]);
        var calendarSurface = await workbenchService.GetCalendarAsync(projectId);
        var firstEvent = calendarSurface.Events[0];
        var secondEvent = calendarSurface.Events[1];
        navigation.NavigateTo($"/projects/{projectId:D}/calendar");

        var cut = harness.Context.RenderComponent<ProjectCalendarPage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() =>
        {
            var provider = cut.FindComponent<ProjectStructureAgentChatContextProvider>();
            Assert.Equal(ProjectStructureAgentChatView.Calendar, provider.Instance.ActiveView);
            Assert.Equal(
                AgentChatNavigationIdentity.CreateForLocation(navigation.BaseUri, navigation.Uri),
                provider.Instance.ContextNavigationIdentity);
            AssertCalendarContext(registry, projectId, firstEvent);
        });

        var calendar = cut.FindComponent<CanvasCalendar>();
        var secondCanvasEvent = Assert.Single(
            calendar.Instance.Surface.Events,
            item => string.Equals(item.EventId, secondEvent.Id.ToString("D"), StringComparison.Ordinal));
        await cut.InvokeAsync(() => calendar.Instance.OnSelectionChanged(
            JsonSerializer.Serialize(secondCanvasEvent, JsonOptions),
            "{}"));

        cut.WaitForAssertion(() => AssertCalendarContext(registry, projectId, secondEvent));

        Assert.Equal(1, await workbenchService.DeleteObjectAsync(projectId, secondEvent.NodeKey));
        var selectedSnapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
        await notificationHub.PublishAsync(new AgentChatExecutionCompleted(
            selectedSnapshot.Scope.Id,
            selectedSnapshot.Scope.Source,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow));

        cut.WaitForAssertion(() => AssertCalendarContext(registry, projectId, firstEvent));
    }

    private static void AssertCalendarContext(
        IAgentChatContextRegistry registry,
        Guid projectId,
        ProjectCalendarEvent expectedEvent)
    {
        var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
        Assert.Equal(AgentChatContextAccessState.Ready, snapshot.Scope.AccessState);
        Assert.Equal(ProjectStructureAgentChatContextBuilder.SourceKind, snapshot.Scope.Source.Kind.Value);
        Assert.Equal(projectId.ToString("D"), snapshot.Scope.Source.Id.Value);
        var position = Assert.IsType<AgentChatSurfacePosition>(snapshot.Scope.SurfacePosition);
        Assert.Equal("projects", position.Module);
        Assert.Equal("project-calendar", position.Surface);
        Assert.Equal("calendar", position.View);
        Assert.Equal($"/projects/{projectId:D}/calendar", position.Route);
        Assert.Equal(projectId.ToString("D"), position.PrimarySelection?.Id);
        var selectedNode = Assert.Single(position.SelectedEntities);
        Assert.Equal("project-node", selectedNode.Kind);
        Assert.Equal(expectedEvent.NodeKey, selectedNode.Id);
        Assert.Equal(expectedEvent.Title, selectedNode.DisplayName);
    }

    private static async Task<Guid> CreateProjectAsync(
        ProjectsService projectsService,
        string name)
    {
        var project = await projectsService.GetAsync(null);
        project.Name = name;
        project.Description = $"{name} description";
        project.Objective = $"{name} objective";
        project.CurrentPhase = "Execution";
        var result = await projectsService.SaveAsync(project);
        Assert.True(result.IsSuccess);
        return result.Value;
    }
}



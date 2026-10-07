using System.Text.Json;
using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProjectStructure;

public sealed class ProjectCalendarLifetimeTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Retired_calendar_state_callback_cannot_write_a_successor_project(bool returnToOriginal) {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbench = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var first = await CreateProjectAsync(projects, "First calendar");
        var second = await CreateProjectAsync(projects, "Second calendar");
        var cut = harness.Context.Render<ProjectCalendarPage>(parameters => parameters.Add(page => page.ProjectId, first));
        cut.WaitForAssertion(() => Assert.Single(cut.FindComponents<CanvasCalendar>()));
        var retiredCallback = cut.FindComponent<CanvasCalendar>().Instance.StateChanged;
        cut.Render(parameters => parameters.Add(page => page.ProjectId, second));
        cut.WaitForAssertion(() => Assert.Contains("Second calendar", cut.Markup));
        if (returnToOriginal) {
            cut.Render(parameters => parameters.Add(page => page.ProjectId, first));
            cut.WaitForAssertion(() => Assert.Contains("First calendar", cut.Markup));
        }

        var stateJson = JsonSerializer.Serialize(new { view = "year", selectedDate = "2027-04-01", timezone = "UTC" });
        await cut.InvokeAsync(() => retiredCallback.InvokeAsync(new CanvasCalendarStateChangedEventArgs(
            stateJson, null, "2027-04-01", "year", "year", "UTC")));

        Assert.Null((await workbench.GetCalendarAsync(first)).ViewStateJson);
        Assert.Null((await workbench.GetCalendarAsync(second)).ViewStateJson);
        Assert.DoesNotContain("2027-04-01", cut.FindComponent<CanvasCalendar>().Instance.Surface.SelectedDate);
    }

    [Fact]
    public async Task Repeated_parameters_do_not_reload_the_accepted_calendar() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var projectId = await CreateProjectAsync(projects, "Accepted calendar");
        var cut = harness.Context.Render<ProjectCalendarPage>(parameters => parameters.Add(page => page.ProjectId, projectId));
        cut.WaitForAssertion(() => Assert.Single(cut.FindComponents<CanvasCalendar>()));
        var changed = await projects.GetAsync(projectId);
        changed.Name = "Changed outside this view";
        Assert.True((await projects.SaveAsync(changed)).IsSuccess);

        await cut.InvokeAsync(() => cut.Instance.SetParametersAsync(ParameterView.FromDictionary(
            new Dictionary<string, object?> { [nameof(ProjectCalendarPage.ProjectId)] = projectId })));

        Assert.Contains("Accepted calendar", cut.Markup);
        Assert.DoesNotContain("Changed outside this view", cut.Markup);
    }

    [Fact]
    public async Task Production_calendar_is_read_only_and_has_no_synthetic_editor_facts() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var projectId = await CreateProjectAsync(projects, "Native calendar");
        var cut = harness.Context.Render<ProjectCalendarPage>(parameters => parameters.Add(page => page.ProjectId, projectId));
        cut.WaitForAssertion(() => Assert.Single(cut.FindComponents<CanvasCalendar>()));
        var surface = cut.FindComponent<CanvasCalendar>().Instance.Surface;
        Assert.False(surface.AllowCreate);
        Assert.False(surface.AllowEdit);
        Assert.False(surface.AllowDelete);
        Assert.False(surface.AllowDragDrop);
        Assert.False(surface.AllowResize);
        Assert.True(surface.EnableListExport);
        Assert.Empty(cut.FindAll("[data-testid='calendar-crud-bridge']"));
        Assert.Empty(cut.FindAll("[data-testid='calendar-event-editor-modal']"));
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projects, string name) {
        var project = await projects.GetAsync(null);
        project.Name = name;
        var result = await projects.SaveAsync(project);
        Assert.True(result.IsSuccess);
        return result.Value;
    }
}

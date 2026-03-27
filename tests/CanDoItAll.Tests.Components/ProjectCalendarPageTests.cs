using Bunit;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectCalendarPageTests
{
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
}



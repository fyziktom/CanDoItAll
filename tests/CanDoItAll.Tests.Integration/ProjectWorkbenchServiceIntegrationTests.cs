using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectWorkbenchServiceIntegrationTests
{
    [Fact]
    public async Task GetStructureAsync_builds_a_structure_surface_for_sqlite_projects()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();

        var saveResult = await projects.SaveAsync(new ProjectEditorModel
        {
            Name = "Workbench Structure Validation",
            Description = "Structure projection smoke test.",
            Objective = "Ensure structure surfaces load from SQLite.",
            CurrentPhase = "Discovery",
            Phases =
            [
                new ProjectPhaseEditorModel
                {
                    Name = "Discovery",
                    Goal = "Investigate flow stability.",
                    Status = ProjectPhaseStatus.Active,
                    StartDateUtc = new DateTime(2026, 3, 19),
                    EndDateUtc = new DateTime(2026, 3, 21)
                }
            ]
        });

        Assert.True(saveResult.IsSuccess);
        Assert.NotEqual(Guid.Empty, saveResult.Value);
        var surface = await workbench.GetStructureAsync(saveResult.Value);

        Assert.Equal("Workbench Structure Validation", surface.ProjectName);
        Assert.Contains(surface.Nodes, node => node.ObjectType == ProjectObjectType.ProjectRoot);
        Assert.Contains(surface.Nodes, node => node.ObjectType == ProjectObjectType.Phase && node.Title == "Discovery");
        Assert.Contains(surface.Links, link => link.Kind == ProjectObjectLinkKind.Contains);
    }

    [Fact]
    public async Task GetCalendarAsync_returns_phase_events_for_sqlite_projects()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();

        var saveResult = await projects.SaveAsync(new ProjectEditorModel
        {
            Name = "Workbench Calendar Validation",
            Description = "Calendar projection smoke test.",
            Objective = "Ensure calendar surfaces load from SQLite.",
            CurrentPhase = "Execution",
            Phases =
            [
                new ProjectPhaseEditorModel
                {
                    Name = "Execution",
                    Goal = "Deliver the repaired build.",
                    Status = ProjectPhaseStatus.Active,
                    StartDateUtc = new DateTime(2026, 3, 22),
                    EndDateUtc = new DateTime(2026, 3, 24)
                }
            ]
        });

        Assert.True(saveResult.IsSuccess);
        Assert.NotEqual(Guid.Empty, saveResult.Value);
        var surface = await workbench.GetCalendarAsync(saveResult.Value);

        var phaseEvent = Assert.Single(surface.Events);
        Assert.Equal("Execution", phaseEvent.Title);
        Assert.Equal(ProjectObjectType.Phase, phaseEvent.ObjectType);
    }
}

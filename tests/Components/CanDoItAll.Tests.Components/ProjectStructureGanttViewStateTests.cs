using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.CanvasAdapters;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructureGanttViewStateTests
{
    [Fact]
    public async Task Typed_view_state_round_trips_normalized_canonical_task_order()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var projectId = await CreateProjectAsync(projectsService, "Gantt row order persistence");
        var first = await CreateTaskAsync(workbenchService, projectId, "First", 100);
        var second = await CreateTaskAsync(workbenchService, projectId, "Second", 200);
        var third = await CreateTaskAsync(workbenchService, projectId, "Third", 300);

        await workbenchService.SaveGanttViewStateAsync(
            projectId,
            new ProjectStructureGanttViewState(
            [
                $" {third.Id} ",
                string.Empty,
                third.Id,
                first.Id,
                "stale-task"
            ]));

        var reloaded = await workbenchService.LoadGanttViewStateAsync(projectId);

        Assert.Equal(
            [third.Id, first.Id, second.Id],
            reloaded.OrderedTaskNodeIds);
    }

    [Fact]
    public async Task Insert_and_move_mutations_persist_complete_row_order()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var rowOrderService = harness.Context.Services.GetRequiredService<ProjectStructureGanttRowOrderService>();
        var projectId = await CreateProjectAsync(projectsService, "Gantt row order mutations");
        var first = await CreateTaskAsync(workbenchService, projectId, "First", 100);
        var second = await CreateTaskAsync(workbenchService, projectId, "Second", 200);
        var third = await CreateTaskAsync(workbenchService, projectId, "Third", 300);
        await workbenchService.SaveGanttViewStateAsync(
            projectId,
            new ProjectStructureGanttViewState([first.Id, second.Id, third.Id]));
        var inserted = await CreateTaskAsync(workbenchService, projectId, "Inserted", 400);

        var afterAppend = await rowOrderService.InsertAsync(
            projectId,
            inserted.Id,
            null,
            CreateAgent("append"));
        var afterInsert = await rowOrderService.InsertAsync(
            projectId,
            inserted.Id,
            second.Id,
            CreateAgent("insert"));
        var afterMove = await rowOrderService.MoveAsync(
            projectId,
            new ProjectStructureGanttRowMoveRequest(
                inserted.Id,
                second.Id,
                ProjectStructureGanttRowPlacement.Before),
            CreateAgent("move"));
        var reloaded = await workbenchService.LoadGanttViewStateAsync(projectId);

        Assert.Equal([first.Id, second.Id, third.Id, inserted.Id], afterAppend.OrderedTaskNodeIds);
        Assert.Equal([first.Id, second.Id, inserted.Id, third.Id], afterInsert.OrderedTaskNodeIds);
        Assert.Equal([first.Id, inserted.Id, second.Id, third.Id], afterMove.OrderedTaskNodeIds);
        Assert.Equal(afterMove.OrderedTaskNodeIds, reloaded.OrderedTaskNodeIds);
    }

    [Fact]
    public async Task Insert_rejects_missing_anchor_without_changing_persisted_order()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var rowOrderService = harness.Context.Services.GetRequiredService<ProjectStructureGanttRowOrderService>();
        var projectId = await CreateProjectAsync(projectsService, "Gantt row order validation");
        var first = await CreateTaskAsync(workbenchService, projectId, "First", 100);
        var second = await CreateTaskAsync(workbenchService, projectId, "Second", 200);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rowOrderService.InsertAsync(
                projectId,
                second.Id,
                "missing-anchor",
                CreateAgent("missing-anchor")));
        var reloaded = await workbenchService.LoadGanttViewStateAsync(projectId);

        Assert.Contains("missing-anchor", exception.Message, StringComparison.Ordinal);
        Assert.Equal([first.Id, second.Id], reloaded.OrderedTaskNodeIds);
    }

    [Fact]
    public async Task Parallel_moves_preserve_both_row_order_changes()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var rowOrderService = harness.Context.Services.GetRequiredService<ProjectStructureGanttRowOrderService>();
        var projectId = await CreateProjectAsync(projectsService, "Concurrent Gantt row moves");
        var first = await CreateTaskAsync(workbenchService, projectId, "First", 100);
        var second = await CreateTaskAsync(workbenchService, projectId, "Second", 200);
        var third = await CreateTaskAsync(workbenchService, projectId, "Third", 300);
        var fourth = await CreateTaskAsync(workbenchService, projectId, "Fourth", 400);
        await workbenchService.SaveGanttViewStateAsync(
            projectId,
            new ProjectStructureGanttViewState([first.Id, second.Id, third.Id, fourth.Id]));

        var moveFirstDown = rowOrderService.MoveAsync(
            projectId,
            new ProjectStructureGanttRowMoveRequest(
                first.Id,
                second.Id,
                ProjectStructureGanttRowPlacement.After),
            CreateAgent("first-down"));
        var moveFourthUp = rowOrderService.MoveAsync(
            projectId,
            new ProjectStructureGanttRowMoveRequest(
                fourth.Id,
                third.Id,
                ProjectStructureGanttRowPlacement.Before),
            CreateAgent("fourth-up"));

        await Task.WhenAll(moveFirstDown, moveFourthUp);
        var reloaded = await workbenchService.LoadGanttViewStateAsync(projectId);

        Assert.Equal([second.Id, first.Id, fourth.Id, third.Id], reloaded.OrderedTaskNodeIds);
    }

    [Fact]
    public async Task Stale_anchor_from_second_owner_is_rejected_without_changing_persisted_order()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var rowOrderService = harness.Context.Services.GetRequiredService<ProjectStructureGanttRowOrderService>();
        var projectId = await CreateProjectAsync(projectsService, "Stale Gantt row intent");
        var first = await CreateTaskAsync(workbenchService, projectId, "First", 100);
        var second = await CreateTaskAsync(workbenchService, projectId, "Second", 200);
        var third = await CreateTaskAsync(workbenchService, projectId, "Third", 300);
        await workbenchService.SaveGanttViewStateAsync(
            projectId,
            new ProjectStructureGanttViewState([first.Id, second.Id, third.Id]));

        var firstCircuitResult = await rowOrderService.MoveAsync(
            projectId,
            new ProjectStructureGanttRowMoveRequest(
                second.Id,
                third.Id,
                ProjectStructureGanttRowPlacement.After),
            CreateAgent("first-circuit"));
        var staleRequest = new ProjectStructureGanttRowMoveRequest(
            second.Id,
            first.Id,
            ProjectStructureGanttRowPlacement.Before);

        var exception = await Assert.ThrowsAsync<ProjectStructureGanttRowOrderConflictException>(() =>
            rowOrderService.MoveAsync(
                projectId,
                staleRequest,
                CreateAgent("second-circuit")));
        var reloaded = await workbenchService.LoadGanttViewStateAsync(projectId);

        Assert.Equal(second.Id, exception.TaskNodeId);
        Assert.Equal(first.Id, exception.AnchorTaskNodeId);
        Assert.Equal(ProjectStructureGanttRowPlacement.Before, exception.Placement);
        Assert.Equal([first.Id, third.Id, second.Id], firstCircuitResult.OrderedTaskNodeIds);
        Assert.Equal(firstCircuitResult.OrderedTaskNodeIds, reloaded.OrderedTaskNodeIds);
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

    private static Task<ProjectStructureNode> CreateTaskAsync(
        ProjectWorkbenchService workbenchService,
        Guid projectId,
        string title,
        double y)
    {
        return workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                title,
                string.Empty,
                string.Empty,
                ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(projectId),
                100,
                y,
                ObjectSubtype: "task"));
    }

    private static ProjectStructureAgentContext CreateAgent(string owner)
        => new(
            $"gantt-row-tests-{owner}",
            "Gantt row tests",
            Environment.MachineName,
            AppContext.BaseDirectory,
            string.Empty,
            $"gantt-row-tests-{owner}");
}

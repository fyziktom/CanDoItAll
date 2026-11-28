using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.CanvasAdapters;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProjectStructure;

public sealed class ProjectStructureNodeInitialSignalsTests {
    [Fact]
    public void Invalid_priority_progress_and_marker_presets_are_rejected_before_a_create() {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProjectNodeInitialSignals(7));
        Assert.Throws<ArgumentException>(() => ProjectNodeInitialProgress.FromPreset("progress", -1));
        Assert.Throws<ArgumentException>(() => ProjectNodeInitialProgress.FromPreset("complete", 20));
        Assert.Throws<ArgumentException>(() => new ProjectNodeInitialSignals(Markers: [new("", "sky", "Missing icon")]));
        Assert.Throws<ArgumentException>(() => new ProjectNodeInitialSignals(Markers: [new("Alert", "danger", "!"), new("alert", "danger", "!")]));
    }

    [Theory]
    [InlineData(null, 0, "progress", 0)]
    [InlineData("progress", 0, "progress", 0)]
    [InlineData("progress", 40, "progress", 40)]
    [InlineData("complete", 100, "complete", 100)]
    [InlineData("started", 0, "started", 0)]
    [InlineData("na", 0, "na", 0)]
    public async Task Create_persists_the_selected_signals_on_the_first_write(
        string? mode, int percent, string expectedMode, int expectedPercent) {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var services = harness.Context.Services;
        var project = (await services.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = "Node initial signals" })).Value;
        var owner = services.GetRequiredService<ProjectWorkbenchService>();
        ProjectNodeMarker[] markers = [new("alert", "danger", "Exclamation"), new("question", "sky", "Question")];
        var signals = mode is null ? null : new ProjectNodeInitialSignals(2, ProjectNodeInitialProgress.FromPreset(mode, percent), markers);
        var node = await owner.CreateObjectAsync(project, new(ProjectObjectType.Note, "Planning note", "", "", $"project:{project:D}", Status: "Ready for planning") {
            InitialSignals = signals
        });

        var surface = await owner.GetStructureAsync(project);
        var stored = surface.Nodes.Single(item => item.Id == node.Id);
        Assert.Equal("Ready for planning", stored.Status);
        Assert.Equal((expectedMode, expectedPercent), (stored.ProgressMode, stored.ProgressPercent));
        Assert.Equal(signals?.Priority ?? 0, stored.Priority);
        Assert.Equal(mode is null ? Array.Empty<ProjectNodeMarker>() : markers, stored.Markers);
        Assert.Equal(mode is null ? "" : "question", stored.MarkerIcon);
        var displayed = new ProjectStructureGraphAdapter().BuildSurface(surface, new(), new(), new()).Nodes.Single(item => item.Id == node.Id);
        Assert.Equal((expectedMode, expectedPercent), (displayed.ProgressMode, displayed.ProgressPercent));
    }

    [Fact]
    public async Task Canonical_task_canvas_progress_keeps_its_actual_execution_and_schedule_independent() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var services = harness.Context.Services;
        var project = (await services.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = "Task initial signals" })).Value;
        var owner = services.GetRequiredService<ProjectWorkbenchService>();
        var node = await owner.CreateObjectAsync(project, new(ProjectObjectType.WorkItem, "Review plan", "", "", $"project:{project:D}",
            ObjectSubtype: ProjectObjectSubtypePolicy.Task) {
            InitialSignals = new(1, ProjectNodeInitialProgress.FromPreset("complete", 100), [new("question", "sky", "Question")])
        });

        var surface = await owner.GetStructureAsync(project);
        var stored = surface.Nodes.Single(item => item.Id == node.Id);
        Assert.Equal(("complete", 100, 1, "question"), (stored.ProgressMode, stored.ProgressPercent, stored.Priority, stored.MarkerIcon));
        var execution = ProjectObjectMetadataSerializer.Parse(stored.MetadataJson).WorkItem!;
        Assert.Equal(ProjectTaskExecutionState.NotStarted, execution.ExecutionState);
        Assert.Null(execution.ActualStartedAtUtc);
        Assert.Null(execution.ActualEndedAtUtc);
        var gantt = new ProjectStructureGanttProjectionAdapter().Build(surface, [], new(DateTimeOffset.UnixEpoch, TimeSpan.FromHours(1)));
        Assert.True(gantt.IsValid);
        Assert.Equal(0, gantt.Tasks.Single(task => task.Id.Value == node.Id).ProgressPercent);
    }
}

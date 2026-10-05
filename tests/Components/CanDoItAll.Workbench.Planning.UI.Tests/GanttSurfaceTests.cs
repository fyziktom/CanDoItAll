using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Gantt;
using CanDoItAll.Components.Mermaid;
using CanDoItAll.Workbench.Planning.UI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.WorkbenchPlanning;

public sealed class GanttSurfaceTests {
    [Fact]
    public async Task Queued_title_intent_keeps_original_origin_and_receiver() {
        using var context = CreateContext();
        var original = Presentation();
        var first = new List<GanttIntent<GanttTaskTitleChangeRequest>>();
        var second = new List<GanttIntent<GanttTaskTitleChangeRequest>>();
        var cut = context.Render<PlanningGanttSurface>(parameters => parameters
            .Add(component => component.Presentation, original)
            .Add(component => component.TitleChanged, (GanttIntent<GanttTaskTitleChangeRequest> intent) => first.Add(intent)));
        var queued = cut.FindComponent<GanttChart>().Instance.TaskTitleChangeRequested;
        var replacement = Presentation();
        cut.Render(parameters => parameters
            .Add(component => component.Presentation, replacement)
            .Add(component => component.TitleChanged, (GanttIntent<GanttTaskTitleChangeRequest> intent) => second.Add(intent)));
        var request = new GanttTaskTitleChangeRequest(original.Tasks[0].Id, "First", "Changed");
        await cut.InvokeAsync(() => queued.InvokeAsync(request));
        Assert.Equal(new(original.Origin, request), Assert.Single(first));
        Assert.Empty(second);
        Assert.Same(replacement.Tasks, cut.FindComponent<GanttChart>().Instance.Tasks);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void All_real_chart_gestures_follow_host_capability(bool canMutate) {
        using var context = CreateContext();
        var accepted = Presentation() with { CanMutate = canMutate };
        var cut = context.Render<PlanningGanttSurface>(parameters => parameters.Add(component => component.Presentation, accepted));
        var chart = cut.FindComponent<GanttChart>().Instance;
        Assert.Equal(canMutate, chart.AllowTaskEditing);
        Assert.Equal(canMutate, chart.AllowDependencyEditing);
        Assert.Equal(canMutate, chart.AllowTaskInsertion);
        Assert.Equal(canMutate, chart.AllowTaskReordering);
        Assert.Equal(canMutate, chart.AllowTimelineTaskCreation);
        Assert.True(chart.TaskTitleChangeRequested.HasDelegate);
        Assert.True(chart.TaskScheduleChangeRequested.HasDelegate);
        Assert.True(chart.DependencyMutationRequested.HasDelegate);
        Assert.True(chart.TaskInsertionRequested.HasDelegate);
        Assert.True(chart.TaskOrderChangeRequested.HasDelegate);
        Assert.True(chart.TaskDoubleClicked.HasDelegate);
        Assert.True(chart.TimelineDoubleClicked.HasDelegate);
        Assert.Equal(!canMutate, cut.FindComponent<GanttTaskDragSource>().Instance.Disabled);
    }

    [Fact]
    public void Export_preview_uses_one_frozen_title_source_and_counts() {
        using var context = CreateContext();
        var snapshot = new GanttPreview(Guid.NewGuid(), "Original title", "gantt\n    title Original title", 4, 3);
        var cut = context.Render<PlanningGanttSurface>(parameters => parameters.Add(component => component.Presentation,
            Presentation() with { ProjectName = "New title", Preview = snapshot }));
        var dialog = cut.Find("[data-testid='project-structure-gantt-mermaid-dialog']");
        Assert.Contains("Original title schedule", dialog.TextContent);
        Assert.DoesNotContain("New title", dialog.TextContent);
        var diagram = cut.FindComponent<MermaidDiagram>().Instance;
        Assert.Equal(snapshot.Source, diagram.Source);
        Assert.Equal("strict", diagram.Options.SecurityLevel);
        Assert.False(diagram.Options.HtmlLabels);
        var download = cut.Find("[data-testid='project-structure-gantt-download-mermaid']");
        Assert.Equal(snapshot.Source, Uri.UnescapeDataString(download.GetAttribute("href")!.Split(',', 2)[1]));
        Assert.Equal(snapshot.Source, cut.FindComponent<CopyButton>().Instance.Value);
    }

    [Fact]
    public void Loading_retains_the_chart_without_authorizing_edits() {
        using var context = CreateContext();
        var cut = context.Render<PlanningGanttSurface>(parameters => parameters.Add(component => component.Presentation, Presentation()));
        var chart = cut.FindComponent<GanttChart>().Instance;
        cut.Render(parameters => parameters.Add(component => component.Presentation, Presentation() with { IsLoading = true }));
        Assert.Same(chart, cut.FindComponent<GanttChart>().Instance);
        Assert.False(chart.AllowTaskEditing);
    }

    [Fact]
    public void Invalid_projection_shows_issues_and_never_mounts_an_editable_chart() {
        using var context = CreateContext();
        var cut = context.Render<PlanningGanttSurface>(parameters => parameters.Add(component => component.Presentation,
            Presentation() with { Errors = ["Dependency cycle"], Warnings = ["Multiple original assignees"] }));
        Assert.Contains("Dependency cycle", cut.Markup);
        Assert.Contains("Multiple original assignees", cut.Markup);
        Assert.Empty(cut.FindComponents<GanttChart>());
    }

    [Fact]
    public async Task Two_gantt_views_do_not_share_intent_receivers_or_task_occurrences() {
        using var context = CreateContext();
        var first = Presentation();
        var second = Presentation();
        var firstCalls = new List<GanttIntent<GanttTaskId>>();
        var secondCalls = new List<GanttIntent<GanttTaskId>>();
        var a = context.Render<PlanningGanttSurface>(parameters => parameters
            .Add(component => component.Presentation, first)
            .Add(component => component.TaskOpened, (GanttIntent<GanttTaskId> intent) => firstCalls.Add(intent)));
        var b = context.Render<PlanningGanttSurface>(parameters => parameters
            .Add(component => component.Presentation, second)
            .Add(component => component.TaskOpened, (GanttIntent<GanttTaskId> intent) => secondCalls.Add(intent)));
        await a.InvokeAsync(() => a.FindComponent<GanttChart>().Instance.TaskDoubleClicked.InvokeAsync(first.Tasks[0].Id));
        Assert.Equal(new(first.Origin, first.Tasks[0].Id), Assert.Single(firstCalls));
        Assert.Empty(secondCalls);
        Assert.Same(second.Tasks, b.FindComponent<GanttChart>().Instance.Tasks);
    }

    [Fact]
    public async Task Queued_removal_retains_the_displayed_dependency_occurrence() {
        using var context = CreateContext();
        var original = Presentation();
        var intents = new List<GanttIntent<GanttDependencyMutationRequest>>();
        var cut = context.Render<PlanningGanttSurface>(parameters => parameters
            .Add(component => component.Presentation, original)
            .Add(component => component.DependencyChanged, (GanttIntent<GanttDependencyMutationRequest> intent) => intents.Add(intent)));
        cut.FindComponents<Button>().Single(button => button.Instance.Text == "Dependencies").Find("button").Click();
        var queued = cut.FindComponents<Button>().Single(button => button.Instance.Text == "Remove dependency").Instance.Click;
        cut.Render(parameters => parameters.Add(component => component.Presentation, Presentation() with { Dependencies = [] }));
        await cut.InvokeAsync(() => queued.InvokeAsync());
        var intent = Assert.Single(intents);
        Assert.Equal(original.Origin, intent.Origin);
        Assert.Equal(GanttDependencyMutationKind.Remove, intent.Value.Mutation);
        Assert.Equal(original.Dependencies[0], intent.Value.PreviousDependency);
        Assert.Null(intent.Value.ProposedDependency);
        Assert.Empty(intent.Value.AffectedTasks);
    }

    [Fact]
    public void An_open_dependency_list_cannot_render_broken_successor_facts() {
        using var context = CreateContext();
        var cut = context.Render<PlanningGanttSurface>(parameters => parameters.Add(component => component.Presentation, Presentation()));
        cut.FindComponents<Button>().Single(button => button.Instance.Text == "Dependencies").Find("button").Click();
        cut.Render(parameters => parameters.Add(component => component.Presentation, Presentation() with {
            Tasks = [], Errors = ["A dependency refers to a missing task"]
        }));
        Assert.Contains("A dependency refers to a missing task", cut.Markup);
        Assert.Empty(cut.FindComponents<GanttChart>());
        Assert.DoesNotContain(cut.FindComponents<Button>(), button => button.Instance.Text == "Remove dependency");
        Assert.True(cut.FindComponents<Button>().Single(button => button.Instance.Text == "Add task").Instance.Disabled);
    }

    private static GanttPresentation Presentation() {
        var start = new DateTimeOffset(2026, 10, 5, 8, 0, 0, TimeSpan.Zero);
        return new(Guid.NewGuid()) {
            HasProjection = true,
            CanMutate = true,
            ProjectName = "Independent Gantt",
            Tasks = [new(new("first"), "First", start, start.AddHours(8)), new(new("second"), "Second", start.AddHours(8), start.AddHours(16))],
            Dependencies = [new(new("dependency"), new("first"), new("second"))],
            InsertionCandidate = new(new("candidate"), "New task", start, start.AddHours(8))
        };
    }

    private static BunitContext CreateContext() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }
}

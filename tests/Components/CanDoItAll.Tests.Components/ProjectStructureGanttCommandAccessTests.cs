using System.Data.Common;
using System.Text.Json;
using Bunit;
using CanDoItAll.Components.Gantt;
using CanDoItAll.Editing;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using CanDoItAll.Workbench.Planning.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProjectStructure;

[Trait("Category", "HostPlatform")]
public sealed class ProjectStructureGanttCommandAccessTests {
    public enum Gesture { Title, Schedule, AddDependency, RemoveDependency, ReconnectDependency, Insertion, Order }
    public enum AccessCondition { Allowed, DeniedBefore, DeniedAfterSql }
    private static readonly DateTimeOffset Start = new(2026, 11, 3, 8, 0, 0, TimeSpan.Zero);

    public static IEnumerable<object[]> Commands => Enum.GetValues<Gesture>()
        .SelectMany(gesture => Enum.GetValues<AccessCondition>().Select(condition => new object[] { gesture, condition }));

    [Theory]
    [MemberData(nameof(Commands))]
    public async Task Native_Gantt_commands_require_current_access_at_the_actual_content_commit(Gesture gesture, AccessCondition condition) {
        var access = new EditingUiTestAccess();
        var probe = new RevokeContentAfterSql(access);
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IEditingUiAccess>(access);
            services.ConfigureDbContext<WorkbenchDbContext>(options => options.AddInterceptors(probe), ServiceLifetime.Singleton);
        });
        harness.Context.JSInterop.Setup<bool>("CanDoItAll.canvasWorkbench.create", _ => true).SetResult(true);
        var services = harness.Context.Services;
        var project = (await services.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = "Gantt command authority" })).Value;
        probe.Project = project;
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var admission = (await workbench.GetStructureAsync(project)).ExpectedProjectAdmission!;
        var owner = new ProjectStructureAgentContext("component-tests", "Gantt command authority", Environment.MachineName,
            AppContext.BaseDirectory, "", "gantt-command-authority") { ExpectedProjectAdmission = admission };
        var creator = services.GetRequiredService<ProjectStructureTaskCreationService>();
        var created = new List<ProjectStructureTaskCreateResult>();
        for (var index = 0; index < 3; index++) {
            created.Add(await creator.CreateAsync(project, new($"Task {index + 1}", Start.AddHours(index * 8), Start.AddHours((index + 1) * 8)) {
                ExpectedProjectAdmission = admission
            }, owner));
        }
        await workbench.LinkObjectsAsync(project, created[1].TaskNodeId, created[0].TaskNodeId,
            ProjectObjectLinkKind.DependsOn, expectedProjectAdmission: admission);
        services.GetRequiredService<NavigationManager>().NavigateTo($"/projects/{project:D}/structure?tab=gantt");
        var page = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(component => component.ProjectId, project));
        page.WaitForAssertion(() => {
            var presentation = page.FindComponent<PlanningGanttSurface>().Instance.Presentation;
            Assert.False(presentation.IsLoading);
            Assert.True(presentation.CanMutate);
            Assert.Equal(3, presentation.Tasks.Count);
        });
        var chart = page.FindComponent<GanttChart>().Instance;
        var first = Assert.Single(chart.Tasks, task => task.Id.Value == created[0].TaskNodeId);
        var second = Assert.Single(chart.Tasks, task => task.Id.Value == created[1].TaskNodeId);
        var third = Assert.Single(chart.Tasks, task => task.Id.Value == created[2].TaskNodeId);
        var previous = Assert.Single(chart.Dependencies);
        var invoke = CreateCommand(chart, gesture, first, second, third, previous);
        var before = await CaptureAsync(workbench, project);
        probe.Armed = condition == AccessCondition.DeniedAfterSql;
        access.Allowed = condition != AccessCondition.DeniedBefore;
        await page.InvokeAsync(invoke);
        var after = await CaptureAsync(workbench, project);
        if (condition == AccessCondition.Allowed) {
            Assert.NotEqual(before, after);
            Assert.False(probe.Reached);
            await AssertCommittedAsync(workbench, project, gesture, first, second, third);
            return;
        }
        Assert.Equal(condition == AccessCondition.DeniedAfterSql, probe.Reached);
        Assert.Equal(before, after);
        Assert.False(page.FindComponent<PlanningGanttSurface>().Instance.Presentation.CanMutate);
        access.Allowed = true;
        await page.InvokeAsync(invoke);
        Assert.Equal(before, await CaptureAsync(workbench, project));
    }

    private static Func<Task> CreateCommand(GanttChart chart, Gesture gesture, GanttTask first, GanttTask second,
        GanttTask third, GanttDependency previous) => gesture switch {
        Gesture.Title => () => chart.TaskTitleChangeRequested.InvokeAsync(new(first.Id, first.Title, "Current-authority title")),
        Gesture.Schedule => () => chart.TaskScheduleChangeRequested.InvokeAsync(new(first.Id, GanttScheduleGesture.ResizeEnd,
            [new(first.Id, first.Start, first.End, first.Start, first.End.AddHours(1), true),
             new(second.Id, second.Start, second.End, second.Start.AddHours(1), second.End.AddHours(1), true)], [])),
        Gesture.AddDependency => () => chart.DependencyMutationRequested.InvokeAsync(new(GanttDependencyMutationKind.Add,
            null, new(NewDependencyId(), first.Id, third.Id))),
        Gesture.RemoveDependency => () => chart.DependencyMutationRequested.InvokeAsync(new(GanttDependencyMutationKind.Remove, previous, null)),
        Gesture.ReconnectDependency => () => chart.DependencyMutationRequested.InvokeAsync(new(GanttDependencyMutationKind.Reconnect,
            previous, new(previous.Id, third.Id, second.Id))),
        Gesture.Insertion => () => chart.TaskInsertionRequested.InvokeAsync(CreateInsertion(first, second, previous)),
        Gesture.Order => () => chart.TaskOrderChangeRequested.InvokeAsync(new(first.Id, second.Id, GanttTaskOrderPlacement.After)),
        _ => throw new ArgumentOutOfRangeException(nameof(gesture))
    };

    [Fact]
    public async Task A_physically_committed_Gantt_command_keeps_its_original_target_when_the_reply_outlives_the_view() {
        var probe = new HoldCommittedTitle();
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.ConfigureDbContext<WorkbenchDbContext>(options => options.AddInterceptors(probe), ServiceLifetime.Singleton));
        harness.Context.JSInterop.Setup<bool>("CanDoItAll.canvasWorkbench.create", _ => true).SetResult(true);
        var services = harness.Context.Services;
        var projects = services.GetRequiredService<ProjectsService>();
        var project = (await projects.SaveAsync(new() { Name = "Original committed Gantt target" })).Value;
        var successor = (await projects.SaveAsync(new() { Name = "Successor Gantt target" })).Value;
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var admission = (await workbench.GetStructureAsync(project)).ExpectedProjectAdmission!;
        var owner = new ProjectStructureAgentContext("component-tests", "Gantt late committed reply", Environment.MachineName,
            AppContext.BaseDirectory, "", "gantt-late-committed-reply") { ExpectedProjectAdmission = admission };
        var task = await services.GetRequiredService<ProjectStructureTaskCreationService>().CreateAsync(project,
            new("Original title", Start, Start.AddHours(8)) { ExpectedProjectAdmission = admission }, owner);
        var navigation = services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/projects/{project:D}/structure?tab=gantt");
        var page = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(component => component.ProjectId, project));
        page.WaitForAssertion(() => Assert.True(page.FindComponent<PlanningGanttSurface>().Instance.Presentation.CanMutate));
        var chart = page.FindComponent<GanttChart>().Instance;
        probe.Project = project;
        probe.Armed = true;
        var pending = page.InvokeAsync(() => chart.TaskTitleChangeRequested.InvokeAsync(new(new(task.TaskNodeId), "Original title", HoldCommittedTitle.Title)));
        try {
            await probe.Committed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(HoldCommittedTitle.Title, Assert.Single((await workbench.GetStructureAsync(project)).Nodes,
                node => node.Id == task.TaskNodeId).Title);
            navigation.NavigateTo($"/projects/{successor:D}/structure?tab=gantt");
            await page.InvokeAsync(() => page.Render(parameters => parameters.Add(component => component.ProjectId, successor)));
            page.WaitForAssertion(() => Assert.Equal(successor, page.FindComponent<ProjectStructureGanttPanel>().Instance.ProjectId));
        } finally {
            probe.Release.TrySetResult();
            await pending.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(HoldCommittedTitle.Title, Assert.Single((await workbench.GetStructureAsync(project)).Nodes,
            node => node.Id == task.TaskNodeId).Title);
        Assert.DoesNotContain((await workbench.GetStructureAsync(successor)).Nodes, node => node.ObjectSubtype == "task");
        Assert.Null(page.FindComponent<PlanningGanttSurface>().Instance.Presentation.Receipt);
    }

    private static GanttTaskInsertionRequest CreateInsertion(GanttTask first, GanttTask second, GanttDependency previous) {
        var inserted = new GanttTask(new($"custom:{Guid.NewGuid():N}"), "Inserted with current authority", first.End, first.End.AddHours(1));
        return new(inserted, first.Id, second.Id,
            [new(GanttDependencyMutationKind.Remove, previous, null),
             new(GanttDependencyMutationKind.Add, null, new(NewDependencyId(), first.Id, inserted.Id)),
             new(GanttDependencyMutationKind.Add, null, new(NewDependencyId(), inserted.Id, second.Id))],
            [new(inserted.Id, inserted.Start, inserted.End, inserted.Start, inserted.End, true),
             new(second.Id, second.Start, second.End, second.Start.AddHours(1), second.End.AddHours(1), true)],
            [first.Id, inserted.Id, second.Id]);
    }

    private static GanttDependencyId NewDependencyId() => new($"gantt-dependency:{Guid.NewGuid():N}");

    private static async Task AssertCommittedAsync(ProjectWorkbenchService workbench, Guid project, Gesture gesture,
        GanttTask first, GanttTask second, GanttTask third) {
        var surface = await workbench.GetStructureAsync(project);
        var firstNode = Assert.Single(surface.Nodes, node => node.Id == first.Id.Value);
        var secondNode = Assert.Single(surface.Nodes, node => node.Id == second.Id.Value);
        var dependencies = surface.Links.Where(link => link.Kind == ProjectObjectLinkKind.DependsOn).ToArray();
        switch (gesture) {
            case Gesture.Title:
                Assert.Equal("Current-authority title", firstNode.Title);
                break;
            case Gesture.Schedule:
                Assert.Equal(first.End.AddHours(1), firstNode.EndUtc);
                Assert.Equal(second.Start.AddHours(1), secondNode.StartUtc);
                break;
            case Gesture.AddDependency:
                Assert.Equal(2, dependencies.Length);
                Assert.Contains(dependencies, link => link.SourceId == third.Id.Value && link.TargetId == first.Id.Value);
                break;
            case Gesture.RemoveDependency:
                Assert.Empty(dependencies);
                break;
            case Gesture.ReconnectDependency:
                var reconnected = Assert.Single(dependencies);
                Assert.Equal(second.Id.Value, reconnected.SourceId);
                Assert.Equal(third.Id.Value, reconnected.TargetId);
                Assert.Equal(third.End, secondNode.StartUtc);
                break;
            case Gesture.Insertion:
                Assert.Single(surface.Nodes, node => node.Title == "Inserted with current authority");
                Assert.Equal(2, dependencies.Length);
                Assert.Equal(second.Start.AddHours(1), secondNode.StartUtc);
                break;
            case Gesture.Order:
                var order = await workbench.LoadGanttViewStateAsync(project);
                Assert.Equal([second.Id.Value, first.Id.Value], order.OrderedTaskNodeIds.Take(2));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(gesture));
        }
    }

    private static async Task<string> CaptureAsync(ProjectWorkbenchService workbench, Guid project) {
        var surface = await workbench.GetStructureAsync(project);
        var order = await workbench.LoadGanttViewStateAsync(project);
        return JsonSerializer.Serialize(new {
            Nodes = surface.Nodes.OrderBy(node => node.Id, StringComparer.Ordinal).Select(node => new {
                node.Id, RecordId = node.IsSystemManaged ? (Guid?)null : node.RecordId,
                node.Title, node.Notes, node.StartUtc, node.EndUtc, node.DurationSeconds, node.MetadataJson
            }),
            Links = surface.Links.OrderBy(link => link.SourceId, StringComparer.Ordinal).ThenBy(link => link.TargetId, StringComparer.Ordinal)
                .ThenBy(link => link.Kind).Select(link => new {
                    link.SourceId, link.TargetId, link.Kind, link.IsUserAuthored, RecordId = link.IsUserAuthored ? link.RecordId : null
                }),
            order.OrderedTaskNodeIds
        });
    }

    private sealed class RevokeContentAfterSql(EditingUiTestAccess access) : SaveChangesInterceptor {
        public Guid Project { get; set; }
        public bool Armed { get; set; }
        public bool Reached { get; private set; }

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) {
            if (Armed && result > 0 && eventData.Context is WorkbenchDbContext context &&
                    (context.ChangeTracker.Entries<ProjectObjectRecord>().Any(entry => entry.Entity.ProjectId == Project) ||
                     context.ChangeTracker.Entries<ProjectWorkbenchViewStateRecord>().Any(entry => entry.Entity.ProjectId == Project))) {
                Assert.NotNull(context.Database.CurrentTransaction);
                Armed = false;
                Reached = true;
                access.Allowed = false;
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class HoldCommittedTitle : DbTransactionInterceptor {
        public const string Title = "Physically committed original title";
        public Guid Project { get; set; }
        public bool Armed { get; set; }
        public TaskCompletionSource Committed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default) {
            if (Armed && eventData.Context is WorkbenchDbContext context && context.ChangeTracker.Entries<ProjectObjectRecord>()
                    .Any(entry => entry.Entity.ProjectId == Project && entry.Entity.Title == Title)) {
                Armed = false;
                Committed.TrySetResult();
                await Release.Task;
            }
        }
    }
}

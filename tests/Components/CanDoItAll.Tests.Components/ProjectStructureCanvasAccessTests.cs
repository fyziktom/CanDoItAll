using System.Data.Common;
using System.Text.Json;
using Bunit;
using CanDoItAll.Application.UI.Dashboard;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Editing;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using CanDoItAll.Workbench.Insights.UI;
using CanDoItAll.Web.Dashboard;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProjectStructure;

[Trait("Category", "HostPlatform")]
public sealed class ProjectStructureCanvasAccessTests {
    public enum Command { Progress, Started, Priority, AddMarker, ClearMarkers, ViewState }
    public enum AccessCondition { Allowed, DeniedBefore, DeniedAfterSql }

    public static IEnumerable<object[]> Commands => Enum.GetValues<Command>()
        .SelectMany(command => Enum.GetValues<AccessCondition>().Select(condition => new object[] { command, condition }));

    [Fact]
    public async Task Direct_canvas_create_uses_the_displayed_project_admission() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        harness.Context.JSInterop.Setup<bool>("CanDoItAll.canvasWorkbench.create", _ => true).SetResult(true);
        var services = harness.Context.Services;
        var project = (await services.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = "Direct canvas create" })).Value;
        var rootId = $"project:{project:D}";
        services.GetRequiredService<NavigationManager>().NavigateTo($"/projects/{project:D}/structure");
        var page = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(component => component.ProjectId, project));
        page.WaitForAssertion(() => Assert.Contains(page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes, node => node.Id == rootId));
        Assert.True(ProjectStructureCanvasCatalog.TryResolveCreateDefinition(ProjectObjectType.Note, null, out var definition));

        var canvas = page.FindComponent<CanvasWorkbench>().Instance;
        await page.InvokeAsync(() => canvas.ContextActionRequested.InvokeAsync(new(rootId, definition.ActionId, 300, 300)));

        var structure = await services.GetRequiredService<ProjectWorkbenchService>().GetStructureAsync(project);
        var note = Assert.Single(structure.Nodes, node => node.ObjectType == ProjectObjectType.Note);
        Assert.Equal(rootId, note.ParentId);
        Assert.Equal(project, note.ProjectId);
    }

    [Theory]
    [MemberData(nameof(Commands))]
    public async Task Actual_Canvas_actions_and_preferences_require_current_access_at_physical_commit(Command command, AccessCondition condition) {
        var access = new EditingUiTestAccess();
        var probe = new RevokeAfterSql(access);
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IEditingUiAccess>(access);
            services.ConfigureDbContext<WorkbenchDbContext>(options => options.AddInterceptors(probe), ServiceLifetime.Singleton);
        });
        harness.Context.JSInterop.Setup<bool>("CanDoItAll.canvasWorkbench.create", _ => true).SetResult(true);
        var services = harness.Context.Services;
        var project = (await services.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = "Canvas command authority" })).Value;
        probe.Project = project;
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var admission = (await workbench.GetStructureAsync(project)).ExpectedProjectAdmission!;
        var nodes = new List<ProjectStructureNode>();
        for (var index = 0; index < 3; index++) {
            nodes.Add(await workbench.CreateObjectAsync(project, new(ProjectObjectType.Note, $"Canvas node {index}", "", "Original notes",
                $"project:{project:D}", 100 + index * 300, 100, Status: "Open") { ExpectedProjectAdmission = admission }));
        }
        var ids = nodes.Take(2).Select(node => node.Id).ToArray();
        var initial = await workbench.GetStructureAsync(project);
        await workbench.AddObjectMarkerDetailedAsync(project, ids, "risk", "warn", "Risk", expectedProjectAdmission: admission,
            expectedStates: ProjectStructureNodeStateGuard.CaptureMany(initial.Nodes.Where(node => ids.Contains(node.Id)), admission));
        services.GetRequiredService<NavigationManager>().NavigateTo($"/projects/{project:D}/structure");
        var page = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(component => component.ProjectId, project));
        page.WaitForAssertion(() => Assert.Contains(page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes, node => node.Id == ids[0]));
        var canvas = page.FindComponent<CanvasWorkbench>().Instance;
        await page.InvokeAsync(() => canvas.OnSelectionChanged(ids[0], JsonSerializer.Serialize(ids)));
        Func<Task> invoke;
        if (command == Command.ViewState) {
            await page.InvokeAsync(() => page.Find("[data-testid='project-structure-object-index-toggle']").ClickAsync(new MouseEventArgs()));
            var index = page.FindComponent<ProjectStructureObjectIndexWindow>().Instance;
            invoke = () => index.StateChanged.InvokeAsync(new InsightsWindowIntent(index.Origin, InsightsWindow.ObjectIndex,
                new() { IsVisible = true, Left = 91, Top = 117, Width = 400, Height = 600 }));
        } else {
            var action = command switch {
                Command.Progress => "progress:75",
                Command.Started => "progress:started",
                Command.Priority => "priority:4",
                Command.AddMarker => "marker:idea",
                Command.ClearMarkers => "marker:none",
                _ => throw new ArgumentOutOfRangeException(nameof(command))
            };
            invoke = () => canvas.OnContextAction(ids[0], action, 100, 100);
        }
        var before = await CaptureAsync(workbench, project);
        probe.Armed = condition == AccessCondition.DeniedAfterSql;
        access.Allowed = condition != AccessCondition.DeniedBefore;
        await page.InvokeAsync(invoke);
        var after = await CaptureAsync(workbench, project);
        if (condition != AccessCondition.Allowed) {
            Assert.Equal(condition == AccessCondition.DeniedAfterSql, probe.Reached);
            Assert.Equal(before, after);
            Assert.Contains(services.GetRequiredService<NotificationService>().Messages, message =>
                message.Summary == "Structure changes not saved" && message.Severity == NotificationSeverity.Warning);
            return;
        }
        Assert.NotEqual(before, after);
        var accepted = await workbench.GetStructureAsync(project);
        Assert.Equal(JsonSerializer.Serialize(nodes[2]), JsonSerializer.Serialize(accepted.Nodes.Single(node => node.Id == nodes[2].Id)));
        if (command == Command.ViewState) {
            var stored = CanvasWorkbenchUiState.Parse(accepted.ViewStateJson);
            Assert.Equal(91, stored.WindowStates["project-structure.objectIndex"].Left);
            Assert.Equal(ids, stored.SelectedNodeIds);
            return;
        }
        foreach (var node in accepted.Nodes.Where(node => ids.Contains(node.Id))) {
            switch (command) {
                case Command.Progress:
                    Assert.Equal(75, node.ProgressPercent);
                    Assert.Equal("Open", node.Status);
                    break;
                case Command.Started:
                    Assert.Equal("started", node.ProgressMode);
                    Assert.Equal(0, node.ProgressPercent);
                    Assert.Equal("Open", node.Status);
                    break;
                case Command.Priority:
                    Assert.Equal(4, node.Priority);
                    break;
                case Command.AddMarker:
                    Assert.Equal(new[] { "idea", "risk" }, node.Markers.Select(marker => marker.Icon).Order());
                    break;
                case Command.ClearMarkers:
                    Assert.Empty(node.Markers);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(command));
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_physically_committed_Canvas_reply_remains_saved_and_cannot_publish_into_retired_access_or_project(bool retireView) {
        var access = new EditingUiTestAccess();
        var hold = new HoldCommittedProgress();
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IEditingUiAccess>(access);
            services.ConfigureDbContext<WorkbenchDbContext>(options => options.AddInterceptors(hold), ServiceLifetime.Singleton);
        });
        harness.Context.JSInterop.Setup<bool>("CanDoItAll.canvasWorkbench.create", _ => true).SetResult(true);
        var services = harness.Context.Services;
        var projects = services.GetRequiredService<ProjectsService>();
        var project = (await projects.SaveAsync(new() { Name = "Original Canvas commit" })).Value;
        var next = (await projects.SaveAsync(new() { Name = "Successor Canvas" })).Value;
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var admission = (await workbench.GetStructureAsync(project)).ExpectedProjectAdmission!;
        var note = await workbench.CreateObjectAsync(project, new(ProjectObjectType.Note, "Original Canvas target", "", "Retained notes",
            $"project:{project:D}") { ExpectedProjectAdmission = admission });
        services.GetRequiredService<NavigationManager>().NavigateTo($"/projects/{project:D}/structure");
        var page = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(component => component.ProjectId, project));
        page.WaitForAssertion(() => Assert.Contains(page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes, node => node.Id == note.Id));
        var retainedNode = JsonSerializer.Serialize(page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes.Single(node => node.Id == note.Id));
        hold.Project = project;
        hold.Armed = true;
        var pending = page.InvokeAsync(() => page.FindComponent<CanvasWorkbench>().Instance.OnContextAction(note.Id, "progress:75", 100, 100));
        try {
            await hold.Committed.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal(75, (await workbench.GetStructureAsync(project)).Nodes.Single(node => node.Id == note.Id).ProgressPercent);
            if (retireView) {
                services.GetRequiredService<NavigationManager>().NavigateTo($"/projects/{next:D}/structure");
                await page.InvokeAsync(() => page.Render(parameters => parameters.Add(component => component.ProjectId, next)));
                page.WaitForAssertion(() => Assert.Contains(page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes, node => node.Id == $"project:{next:D}"));
            } else {
                access.Allowed = false;
            }
        } finally {
            hold.Release.TrySetResult();
        }
        await pending.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Equal(75, (await workbench.GetStructureAsync(project)).Nodes.Single(node => node.Id == note.Id).ProgressPercent);
        Assert.DoesNotContain((await workbench.GetStructureAsync(next)).Nodes, node => node.Id == note.Id);
        var outcome = page.Instance.AuthoringOutcomes.Last();
        Assert.Contains(note.Id, outcome.NodeIds);
        Assert.Equal(retireView ? ProjectStructureAuthoringResultKind.Committed : ProjectStructureAuthoringResultKind.PartialCommit, outcome.Kind);
        if (retireView) {
            Assert.DoesNotContain(services.GetRequiredService<NotificationService>().Messages, message => message.Summary.StartsWith("Structure changes", StringComparison.Ordinal));
        } else {
            Assert.Equal(retainedNode, JsonSerializer.Serialize(page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes.Single(node => node.Id == note.Id)));
            Assert.Contains(services.GetRequiredService<NotificationService>().Messages, message => message.Summary == "Structure changes saved; refresh required");
        }
    }

    [Fact]
    public async Task A_Canvas_context_target_outside_the_selection_does_not_mutate_selected_neighbors() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        harness.Context.JSInterop.Setup<bool>("CanDoItAll.canvasWorkbench.create", _ => true).SetResult(true);
        var services = harness.Context.Services;
        var project = (await services.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = "Explicit Canvas context target" })).Value;
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var admission = (await workbench.GetStructureAsync(project)).ExpectedProjectAdmission!;
        var nodes = new List<ProjectStructureNode>();
        for (var index = 0; index < 3; index++) {
            nodes.Add(await workbench.CreateObjectAsync(project, new(ProjectObjectType.Note, $"Context node {index}", "", "Retained", $"project:{project:D}") {
                ExpectedProjectAdmission = admission
            }));
        }
        services.GetRequiredService<NavigationManager>().NavigateTo($"/projects/{project:D}/structure");
        var page = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(component => component.ProjectId, project));
        page.WaitForAssertion(() => Assert.Equal(4, page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes.Count));
        var canvas = page.FindComponent<CanvasWorkbench>().Instance;
        await page.InvokeAsync(() => canvas.OnSelectionChanged(nodes[0].Id, JsonSerializer.Serialize(nodes.Take(2).Select(node => node.Id))));
        await page.InvokeAsync(() => canvas.OnContextAction(nodes[2].Id, "priority:4", 100, 100));
        var accepted = await workbench.GetStructureAsync(project);
        foreach (var node in nodes.Take(2)) {
            Assert.Equal(JsonSerializer.Serialize(node), JsonSerializer.Serialize(accepted.Nodes.Single(current => current.Id == node.Id)));
        }
        Assert.Equal(4, accepted.Nodes.Single(node => node.Id == nodes[2].Id).Priority);
    }

    [Fact]
    public async Task Retiring_the_actual_Canvas_after_preference_SQL_rejects_the_uncommitted_original_write() {
        var hold = new HoldViewAfterSql();
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.ConfigureDbContext<WorkbenchDbContext>(options => options.AddInterceptors(hold), ServiceLifetime.Singleton));
        harness.Context.JSInterop.Setup<bool>("CanDoItAll.canvasWorkbench.create", _ => true).SetResult(true);
        var services = harness.Context.Services;
        var projects = services.GetRequiredService<ProjectsService>();
        var project = (await projects.SaveAsync(new() { Name = "Uncommitted Canvas preference" })).Value;
        var next = (await projects.SaveAsync(new() { Name = "Successor preference owner" })).Value;
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        services.GetRequiredService<NavigationManager>().NavigateTo($"/projects/{project:D}/structure");
        var page = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(component => component.ProjectId, project));
        page.WaitForElement("[data-testid='project-structure-canvas-loaded']");
        await page.InvokeAsync(() => page.Find("[data-testid='project-structure-object-index-toggle']").ClickAsync(new MouseEventArgs()));
        var index = page.FindComponent<ProjectStructureObjectIndexWindow>().Instance;
        var before = await CaptureAsync(workbench, project);
        hold.Project = project;
        hold.Armed = true;
        var pending = page.InvokeAsync(() => index.StateChanged.InvokeAsync(new InsightsWindowIntent(index.Origin, InsightsWindow.ObjectIndex,
            new() { IsVisible = true, Left = 91, Top = 117, Width = 400, Height = 600 })));
        try {
            await hold.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            services.GetRequiredService<NavigationManager>().NavigateTo($"/projects/{next:D}/structure");
            await page.InvokeAsync(() => page.Render(parameters => parameters.Add(component => component.ProjectId, next)));
            page.WaitForAssertion(() => Assert.Contains(page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes, node => node.Id == $"project:{next:D}"));
        } finally {
            hold.Release.TrySetResult();
        }
        await pending.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Equal(before, await CaptureAsync(workbench, project));
        Assert.Null((await workbench.GetStructureAsync(next)).ViewStateJson);
        Assert.DoesNotContain(services.GetRequiredService<NotificationService>().Messages, message => message.Summary.StartsWith("Structure changes", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Unmounting_Canvas_during_preference_SQL_handles_cancellation_or_retired_access_and_allows_Dashboard(bool debounced) {
        var hold = new HoldViewAfterSql { HonorCancellation = debounced };
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.ConfigureDbContext<WorkbenchDbContext>(options => options.AddInterceptors(hold), ServiceLifetime.Singleton);
            services.AddCanDoItAllDashboard();
            services.AddSingleton<IDashboardSnapshotLoadRunner, EmptyDashboardSnapshotRunner>();
        });
        harness.Context.JSInterop.Setup<bool>("CanDoItAll.canvasWorkbench.create", _ => true).SetResult(true);
        var services = harness.Context.Services;
        var project = (await services.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = "Retiring preference owner" })).Value;
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var navigation = services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/projects/{project:D}/structure");
        var page = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(component => component.ProjectId, project));
        page.WaitForElement("[data-testid='project-structure-canvas-loaded']");
        Func<Task> change;
        if (debounced) {
            var canvas = page.FindComponent<CanvasWorkbench>().Instance;
            change = () => canvas.OnStateChanged(new CanvasWorkbenchUiState { Zoom = 1.4 }.ToJson());
        } else {
            await page.InvokeAsync(() => page.Find("[data-testid='project-structure-object-index-toggle']").ClickAsync(new MouseEventArgs()));
            var index = page.FindComponent<ProjectStructureObjectIndexWindow>().Instance;
            change = () => index.StateChanged.InvokeAsync(new InsightsWindowIntent(index.Origin, InsightsWindow.ObjectIndex,
                new() { IsVisible = true, Left = 91, Top = 117, Width = 400, Height = 600 }));
        }
        var before = await CaptureAsync(workbench, project);
        hold.Project = project;
        hold.Armed = true;
        var pending = page.InvokeAsync(change);
        Task disposal;
        try {
            await hold.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            disposal = harness.Context.DisposeRenderedComponentsAsync();
            await hold.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(20));
            if (!debounced) {
                Assert.False(disposal.IsCompleted);
            }
        } finally {
            hold.Release.TrySetResult();
        }
        await disposal.WaitAsync(TimeSpan.FromSeconds(20));
        await pending.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.False(harness.Context.Renderer.UnhandledException.IsCompleted);
        Assert.Equal(before, await CaptureAsync(workbench, project));
        Assert.DoesNotContain(services.GetRequiredService<NotificationService>().Messages,
            message => message.Summary.StartsWith("Structure", StringComparison.Ordinal));

        navigation.NavigateTo("/dashboard");
        var dashboard = harness.Context.Render<CanDoItAll.Web.Components.Pages.Home>();
        dashboard.WaitForElement("[data-testid='dashboard-refresh-countdown']");
        dashboard.WaitForAssertion(() => Assert.Contains("No projects yet", dashboard.Markup, StringComparison.Ordinal));
        Assert.False(harness.Context.Renderer.UnhandledException.IsCompleted);
    }

    [Fact]
    public async Task An_unexpected_preference_failure_remains_visible_to_the_active_Canvas_owner() {
        var hold = new HoldViewAfterSql { FailureAfterRelease = new IOException("Preference persistence failed.") };
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.ConfigureDbContext<WorkbenchDbContext>(options => options.AddInterceptors(hold), ServiceLifetime.Singleton));
        harness.Context.JSInterop.Setup<bool>("CanDoItAll.canvasWorkbench.create", _ => true).SetResult(true);
        var services = harness.Context.Services;
        var project = (await services.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = "Active preference owner" })).Value;
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        services.GetRequiredService<NavigationManager>().NavigateTo($"/projects/{project:D}/structure");
        var page = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(component => component.ProjectId, project));
        page.WaitForElement("[data-testid='project-structure-canvas-loaded']");
        var before = await CaptureAsync(workbench, project);
        hold.Project = project;
        hold.Armed = true;
        await page.InvokeAsync(() => page.FindComponent<CanvasWorkbench>().Instance.OnStateChanged(new CanvasWorkbenchUiState { Zoom = 1.4 }.ToJson()));
        try {
            await hold.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
        } finally {
            hold.Release.TrySetResult();
        }
        page.WaitForAssertion(() => Assert.Contains(services.GetRequiredService<NotificationService>().Messages,
            message => message.Summary == "Structure outcome unconfirmed" && message.Severity == NotificationSeverity.Warning));
        Assert.Equal(before, await CaptureAsync(workbench, project));
        await harness.Context.DisposeRenderedComponentsAsync().WaitAsync(TimeSpan.FromSeconds(20));
        Assert.False(harness.Context.Renderer.UnhandledException.IsCompleted);
    }

    private sealed class EmptyDashboardSnapshotRunner : IDashboardSnapshotLoadRunner {
        public Task<DashboardSnapshotData> LoadAsync() => Task.FromResult(new DashboardSnapshotData(
            [], DashboardActivityMode.RecentFallback, [], DashboardActivityMode.RecentFallback, [],
            new DashboardUsageTotals(0, 0m, 0, DateTimeOffset.UtcNow)));
    }

    private static async Task<string> CaptureAsync(ProjectWorkbenchService workbench, Guid project) {
        var surface = await workbench.GetStructureAsync(project);
        return JsonSerializer.Serialize(new {
            Nodes = surface.Nodes.Where(node => !node.IsSystemManaged).OrderBy(node => node.Id, StringComparer.Ordinal),
            surface.ViewStateJson
        });
    }

    private sealed class HoldViewAfterSql : SaveChangesInterceptor {
        public Guid Project { get; set; }
        public bool Armed { get; set; }
        public bool HonorCancellation { get; init; } = true;
        public Exception? FailureAfterRelease { get; init; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource CancellationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) {
            if (Armed && result > 0 && eventData.Context is WorkbenchDbContext context &&
                    context.ChangeTracker.Entries<ProjectWorkbenchViewStateRecord>().Any(entry => entry.Entity.ProjectId == Project)) {
                Assert.NotNull(context.Database.CurrentTransaction);
                Armed = false;
                using var registration = cancellationToken.Register(() => CancellationObserved.TrySetResult());
                Entered.TrySetResult();
                if (HonorCancellation) {
                    await Release.Task.WaitAsync(cancellationToken);
                } else {
                    await Release.Task;
                }
                if (FailureAfterRelease is { } failure) {
                    throw failure;
                }
            }
            return result;
        }
    }

    private sealed class RevokeAfterSql(EditingUiTestAccess access) : SaveChangesInterceptor {
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

    private sealed class HoldCommittedProgress : DbTransactionInterceptor {
        public Guid Project { get; set; }
        public bool Armed { get; set; }
        public TaskCompletionSource Committed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default) {
            if (Armed && eventData.Context is WorkbenchDbContext context && context.ChangeTracker.Entries<ProjectObjectRecord>()
                    .Any(entry => entry.Entity.ProjectId == Project && entry.Entity.ProgressPercent == 75)) {
                Armed = false;
                Committed.TrySetResult();
                await Release.Task;
            }
        }
    }
}

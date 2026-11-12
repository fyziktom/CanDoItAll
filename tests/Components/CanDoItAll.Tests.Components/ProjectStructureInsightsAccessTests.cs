using System.Data.Common;
using System.Text.Json;
using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Editing;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using CanDoItAll.Workbench.Insights.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProjectStructure;

[Trait("Category", "HostPlatform")]
public sealed class ProjectStructureInsightsAccessTests {
    public enum Command { Status, Progress, Priority, AddMarker, RemoveMarker, ClearMarkers }
    public enum AccessCondition { Allowed, DeniedBefore, DeniedAfterSql }

    public static IEnumerable<object[]> Commands => Enum.GetValues<Command>()
        .SelectMany(command => Enum.GetValues<AccessCondition>().Select(condition => new object[] { command, condition }));

    [Theory]
    [MemberData(nameof(Commands))]
    public async Task Native_Insights_batch_commands_require_current_access_at_physical_commit(Command command, AccessCondition condition) {
        var access = new EditingUiTestAccess();
        var probe = new RevokeContentAfterSql(access);
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IEditingUiAccess>(access);
            services.ConfigureDbContext<WorkbenchDbContext>(options => options.AddInterceptors(probe), ServiceLifetime.Singleton);
        });
        harness.Context.JSInterop.Setup<bool>("CanDoItAll.canvasWorkbench.create", _ => true).SetResult(true);
        var services = harness.Context.Services;
        var project = (await services.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = "Insights batch authority" })).Value;
        probe.Project = project;
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var admission = (await workbench.GetStructureAsync(project)).ExpectedProjectAdmission!;
        var nodes = new List<ProjectStructureNode>();
        for (var index = 0; index < 3; index++) {
            nodes.Add(await workbench.CreateObjectAsync(project, new(ProjectObjectType.Note, $"Batch node {index}", "", "Original batch notes",
                $"project:{project:D}", 100 + index * 300, 100, Status: "Open") { ExpectedProjectAdmission = admission }));
        }
        var ids = nodes.Take(2).Select(node => node.Id).ToArray();
        var initial = await workbench.GetStructureAsync(project);
        await workbench.AddObjectMarkerDetailedAsync(project, ids, "risk", "warn", "Risk", expectedProjectAdmission: admission,
            expectedStates: ProjectStructureNodeStateGuard.CaptureMany(initial.Nodes.Where(node => ids.Contains(node.Id)).ToArray(), admission));
        initial = await workbench.GetStructureAsync(project);
        await workbench.AddObjectMarkerDetailedAsync(project, ids, "idea", "mint", "Idea", expectedProjectAdmission: admission,
            expectedStates: ProjectStructureNodeStateGuard.CaptureMany(initial.Nodes.Where(node => ids.Contains(node.Id)).ToArray(), admission));
        services.GetRequiredService<NavigationManager>().NavigateTo($"/projects/{project:D}/structure");
        var page = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(component => component.ProjectId, project));
        page.WaitForAssertion(() => Assert.Contains(page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes, node => node.Id == ids[0]));
        await page.InvokeAsync(() => page.FindComponent<CanvasWorkbench>().Instance.OnSelectionChanged(ids[0], JsonSerializer.Serialize(ids)));
        var selection = page.FindComponent<ProjectStructureSelectionPanel>().Instance;
        Assert.Equal(ids.Order(), selection.State.SelectedNodes.Select(node => node.Id).Order());
        Assert.True(selection.State.CanApplySelectionStatus);
        Func<Task> invoke;
        if (command == Command.RemoveMarker) {
            await page.InvokeAsync(() => page.FindComponent<ProjectStructureToolbarActions>().Instance.ToggleSignalsWindow.InvokeAsync());
            var signals = page.FindComponent<ProjectStructureSignalsWindow>().Instance;
            invoke = () => signals.OnApplyAction.InvokeAsync(new(signals.Origin, ids, "marker:risk"));
        } else {
            InsightsSelectionCommand intent = command switch {
                Command.Status => new InsightsSelectionCommand.Status(InsightsStatus.Done),
                Command.Progress => new InsightsSelectionCommand.Progress(75),
                Command.Priority => new InsightsSelectionCommand.Priority(4),
                Command.AddMarker => new InsightsSelectionCommand.Marker(new("review", "mint", "Review")),
                Command.ClearMarkers => new InsightsSelectionCommand.Marker(new("", "", "")),
                _ => throw new ArgumentOutOfRangeException(nameof(command))
            };
            invoke = () => selection.Dispatch.InvokeAsync(new(selection.Origin, ids, intent));
        }
        var before = await CaptureAsync(workbench, project);
        probe.Armed = condition == AccessCondition.DeniedAfterSql;
        access.Allowed = condition != AccessCondition.DeniedBefore;
        await page.InvokeAsync(invoke);
        var after = await CaptureAsync(workbench, project);
        if (condition != AccessCondition.Allowed) {
            Assert.Equal(condition == AccessCondition.DeniedAfterSql, probe.Reached);
            Assert.Equal(before, after);
            Assert.Contains("not saved", page.Markup, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("No write result was confirmed", page.Markup, StringComparison.Ordinal);
            Assert.Contains(services.GetRequiredService<NotificationService>().Messages, message =>
                message.Summary == "Structure changes not saved" && message.Severity == NotificationSeverity.Warning);
            return;
        }
        Assert.NotEqual(before, after);
        Assert.False(probe.Reached);
        var accepted = await workbench.GetStructureAsync(project);
        Assert.Equal(JsonSerializer.Serialize(nodes[2]), JsonSerializer.Serialize(accepted.Nodes.Single(node => node.Id == nodes[2].Id)));
        foreach (var node in accepted.Nodes.Where(node => ids.Contains(node.Id))) {
            switch (command) {
                case Command.Status:
                    Assert.Equal("Done", node.Status);
                    Assert.Equal(100, node.ProgressPercent);
                    break;
                case Command.Progress:
                    Assert.Equal(75, node.ProgressPercent);
                    Assert.Equal("Open", node.Status);
                    break;
                case Command.Priority:
                    Assert.Equal(4, node.Priority);
                    break;
                case Command.AddMarker:
                    Assert.Equal(new[] { "idea", "review", "risk" }, node.Markers.Select(marker => marker.Icon).Order());
                    break;
                case Command.RemoveMarker:
                    Assert.Equal("idea", Assert.Single(node.Markers).Icon);
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
    public async Task A_physically_committed_Insights_reply_keeps_its_original_target_and_withholds_fresh_body_after_access_or_view_retirement(bool retireView) {
        var access = new EditingUiTestAccess();
        var hold = new HoldCommittedProgress();
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IEditingUiAccess>(access);
            services.ConfigureDbContext<WorkbenchDbContext>(options => options.AddInterceptors(hold), ServiceLifetime.Singleton);
        });
        harness.Context.JSInterop.Setup<bool>("CanDoItAll.canvasWorkbench.create", _ => true).SetResult(true);
        var services = harness.Context.Services;
        var projects = services.GetRequiredService<ProjectsService>();
        var project = (await projects.SaveAsync(new() { Name = "Original confirmed Insights" })).Value;
        var nextProject = (await projects.SaveAsync(new() { Name = "Successor Insights" })).Value;
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var admission = (await workbench.GetStructureAsync(project)).ExpectedProjectAdmission!;
        var note = await workbench.CreateObjectAsync(project, new(ProjectObjectType.Note, "Original progress target", "", "", $"project:{project:D}") {
            ExpectedProjectAdmission = admission
        });
        services.GetRequiredService<NavigationManager>().NavigateTo($"/projects/{project:D}/structure");
        var page = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(component => component.ProjectId, project));
        page.WaitForAssertion(() => Assert.Contains(page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes, node => node.Id == note.Id));
        await page.InvokeAsync(() => page.FindComponent<CanvasWorkbench>().Instance.OnSelectionChanged(note.Id, JsonSerializer.Serialize(new[] { note.Id })));
        var panel = page.FindComponent<ProjectStructureSelectionPanel>().Instance;
        hold.Project = project;
        hold.Armed = true;
        var pending = page.InvokeAsync(() => panel.Dispatch.InvokeAsync(new(panel.Origin, [note.Id], new InsightsSelectionCommand.Progress(75))));
        try {
            await hold.Committed.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal(75, (await workbench.GetStructureAsync(project)).Nodes.Single(node => node.Id == note.Id).ProgressPercent);
            if (retireView) {
                services.GetRequiredService<NavigationManager>().NavigateTo($"/projects/{nextProject:D}/structure");
                await page.InvokeAsync(() => page.Render(parameters => parameters.Add(component => component.ProjectId, nextProject)));
                page.WaitForAssertion(() => Assert.Contains(page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes,
                    node => node.Id == $"project:{nextProject:D}"));
            }
            access.Allowed = false;
        } finally {
            hold.Release.TrySetResult();
        }
        await pending.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Equal(75, (await workbench.GetStructureAsync(project)).Nodes.Single(node => node.Id == note.Id).ProgressPercent);
        if (retireView) {
            Assert.DoesNotContain(page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes, node => node.Id == note.Id);
            Assert.DoesNotContain("Updated 1 original node", page.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain((await workbench.GetStructureAsync(nextProject)).Nodes, node => !node.IsSystemManaged);
            Assert.Empty(services.GetRequiredService<NotificationService>().Messages);
        } else {
            Assert.Equal(note.ProgressPercent, page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes.Single(node => node.Id == note.Id).ProgressPercent);
            Assert.Contains("Updated 1 original node", page.Markup, StringComparison.Ordinal);
            Assert.Contains("do not repeat the write", page.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("not saved", page.Markup, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(services.GetRequiredService<NotificationService>().Messages, message =>
                message.Summary == "Structure changes saved; refresh required" && message.Severity == NotificationSeverity.Warning);
        }
    }

    private static async Task<string> CaptureAsync(ProjectWorkbenchService workbench, Guid project) {
        var surface = await workbench.GetStructureAsync(project);
        return JsonSerializer.Serialize(surface.Nodes.Where(node => !node.IsSystemManaged).OrderBy(node => node.Id, StringComparer.Ordinal));
    }

    private sealed class RevokeContentAfterSql(EditingUiTestAccess access) : SaveChangesInterceptor {
        public Guid Project { get; set; }
        public bool Armed { get; set; }
        public bool Reached { get; private set; }

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) {
            if (Armed && result > 0 && eventData.Context is WorkbenchDbContext context &&
                    context.ChangeTracker.Entries<ProjectObjectRecord>().Any(entry => entry.Entity.ProjectId == Project)) {
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

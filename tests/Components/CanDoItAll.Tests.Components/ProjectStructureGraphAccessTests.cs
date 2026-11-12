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
using CanDoItAll.Workbench.Structure.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProjectStructure;

[Trait("Category", "HostPlatform")]
public sealed class ProjectStructureGraphAccessTests {
    public enum Command { Move, Link, Unlink, Reconnect, Copy, Cut, Convert, Recompose }
    public enum AccessCondition { Allowed, DeniedBefore, DeniedAfterSql }

    public static IEnumerable<object[]> Commands => Enum.GetValues<Command>()
        .SelectMany(command => Enum.GetValues<AccessCondition>().Select(condition => new object[] { command, condition }));

    [Theory]
    [MemberData(nameof(Commands))]
    public async Task Actual_graph_commands_require_current_access_at_their_native_physical_commit(Command command, AccessCondition condition) {
        var access = new EditingUiTestAccess();
        var probe = new RevokeAfterGraphSql(access);
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IEditingUiAccess>(access);
            services.ConfigureDbContext<WorkbenchDbContext>(options => options.AddInterceptors(probe), ServiceLifetime.Singleton);
        });
        harness.Context.JSInterop.Setup<bool>("CanDoItAll.canvasWorkbench.create", _ => true).SetResult(true);
        var services = harness.Context.Services;
        var project = (await services.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = "Graph command authority" })).Value;
        probe.Project = project;
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var admission = (await workbench.GetStructureAsync(project)).ExpectedProjectAdmission!;
        var root = $"project:{project:D}";
        var source = await workbench.CreateObjectAsync(project, new(ProjectObjectType.Note, "Graph source", "", "Source notes retained", root, 100, 100) {
            ExpectedProjectAdmission = admission
        });
        var child = await workbench.CreateObjectAsync(project, new(ProjectObjectType.Note, "Graph child", "", "Child notes retained", source.Id, 160, 600) {
            ExpectedProjectAdmission = admission
        });
        var target = await workbench.CreateObjectAsync(project, new(ProjectObjectType.Note, "Graph target", "", "Target notes retained", root, 750, 100) {
            ExpectedProjectAdmission = admission
        });
        var neighbor = await workbench.CreateObjectAsync(project, new(ProjectObjectType.Note, "Graph neighbor", "", "Untouched neighbor", root, 1100, 350) {
            ExpectedProjectAdmission = admission
        });
        if (command == Command.Unlink) {
            await workbench.LinkObjectsAsync(project, source.Id, target.Id, ProjectObjectLinkKind.DependsOn, expectedProjectAdmission: admission);
        }
        services.GetRequiredService<NavigationManager>().NavigateTo($"/projects/{project:D}/structure");
        var page = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(component => component.ProjectId, project));
        page.WaitForAssertion(() => Assert.Contains(page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes, node => node.Id == source.Id));
        var canvas = page.FindComponent<CanvasWorkbench>().Instance;
        await page.InvokeAsync(() => canvas.OnSelectionChanged(source.Id, JsonSerializer.Serialize(new[] { source.Id })));
        Func<Task> invoke;
        switch (command) {
            case Command.Move:
                invoke = () => canvas.OnNodesMoved(JsonSerializer.Serialize<IReadOnlyList<CanvasWorkbenchNodePositionChange>>([new(source.Id, 400, -42.5)]));
                break;
            case Command.Link:
            case Command.Reconnect:
                await page.InvokeAsync(() => canvas.OnContextAction(source.Id, command == Command.Link ? "connect" : "reconnect", 100, 100));
                invoke = command == Command.Link
                    ? () => canvas.OnContextActionRequest(JsonSerializer.Serialize(new CanvasWorkbenchContextActionRequest(
                        target.Id, "dependency:create", target.X, target.Y, "node", source.Id, target.Id)))
                    : () => canvas.OnSelectionChanged(target.Id, JsonSerializer.Serialize(new[] { target.Id }));
                break;
            case Command.Unlink:
                invoke = () => canvas.OnContextActionRequest(JsonSerializer.Serialize(new CanvasWorkbenchContextActionRequest(
                    target.Id, "delete-link", 0, 0, "link", source.Id, target.Id, ProjectObjectLinkKind.DependsOn.ToString())));
                break;
            case Command.Copy:
            case Command.Cut:
                await page.InvokeAsync(() => ClipboardAsync(page, command == Command.Copy ? CanvasWorkbenchClipboardAction.Copy : CanvasWorkbenchClipboardAction.Cut, source.Id));
                invoke = () => ClipboardAsync(page, CanvasWorkbenchClipboardAction.Paste, target.Id);
                break;
            case Command.Convert:
                await page.InvokeAsync(() => canvas.OnContextAction(source.Id, "note:convert-to-block", 100, 100));
                page.WaitForElement("[data-testid='project-structure-block-mutation-submit']");
                invoke = () => page.Find("[data-testid='project-structure-block-mutation-submit']").ClickAsync(new MouseEventArgs());
                break;
            case Command.Recompose:
                invoke = () => page.FindComponent<ProjectStructureToolbarActions>().Instance.RecomposeBranch.InvokeAsync();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(command));
        }
        var snapshots = services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>();
        var before = await CaptureAsync(snapshots, project);
        probe.Armed = condition == AccessCondition.DeniedAfterSql;
        access.Allowed = condition != AccessCondition.DeniedBefore;
        await page.InvokeAsync(invoke);
        var after = await CaptureAsync(snapshots, project);
        if (condition != AccessCondition.Allowed) {
            Assert.Equal(condition == AccessCondition.DeniedAfterSql, probe.Reached);
            Assert.Equal(before, after);
            Assert.Equal(ProjectStructureAuthoringResultKind.Rejected, page.Instance.AuthoringOutcomes.Last().Kind);
            Assert.Contains(services.GetRequiredService<NotificationService>().Messages, message =>
                message.Summary == "Structure changes not saved" && message.Severity == NotificationSeverity.Warning);
            return;
        }
        Assert.NotEqual(before, after);
        var accepted = await workbench.GetStructureAsync(project);
        Assert.Equal(JsonSerializer.Serialize(neighbor), JsonSerializer.Serialize(accepted.Nodes.Single(node => node.Id == neighbor.Id)));
        var saved = accepted.Nodes.Single(node => node.Id == source.Id);
        switch (command) {
            case Command.Move:
                Assert.Equal(400, saved.X);
                Assert.Equal(-42.5, saved.Y);
                break;
            case Command.Link:
                Assert.Contains(accepted.Links, link => link.SourceId == source.Id && link.TargetId == target.Id && link.Kind == ProjectObjectLinkKind.DependsOn);
                break;
            case Command.Unlink:
                Assert.DoesNotContain(accepted.Links, link => link.SourceId == source.Id && link.TargetId == target.Id && link.Kind == ProjectObjectLinkKind.DependsOn);
                break;
            case Command.Reconnect:
            case Command.Cut:
                Assert.Equal(target.Id, saved.ParentId);
                Assert.Equal(source.Id, accepted.Nodes.Single(node => node.Id == child.Id).ParentId);
                break;
            case Command.Copy:
                var copy = Assert.IsType<ProjectStructureClipboardCopyResult>(page.Instance.AuthoringOutcomes.Last().ClipboardCopy);
                Assert.Equal(2, copy.NodeIdMap.Count);
                Assert.Equal(target.Id, accepted.Nodes.Single(node => node.Id == Assert.Single(copy.RootNodeIds)).ParentId);
                Assert.Equal(source.Notes, saved.Notes);
                Assert.Equal(6, accepted.Nodes.Count(node => !node.IsSystemManaged));
                break;
            case Command.Convert:
                Assert.NotEqual(ProjectObjectType.Note, saved.ObjectType);
                Assert.Equal(source.Notes, saved.Notes);
                Assert.Equal(source.Id, accepted.Nodes.Single(node => node.Id == child.Id).ParentId);
                break;
            case Command.Recompose:
                var repositioned = accepted.Nodes.Single(node => node.Id == child.Id);
                Assert.NotEqual((child.X, child.Y), (repositioned.X, repositioned.Y));
                Assert.Equal(source.Id, repositioned.ParentId);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(command));
        }
        Assert.Equal(ProjectStructureAuthoringResultKind.Committed, page.Instance.AuthoringOutcomes.Last().Kind);
    }

    private static Task ClipboardAsync(IRenderedComponent<ProjectStructurePage> page, CanvasWorkbenchClipboardAction action, string node)
        => page.FindComponent<CanvasWorkbench>().Instance.OnClipboardAction(JsonSerializer.Serialize(new CanvasWorkbenchClipboardRequest(
            action, page.FindComponent<CanvasWorkbench>().Instance.Surface.SurfaceId, node, [node])));

    [Fact]
    public async Task Physically_committed_conversion_withholds_the_returned_node_after_access_is_revoked() {
        var access = new EditingUiTestAccess();
        var hold = new HoldCommittedConversion();
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IEditingUiAccess>(access);
            services.ConfigureDbContext<WorkbenchDbContext>(options => options.AddInterceptors(hold), ServiceLifetime.Singleton);
        });
        harness.Context.JSInterop.Setup<bool>("CanDoItAll.canvasWorkbench.create", _ => true).SetResult(true);
        var services = harness.Context.Services;
        var project = (await services.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = "Confirmed private conversion" })).Value;
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var admission = (await workbench.GetStructureAsync(project)).ExpectedProjectAdmission!;
        var note = await workbench.CreateObjectAsync(project, new(ProjectObjectType.Note, "Original conversion", "", "Original conversion notes", $"project:{project:D}") {
            ExpectedProjectAdmission = admission
        });
        services.GetRequiredService<NavigationManager>().NavigateTo($"/projects/{project:D}/structure");
        var page = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(component => component.ProjectId, project));
        page.WaitForElement("[data-testid='project-structure-canvas-loaded']");
        await page.InvokeAsync(() => page.FindComponent<CanvasWorkbench>().Instance.OnContextAction(note.Id, "note:convert-to-block", 100, 100));
        page.WaitForElement("[data-testid='project-structure-block-mutation-submit']");
        var retainedNode = JsonSerializer.Serialize(page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes.Single(node => node.Id == note.Id));
        hold.Project = project;
        hold.Armed = true;
        var pending = page.InvokeAsync(() => page.Find("[data-testid='project-structure-block-mutation-submit']").ClickAsync(new MouseEventArgs()));
        try {
            await hold.Committed.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.NotEqual(ProjectObjectType.Note, (await workbench.GetStructureAsync(project)).Nodes.Single(node => node.Id == note.Id).ObjectType);
            access.Allowed = false;
        } finally {
            hold.Release.TrySetResult();
        }
        await pending.WaitAsync(TimeSpan.FromSeconds(20));
        var accepted = (await workbench.GetStructureAsync(project)).Nodes.Single(node => node.Id == note.Id);
        Assert.NotEqual(ProjectObjectType.Note, accepted.ObjectType);
        Assert.Equal(note.Notes, accepted.Notes);
        Assert.Equal(retainedNode, JsonSerializer.Serialize(page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes.Single(node => node.Id == note.Id)));
        Assert.Equal(ProjectStructureAuthoringResultKind.Committed, page.Instance.AuthoringOutcomes.Last().Kind);
        Assert.Contains(services.GetRequiredService<NotificationService>().Messages, message => message.Summary == "Structure changes saved; refresh required");
    }

    [Fact]
    public async Task Retiring_the_original_graph_after_actual_SQL_rejects_the_uncommitted_move_without_notifying_the_successor() {
        var hold = new HoldMovedSql();
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.ConfigureDbContext<WorkbenchDbContext>(options => options.AddInterceptors(hold), ServiceLifetime.Singleton));
        harness.Context.JSInterop.Setup<bool>("CanDoItAll.canvasWorkbench.create", _ => true).SetResult(true);
        var services = harness.Context.Services;
        var projects = services.GetRequiredService<ProjectsService>();
        var project = (await projects.SaveAsync(new() { Name = "Uncommitted original graph" })).Value;
        var next = (await projects.SaveAsync(new() { Name = "Successor graph" })).Value;
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var admission = (await workbench.GetStructureAsync(project)).ExpectedProjectAdmission!;
        var note = await workbench.CreateObjectAsync(project, new(ProjectObjectType.Note, "Original move", "", "Retained", $"project:{project:D}", 100, 100) {
            ExpectedProjectAdmission = admission
        });
        services.GetRequiredService<NavigationManager>().NavigateTo($"/projects/{project:D}/structure");
        var page = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(component => component.ProjectId, project));
        page.WaitForElement("[data-testid='project-structure-canvas-loaded']");
        var snapshots = services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>();
        var before = await CaptureAsync(snapshots, project);
        hold.Project = project;
        hold.Armed = true;
        var pending = page.InvokeAsync(() => page.FindComponent<CanvasWorkbench>().Instance.OnNodesMoved(
            JsonSerializer.Serialize<IReadOnlyList<CanvasWorkbenchNodePositionChange>>([new(note.Id, 400, -42.5)])));
        try {
            await hold.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            services.GetRequiredService<NavigationManager>().NavigateTo($"/projects/{next:D}/structure");
            await page.InvokeAsync(() => page.Render(parameters => parameters.Add(component => component.ProjectId, next)));
            page.WaitForAssertion(() => Assert.Contains(page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes, node => node.Id == $"project:{next:D}"));
        } finally {
            hold.Release.TrySetResult();
        }
        await pending.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Equal(before, await CaptureAsync(snapshots, project));
        Assert.DoesNotContain((await workbench.GetStructureAsync(next)).Nodes, node => !node.IsSystemManaged);
        Assert.Equal(ProjectStructureAuthoringResultKind.Rejected, page.Instance.AuthoringOutcomes.Last().Kind);
        Assert.DoesNotContain(services.GetRequiredService<NotificationService>().Messages, message => message.Summary.StartsWith("Structure", StringComparison.Ordinal));
        Assert.DoesNotContain("original movement was rejected", page.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Graph_projection_reads_leave_the_complete_native_node_and_link_rows_unchanged() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var services = harness.Context.Services;
        var project = (await services.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = "Canonical graph read stability" })).Value;
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var admission = (await workbench.GetStructureAsync(project)).ExpectedProjectAdmission!;
        await workbench.CreateObjectAsync(project, new(ProjectObjectType.Note, "Stable native note", "", "Retained", $"project:{project:D}") {
            ExpectedProjectAdmission = admission
        });
        var snapshots = services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>();
        var before = await CaptureAsync(snapshots, project);
        await workbench.GetStructureAsync(project);
        await workbench.GetStructureAsync(project);
        Assert.Equal(before, await CaptureAsync(snapshots, project));
    }

    private sealed class HoldCommittedConversion : DbTransactionInterceptor {
        public Guid Project { get; set; }
        public bool Armed { get; set; }
        public TaskCompletionSource Committed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default) {
            if (Armed && eventData.Context is WorkbenchDbContext context && context.ChangeTracker.Entries<ProjectObjectRecord>()
                    .Any(entry => entry.Entity.ProjectId == Project && entry.Entity.ObjectType != ProjectObjectType.Note)) {
                Armed = false;
                Committed.TrySetResult();
                await Release.Task;
            }
        }
    }

    private sealed class HoldMovedSql : SaveChangesInterceptor {
        public Guid Project { get; set; }
        public bool Armed { get; set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) {
            if (Armed && result > 0 && eventData.Context is WorkbenchDbContext context && context.ChangeTracker.Entries<ProjectObjectRecord>()
                    .Any(entry => entry.Entity.ProjectId == Project && entry.Entity.PositionX == 400)) {
                Assert.NotNull(context.Database.CurrentTransaction);
                Armed = false;
                Entered.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }

    private static async Task<string> CaptureAsync(IDbContextFactory<WorkbenchDbContext> snapshots, Guid project) {
        await using var context = await snapshots.CreateDbContextAsync();
        var nodes = await context.Set<ProjectObjectRecord>().AsNoTracking()
            .Where(node => node.ProjectId == project && !node.IsSystemManaged).OrderBy(node => node.NodeKey).ToListAsync();
        var links = await context.Set<ProjectObjectLinkRecord>().AsNoTracking()
            .Where(link => link.ProjectId == project).OrderBy(link => link.Id).ToListAsync();
        return JsonSerializer.Serialize(new {
            Nodes = nodes,
            Links = links
        });
    }

    private sealed class RevokeAfterGraphSql(EditingUiTestAccess access) : SaveChangesInterceptor {
        private readonly HashSet<DbContext> writing = [];
        public Guid Project { get; set; }
        public bool Armed { get; set; }
        public bool Reached { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (Armed && eventData.Context is WorkbenchDbContext context &&
                    (context.ChangeTracker.Entries<ProjectObjectRecord>().Any(entry => entry.Entity.ProjectId == Project) ||
                     context.ChangeTracker.Entries<ProjectObjectLinkRecord>().Any(entry => entry.Entity.ProjectId == Project))) {
                writing.Add(context);
            }
            return ValueTask.FromResult(result);
        }

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) {
            if (Armed && result > 0 && eventData.Context is WorkbenchDbContext context && writing.Remove(context)) {
                Assert.NotNull(context.Database.CurrentTransaction);
                Armed = false;
                Reached = true;
                access.Allowed = false;
            }
            return ValueTask.FromResult(result);
        }
    }
}

using System.Text.Json;
using System.Data.Common;
using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using CanDoItAll.Workbench.Structure.UI;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components.Web;

namespace CanDoItAll.Tests.Components.ProjectStructure;

[Trait("Category", "HostPlatform")]
public sealed class ProjectStructureGraphLifetimeTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Held_index_close_preserves_a_later_toolbar_window_and_its_native_state(bool navigate) {
        var gate = new ViewStateWriteGate();
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IDbContextFactory<WorkbenchDbContext>>(provider => new PooledDbContextFactory<WorkbenchDbContext>(
                new DbContextOptionsBuilder<WorkbenchDbContext>(provider.GetRequiredService<DbContextOptions<WorkbenchDbContext>>())
                    .AddInterceptors(gate).Options));
        });
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbench = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var project = (await projects.SaveAsync(new() { Name = "Original window owner" })).Value;
        var next = navigate ? (await projects.SaveAsync(new() { Name = "Successor window owner" })).Value : project;
        var page = Render(harness, project);
        await page.InvokeAsync(() => page.Find("[data-testid='project-structure-object-index-toggle']").ClickAsync(new MouseEventArgs()));
        gate.Armed = true;
        var closing = page.InvokeAsync(() => page.Find("[data-testid='project-structure-object-index-window'] button[aria-label='Hide window']").ClickAsync(new MouseEventArgs()));
        Task opening = Task.CompletedTask;
        try {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            if (navigate) {
                page.Render(parameters => parameters.Add(component => component.ProjectId, next));
                page.WaitForAssertion(() => Assert.Contains(page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes, node => node.Id == $"project:{next:D}"));
            }
            opening = page.InvokeAsync(() => page.Find("[data-testid='project-structure-health-toggle']").ClickAsync(new MouseEventArgs()));
            page.WaitForElement("[data-testid='project-structure-validation-window']");
        } finally {
            gate.Release.TrySetResult();
        }
        await Task.WhenAll(closing, opening).WaitAsync(TimeSpan.FromSeconds(20));
        page.WaitForElement("[data-testid='project-structure-validation-window']");
        Assert.Empty(page.FindAll("[data-testid='project-structure-object-index-window']"));
        var visible = page.FindComponent<CanvasWorkbench>().Instance.Surface.UiState;
        var stored = CanvasWorkbenchUiState.Parse((await workbench.GetStructureAsync(next)).ViewStateJson);
        const string healthWindowKey = "project-structure.health";
        const string indexWindowKey = "project-structure.objectIndex";
        Assert.Equal(visible.WindowStates[healthWindowKey].IsVisible, stored.WindowStates[healthWindowKey].IsVisible);
        Assert.True(stored.WindowStates[healthWindowKey].IsVisible);
        var original = CanvasWorkbenchUiState.Parse((await workbench.GetStructureAsync(project)).ViewStateJson);
        Assert.False(original.WindowStates[indexWindowKey].IsVisible);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reconnect_selection_echo_publishes_the_accepted_parent_without_replacing_a_later_selection(bool laterSelection) {
        var gate = new ProjectStructureHierarchyLifetimeTests.OwnerWriteGate();
        await using var harness = await ProjectStructureHierarchyLifetimeTests.CreateHarnessAsync(gate);
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbench = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var project = (await projects.SaveAsync(new() { Name = "Reconnect selection echo" })).Value;
        var rootId = $"project:{project:D}";
        var source = await workbench.CreateObjectAsync(project, new(ProjectObjectType.Note, "Source", "", "Retained", rootId));
        var target = await workbench.CreateObjectAsync(project, new(ProjectObjectType.Note, "Target", "", "Untouched", rootId));
        var page = Render(harness, project);
        var canvas = page.FindComponent<CanvasWorkbench>().Instance;
        await page.InvokeAsync(() => canvas.OnSelectionChanged(source.Id, JsonSerializer.Serialize(new[] { source.Id })));
        await page.InvokeAsync(() => canvas.OnContextAction(source.Id, "reconnect", source.X, source.Y));
        gate.ArmNode(project, source.Id);
        var pending = page.InvokeAsync(() => canvas.OnSelectionChanged(target.Id, JsonSerializer.Serialize(new[] { target.Id })));
        try {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await page.InvokeAsync(() => canvas.OnStateChanged(new CanvasWorkbenchUiState { SelectedNodeIds = [target.Id] }.ToJson()));
            if (laterSelection) {
                await page.InvokeAsync(() => canvas.OnStateChanged(new CanvasWorkbenchUiState { SelectedNodeIds = [rootId] }.ToJson()));
            }
        } finally {
            gate.Release.TrySetResult();
        }
        await pending.WaitAsync(TimeSpan.FromSeconds(20));
        var accepted = Assert.Single(page.Instance.AuthoringOutcomes);
        Assert.Equal(ProjectStructureAuthoringResultKind.Committed, accepted.Kind);
        Assert.Equal(target.Id, accepted.Node!.ParentId);
        Assert.Equal(target.Id, Assert.Single((await workbench.GetStructureAsync(project)).Nodes, node => node.Id == source.Id).ParentId);
        var presented = page.FindComponent<CanvasWorkbench>().Instance.Surface;
        Assert.Equal(new[] { laterSelection ? rootId : target.Id }, presented.UiState.SelectedNodeIds);
        if (!laterSelection) {
            Assert.Equal(target.Id, Assert.Single(presented.Nodes, node => node.Id == source.Id).ParentId);
        }
    }

    [Fact]
    public async Task Paste_publishes_accepted_copies_and_their_selection_into_the_actual_canvas() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbench = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var project = (await projects.SaveAsync(new() { Name = "Clipboard readback" })).Value;
        var root = await workbench.CreateObjectAsync(project, new(ProjectObjectType.Note, "Source", "", "Retained", $"project:{project:D}"));
        var target = await workbench.CreateObjectAsync(project, new(ProjectObjectType.Note, "Target", "", "Untouched", $"project:{project:D}"));
        var page = Render(harness, project);
        await ClipboardAsync(page, CanvasWorkbenchClipboardAction.Copy, [root.Id]);
        await ClipboardAsync(page, CanvasWorkbenchClipboardAction.Paste, [target.Id]);
        var outcome = Assert.Single(page.Instance.AuthoringOutcomes);
        var copied = Assert.Single(outcome.NodeIds);
        var canvas = page.FindComponent<CanvasWorkbench>().Instance.Surface;
        Assert.Equal(target.Id, Assert.Single(canvas.Nodes, node => node.Id == copied).ParentId);
        Assert.Equal(new[] { copied }, canvas.UiState.SelectedNodeIds);
        Assert.Contains(canvas.Nodes, node => node.Id == root.Id);
    }

    [Fact]
    public async Task Transfer_refuses_a_reclassified_root_and_compensates_only_its_created_child() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbench = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var project = (await projects.SaveAsync(new() { Name = "Original transfer root" })).Value;
        var root = await workbench.CreateObjectAsync(project, new(ProjectObjectType.Note, "Original", "", "Retained", $"project:{project:D}"));
        var child = await workbench.CreateObjectAsync(project, new(ProjectObjectType.Note, "Descendant", "", "Untouched", root.Id));
        var page = Render(harness, project);
        await page.InvokeAsync(() => page.FindComponent<CanvasWorkbench>().Instance.OnContextAction(root.Id, "move-descendants-to-subproject", root.X, root.Y));
        await workbench.ReclassifyObjectAsync(project, root.Id,
            new(ProjectObjectType.ProjectBlock, "decision", "Changed root", "", root.Notes));
        await page.InvokeAsync(() => page.FindComponent<StructureStructuralDialogs>().Instance.ExecuteSubprojectTransfer.InvokeAsync());
        var outcome = Assert.Single(page.Instance.AuthoringOutcomes);
        Assert.Equal(ProjectStructureAuthoringResultKind.Compensated, outcome.Kind);
        Assert.NotNull(outcome.Creation);
        Assert.Equal(root.Id, outcome.SourceNodeId);
        var persisted = await workbench.GetStructureAsync(project);
        Assert.Equal(root.Id, Assert.Single(persisted.Nodes, node => node.Id == child.Id).ParentId);
        Assert.Equal("Changed root", Assert.Single(persisted.Nodes, node => node.Id == root.Id).Title);
        Assert.DoesNotContain(await projects.ListHierarchyLinksAsync(), link => link.ChildProjectId == outcome.TargetProjectId);
    }

    [Theory]
    [InlineData(CanvasWorkbenchClipboardAction.Copy, false)]
    [InlineData(CanvasWorkbenchClipboardAction.Cut, false)]
    [InlineData(CanvasWorkbenchClipboardAction.Copy, true)]
    [InlineData(CanvasWorkbenchClipboardAction.Cut, true)]
    public async Task Captured_clipboard_refuses_a_reclassified_original_root(CanvasWorkbenchClipboardAction action, bool changeDestination) {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbench = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var project = (await projects.SaveAsync(new() { Name = "Original clipboard root" })).Value;
        var root = await workbench.CreateObjectAsync(project, new(ProjectObjectType.Note, "Original", "", "Retained", $"project:{project:D}"));
        var destination = await workbench.CreateObjectAsync(project, new(ProjectObjectType.Note, "Destination", "", "Untouched", $"project:{project:D}"));
        var page = Render(harness, project);
        await ClipboardAsync(page, action, [root.Id]);
        await workbench.ReclassifyObjectAsync(project, changeDestination ? destination.Id : root.Id,
            new(ProjectObjectType.ProjectBlock, "decision", "Reclassified", "", changeDestination ? destination.Notes : root.Notes));
        await ClipboardAsync(page, CanvasWorkbenchClipboardAction.Paste, [destination.Id]);
        var persisted = await workbench.GetStructureAsync(project);
        Assert.Equal(root.ParentId, Assert.Single(persisted.Nodes, node => node.Id == root.Id).ParentId);
        Assert.Equal(new[] { root.Id, destination.Id }.Order(), persisted.Nodes.Where(node => !node.IsSystemManaged).Select(node => node.Id).Order());
        Assert.Equal(destination.Notes, Assert.Single(persisted.Nodes, node => node.Id == destination.Id).Notes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Clipboard_preserves_original_map_and_outcome_after_buffer_replacement(bool cut, bool loseReply) {
        var gate = new ProjectStructureHierarchyLifetimeTests.OwnerWriteGate { LoseCommitReply = loseReply };
        await using var harness = await ProjectStructureHierarchyLifetimeTests.CreateHarnessAsync(gate);
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbench = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var project = (await projects.SaveAsync(new() { Name = "Held clipboard graph" })).Value;
        var root = await workbench.CreateObjectAsync(project, new(ProjectObjectType.Note, "Original root", "", "Retained root", $"project:{project:D}"));
        var child = await workbench.CreateObjectAsync(project, new(ProjectObjectType.Note, "Original child", "", "Retained child", root.Id));
        var target = await workbench.CreateObjectAsync(project, new(ProjectObjectType.Note, "Destination", "", "Untouched", $"project:{project:D}"));
        await workbench.LinkObjectsAsync(project, root.Id, child.Id, ProjectObjectLinkKind.DependsOn);
        await workbench.LinkObjectsAsync(project, child.Id, target.Id, ProjectObjectLinkKind.DependsOn);
        var page = Render(harness, project);
        var originalSelection = page.FindComponent<CanvasWorkbench>().Instance.Surface.UiState.SelectedNodeIds.ToArray();
        await ClipboardAsync(page, cut ? CanvasWorkbenchClipboardAction.Cut : CanvasWorkbenchClipboardAction.Copy, [root.Id, child.Id]);
        if (cut) {
            gate.ArmNode(project, root.Id);
        } else {
            gate.ArmCreatedNodes(project);
        }
        var pending = ClipboardAsync(page, CanvasWorkbenchClipboardAction.Paste, [target.Id]);
        try {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await ClipboardAsync(page, CanvasWorkbenchClipboardAction.Paste, [target.Id]);
            await ClipboardAsync(page, CanvasWorkbenchClipboardAction.Copy, [target.Id]);
        } finally {
            gate.Release.TrySetResult();
        }
        await pending.WaitAsync(TimeSpan.FromSeconds(20));
        var outcome = Assert.Single(page.Instance.AuthoringOutcomes);
        Assert.Equal(loseReply ? ProjectStructureAuthoringResultKind.Unconfirmed : ProjectStructureAuthoringResultKind.Committed, outcome.Kind);
        Assert.Equal(root.Id, outcome.SourceNodeId);
        Assert.Equal(originalSelection, page.FindComponent<CanvasWorkbench>().Instance.Surface.UiState.SelectedNodeIds);
        var persisted = await workbench.GetStructureAsync(project);
        Assert.Equal(target.Notes, Assert.Single(persisted.Nodes, node => node.Id == target.Id).Notes);
        if (cut) {
            Assert.Equal(target.Id, Assert.Single(persisted.Nodes, node => node.Id == root.Id).ParentId);
            Assert.Equal(root.Id, Assert.Single(persisted.Nodes, node => node.Id == child.Id).ParentId);
            Assert.Null(outcome.ClipboardCopy);
        } else {
            Assert.Equal(2, persisted.Nodes.Count(node => node.Title == root.Title || node.Title == child.Title) - 2);
            if (!loseReply) {
                var copy = Assert.IsType<ProjectStructureClipboardCopyResult>(outcome.ClipboardCopy);
                Assert.Equal(2, copy.NodeIdMap.Count);
                Assert.Single(copy.OmittedBoundaryLinks);
                var copiedRoot = Assert.Single(persisted.Nodes, node => node.Id == copy.NodeIdMap[root.Id]);
                var copiedChild = Assert.Single(persisted.Nodes, node => node.Id == copy.NodeIdMap[child.Id]);
                Assert.Equal(target.Id, copiedRoot.ParentId);
                Assert.Equal(copiedRoot.Id, copiedChild.ParentId);
                Assert.Equal(child.Notes, copiedChild.Notes);
                Assert.Contains(persisted.Links, link => link.SourceId == copiedRoot.Id && link.TargetId == copiedChild.Id && link.Kind == ProjectObjectLinkKind.DependsOn);
                Assert.DoesNotContain(persisted.Links, link => link.SourceId == copiedChild.Id && link.TargetId == target.Id);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Accepted_movement_retains_original_positions_after_navigation(bool loseReply) {
        var gate = new ProjectStructureHierarchyLifetimeTests.OwnerWriteGate { LoseCommitReply = loseReply };
        await using var harness = await ProjectStructureHierarchyLifetimeTests.CreateHarnessAsync(gate);
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbench = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var project = (await projects.SaveAsync(new() { Name = "Original move owner" })).Value;
        var next = (await projects.SaveAsync(new() { Name = "Successor move owner" })).Value;
        var note = await workbench.CreateObjectAsync(project, new(ProjectObjectType.Note, "Moving", "", "Retained", $"project:{project:D}"));
        var page = Render(harness, project);
        var canvas = page.FindComponent<CanvasWorkbench>();
        gate.ArmNode(project, note.Id);
        var pending = page.InvokeAsync(() => canvas.Instance.OnNodesMoved(JsonSerializer.Serialize<IReadOnlyList<CanvasWorkbenchNodePositionChange>>([new(note.Id, 0, -42.5)])));
        try {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            page.Render(parameters => parameters.Add(component => component.ProjectId, next));
            page.WaitForAssertion(() => Assert.Contains(page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes, node => node.Id == $"project:{next:D}"));
        } finally {
            gate.Release.TrySetResult();
        }
        await pending.WaitAsync(TimeSpan.FromSeconds(20));
        var outcome = Assert.Single(page.Instance.AuthoringOutcomes);
        Assert.Equal(project, outcome.Project.ProjectId);
        Assert.Equal(loseReply ? ProjectStructureAuthoringResultKind.Unconfirmed : ProjectStructureAuthoringResultKind.Committed, outcome.Kind);
        var stored = Assert.Single((await workbench.GetStructureAsync(project)).Nodes, node => node.Id == note.Id);
        Assert.Equal(0, stored.X);
        Assert.Equal(-42.5, stored.Y);
        Assert.DoesNotContain(page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes, node => node.Id == note.Id);
    }

    [Fact]
    public async Task Exact_dependency_delete_refuses_a_replacement_link_with_the_same_endpoints() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbench = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var project = (await projects.SaveAsync(new() { Name = "Original dependency identity" })).Value;
        var first = await workbench.CreateObjectAsync(project, new(ProjectObjectType.Note, "First", "", "", $"project:{project:D}"));
        var second = await workbench.CreateObjectAsync(project, new(ProjectObjectType.Note, "Second", "", "", $"project:{project:D}"));
        await workbench.LinkObjectsAsync(project, first.Id, second.Id, ProjectObjectLinkKind.DependsOn);
        var page = Render(harness, project);
        var original = Assert.Single((await workbench.GetStructureAsync(project)).Links, link => link.Kind == ProjectObjectLinkKind.DependsOn);
        Assert.True(await workbench.UnlinkObjectsAsync(project, first.Id, second.Id, ProjectObjectLinkKind.DependsOn));
        await workbench.LinkObjectsAsync(project, first.Id, second.Id, ProjectObjectLinkKind.DependsOn);
        var replacement = Assert.Single((await workbench.GetStructureAsync(project)).Links, link => link.Kind == ProjectObjectLinkKind.DependsOn);
        Assert.NotEqual(original.RecordId, replacement.RecordId);
        await page.InvokeAsync(() => page.FindComponent<CanvasWorkbench>().Instance.OnContextActionRequest(JsonSerializer.Serialize(
            new CanvasWorkbenchContextActionRequest(second.Id, "delete-link", 0, 0, "link", first.Id, second.Id, ProjectObjectLinkKind.DependsOn.ToString()))));
        Assert.Equal(replacement.RecordId, Assert.Single((await workbench.GetStructureAsync(project)).Links, link => link.Kind == ProjectObjectLinkKind.DependsOn).RecordId);
        Assert.Equal(ProjectStructureAuthoringResultKind.Rejected, Assert.Single(page.Instance.AuthoringOutcomes).Kind);
    }

    [Fact]
    public async Task Clipboard_cannot_copy_reused_node_keys_from_a_recreated_project() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbench = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var project = (await projects.SaveAsync(new() { Name = "Original clipboard lifetime" })).Value;
        var root = await workbench.CreateObjectAsync(project, new(ProjectObjectType.Note, "Original", "", "Original", $"project:{project:D}"));
        var page = Render(harness, project);
        await ClipboardAsync(page, CanvasWorkbenchClipboardAction.Copy, [root.Id]);
        await projects.DeleteAsync(project, expectedProjectAdmission: (await projects.GetAsync(project)).ExpectedProjectAdmission);
        Assert.True((await projects.CreateAsync(project, new() { Name = "Replacement clipboard lifetime" })).IsSuccess);
        var replacement = await workbench.CreateObjectAsync(project, new(ProjectObjectType.Note, "Replacement", "", "Replacement", $"project:{project:D}"));
        await using (var db = await harness.Context.Services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync()) {
            var record = await db.Set<ProjectObjectRecord>().SingleAsync(node => node.ProjectId == project && node.NodeKey == replacement.Id);
            record.NodeKey = root.Id;
            await db.SaveChangesAsync();
        }
        await ClipboardAsync(page, CanvasWorkbenchClipboardAction.Paste, [$"project:{project:D}"]);
        var persisted = await workbench.GetStructureAsync(project);
        Assert.Single(persisted.Nodes, node => !node.IsSystemManaged);
        Assert.Equal("Replacement", Assert.Single(persisted.Nodes, node => node.Id == root.Id).Title);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Accepted_layout_writes_remain_ordered_and_bound_to_their_original_project(bool navigate) {
        var gate = new ViewStateWriteGate();
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IDbContextFactory<WorkbenchDbContext>>(provider => new PooledDbContextFactory<WorkbenchDbContext>(
                new DbContextOptionsBuilder<WorkbenchDbContext>(provider.GetRequiredService<DbContextOptions<WorkbenchDbContext>>())
                    .AddInterceptors(gate).Options));
        });
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbench = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var project = (await projects.SaveAsync(new() { Name = "Original layout owner" })).Value;
        var next = navigate ? (await projects.SaveAsync(new() { Name = "Successor layout owner" })).Value : project;
        var page = Render(harness, project);
        gate.Armed = true;
        await page.InvokeAsync(() => page.FindComponent<CanvasWorkbench>().Instance.OnStateChanged(new CanvasWorkbenchUiState { Zoom = 1.37 }.ToJson()));
        try {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            if (navigate) {
                page.Render(parameters => parameters.Add(component => component.ProjectId, next));
                page.WaitForAssertion(() => Assert.Contains(page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes, node => node.Id == $"project:{next:D}"));
            }
            await page.InvokeAsync(() => page.FindComponent<CanvasWorkbench>().Instance.OnStateChanged(new CanvasWorkbenchUiState { Zoom = 1.91 }.ToJson()));
        } finally {
            gate.Release.TrySetResult();
        }
        await gate.SecondCommit.Task.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Equal(1.91, CanvasWorkbenchUiState.Parse((await workbench.GetStructureAsync(next)).ViewStateJson).Zoom);
        if (navigate) {
            Assert.Equal(1.37, CanvasWorkbenchUiState.Parse((await workbench.GetStructureAsync(project)).ViewStateJson).Zoom);
        }
    }

    private sealed class ViewStateWriteGate : SaveChangesInterceptor, IDbTransactionInterceptor {
        private readonly HashSet<DbContext> stateContexts = [];
        private int written;
        private int committed;
        public bool Armed { get; set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SecondCommit { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (Armed && data.Context is { } context && context.ChangeTracker.Entries<ProjectWorkbenchViewStateRecord>()
                .Any(entry => entry.State is EntityState.Added or EntityState.Modified)) {
                stateContexts.Add(context);
                if (Interlocked.Increment(ref written) == 1) {
                    Entered.TrySetResult();
                    await Release.Task.WaitAsync(cancellationToken);
                }
            }
            return result;
        }

        public Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData data, CancellationToken cancellationToken = default) {
            if (data.Context is { } context && stateContexts.Remove(context) && Interlocked.Increment(ref committed) == 2) {
                SecondCommit.TrySetResult();
            }
            return Task.CompletedTask;
        }
    }

    private static IRenderedComponent<ProjectStructurePage> Render(ComponentTestHarness harness, Guid project) {
        var page = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(component => component.ProjectId, project));
        page.WaitForElement("[data-testid='project-structure-canvas-loaded']");
        return page;
    }

    private static Task ClipboardAsync(IRenderedComponent<ProjectStructurePage> page, CanvasWorkbenchClipboardAction action, IReadOnlyList<string> nodes)
        => page.InvokeAsync(() => {
            var canvas = page.FindComponent<CanvasWorkbench>().Instance;
            return canvas.OnClipboardAction(JsonSerializer.Serialize(new CanvasWorkbenchClipboardRequest(action, canvas.Surface.SurfaceId, nodes.FirstOrDefault(), nodes)));
        });
}

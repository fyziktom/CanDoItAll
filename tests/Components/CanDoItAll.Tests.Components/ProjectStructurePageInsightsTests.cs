using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using CanDoItAll.Workbench.Insights.UI;

namespace CanDoItAll.Tests.Components.ProjectStructure;

public sealed partial class ProjectStructurePageActionLifetimeTests {
    [Fact]
    public async Task Insights_signals_persist_additive_toggle_progress_and_priority_without_changing_neighbors() {
        await using var harness = await CreateHarnessAsync();
        var original = await CreateTargetAsync(harness, "Insights signals");
        var neighbor = await Workbench(harness).CreateObjectAsync(original.ProjectId,
            new(ProjectObjectType.Note, "Untouched neighbor", "", "", $"project:{original.ProjectId}"));
        var cut = Render(harness, original.ProjectId);
        await SelectAsync(cut, original.Node.Id);
        await cut.InvokeAsync(() => cut.FindComponent<ProjectStructureToolbarActions>().Instance.ToggleSignalsWindow.InvokeAsync());
        foreach (var action in new[] { "marker:risk", "marker:idea", "marker:risk", "progress:na", "progress:started", "progress:30", "priority:2" }) {
            await cut.InvokeAsync(() => {
                var renderer = cut.FindComponent<ProjectStructureSignalsWindow>().Instance;
                Assert.Contains(renderer.Sections.SelectMany(section => section.Actions), tile => tile.ActionId == action);
                return renderer.OnApplyAction.InvokeAsync(new(renderer.Origin, renderer.SelectedNodeIds.ToArray(), action));
            });
        }
        var accepted = await Workbench(harness).GetStructureAsync(original.ProjectId);
        var changed = Assert.Single(accepted.Nodes, node => node.Id == original.Node.Id);
        Assert.Equal("idea", Assert.Single(changed.Markers).Icon);
        Assert.Equal(30, changed.ProgressPercent);
        Assert.Equal(2, changed.Priority);
        var unchanged = Assert.Single(accepted.Nodes, node => node.Id == neighbor.Id);
        Assert.Empty(unchanged.Markers);
        Assert.Equal(neighbor.ProgressPercent, unchanged.ProgressPercent);
        Assert.Equal(neighbor.Priority, unchanged.Priority);
    }

    [Fact]
    public async Task Insights_A_B_A_and_forged_target_callbacks_cannot_retarget_a_native_write() {
        await using var harness = await CreateHarnessAsync();
        var original = await CreateTargetAsync(harness, "Insights ABA");
        var neighbor = await Workbench(harness).CreateObjectAsync(original.ProjectId,
            new(ProjectObjectType.Note, "Neighbor", "", "", $"project:{original.ProjectId}"));
        var cut = Render(harness, original.ProjectId);
        await SelectAsync(cut, original.Node.Id);
        var renderer = cut.FindComponent<ProjectStructureSelectionPanel>().Instance;
        var receiver = renderer.Dispatch;
        var queued = new InsightsSelectionIntent(renderer.Origin, [original.Node.Id], new InsightsSelectionCommand.Progress(100));
        await SelectAsync(cut, neighbor.Id);
        await SelectAsync(cut, original.Node.Id);
        await cut.InvokeAsync(() => receiver.InvokeAsync(queued));
        await cut.InvokeAsync(() => {
            var current = cut.FindComponent<ProjectStructureSelectionPanel>().Instance;
            return current.Dispatch.InvokeAsync(new(current.Origin, [neighbor.Id], new InsightsSelectionCommand.Priority(6)));
        });
        var accepted = await Workbench(harness).GetStructureAsync(original.ProjectId);
        Assert.Equal(original.Node.ProgressPercent, accepted.Nodes.Single(node => node.Id == original.Node.Id).ProgressPercent);
        Assert.Equal(neighbor.Priority, accepted.Nodes.Single(node => node.Id == neighbor.Id).Priority);
        Assert.NotEqual(queued.Origin, cut.FindComponent<ProjectStructureSelectionPanel>().Instance.Origin);
    }

    [Fact]
    public async Task Insights_native_admission_rejects_recreated_project_and_reused_node() {
        await using var harness = await CreateHarnessAsync();
        var original = await CreateTargetAsync(harness, "Insights recreated");
        var cut = Render(harness, original.ProjectId);
        await SelectAsync(cut, original.Node.Id);
        var renderer = cut.FindComponent<ProjectStructureSelectionPanel>().Instance;
        var queued = new InsightsSelectionIntent(renderer.Origin, [original.Node.Id], new InsightsSelectionCommand.Progress(100));
        await RecreateProjectAsync(harness, original);
        var before = await Workbench(harness).GetStructureAsync(original.ProjectId);
        await cut.InvokeAsync(() => renderer.Dispatch.InvokeAsync(queued));
        var replacement = await Workbench(harness).GetStructureAsync(original.ProjectId);
        Assert.Equal(before.Nodes.Single(node => node.Id == original.Node.Id).ProgressPercent,
            replacement.Nodes.Single(node => node.Id == original.Node.Id).ProgressPercent);
        Assert.NotEqual(original.Admission, replacement.ExpectedProjectAdmission);
        cut.WaitForAssertion(() => Assert.Contains(original.Admission.LifetimeId.ToString("D"), cut.Markup));
    }

    [Fact]
    public async Task Insights_delete_confirmation_cannot_close_or_delete_a_successor_dialog() {
        await using var harness = await CreateHarnessAsync();
        var original = await CreateTargetAsync(harness, "Original delete", ProjectObjectType.WorkItem);
        var next = await CreateTargetAsync(harness, "Successor delete", ProjectObjectType.WorkItem);
        var cut = Render(harness, original.ProjectId);
        await SelectAsync(cut, original.Node.Id);
        await InvokeSelectionAsync(cut, new InsightsSelectionCommand.Inspector("delete"));
        var dialogs = cut.FindComponent<ProjectStructureSupportDialogs>().Instance;
        var originalPrompt = Assert.IsType<ProjectStructureDeletePrompt>(dialogs.PendingDeletePrompt);
        var confirm = dialogs.ConfirmDelete;
        var cancel = dialogs.CancelDelete;
        Navigate(harness, cut, next.ProjectId);
        await SelectAsync(cut, next.Node.Id);
        await InvokeSelectionAsync(cut, new InsightsSelectionCommand.Inspector("delete"));
        var nextPrompt = cut.FindComponent<ProjectStructureSupportDialogs>().Instance.PendingDeletePrompt;
        await cut.InvokeAsync(() => cancel.InvokeAsync(originalPrompt));
        await cut.InvokeAsync(() => confirm.InvokeAsync(new(originalPrompt, ProjectStructureManagedStorageDisposition.DeleteOwnedManagedFiles)));
        Assert.Same(nextPrompt, cut.FindComponent<ProjectStructureSupportDialogs>().Instance.PendingDeletePrompt);
        Assert.Contains((await Workbench(harness).GetStructureAsync(original.ProjectId)).Nodes, node => node.Id == original.Node.Id);
        Assert.Contains((await Workbench(harness).GetStructureAsync(next.ProjectId)).Nodes, node => node.Id == next.Node.Id);
        await cut.InvokeAsync(() => cut.FindComponent<ProjectStructureSupportDialogs>().Instance.CancelDelete.InvokeAsync(nextPrompt!));
        Assert.Null(cut.FindComponent<ProjectStructureSupportDialogs>().Instance.PendingDeletePrompt);
    }

    [Fact]
    public async Task Insights_pending_delete_surfaces_native_recreated_lifetime_denial_without_closing_the_prompt() {
        await using var harness = await CreateHarnessAsync();
        var original = await CreateTargetAsync(harness, "Pending delete lifetime", ProjectObjectType.WorkItem);
        var cut = Render(harness, original.ProjectId);
        await SelectAsync(cut, original.Node.Id);
        await InvokeSelectionAsync(cut, new InsightsSelectionCommand.Inspector("delete"));
        var dialogs = cut.FindComponent<ProjectStructureSupportDialogs>().Instance;
        var prompt = Assert.IsType<ProjectStructureDeletePrompt>(dialogs.PendingDeletePrompt);
        await RecreateProjectAsync(harness, original);
        await cut.InvokeAsync(() => dialogs.ConfirmDelete.InvokeAsync(new(prompt, ProjectStructureManagedStorageDisposition.DeleteOwnedManagedFiles)));
        Assert.Same(prompt, cut.FindComponent<ProjectStructureSupportDialogs>().Instance.PendingDeletePrompt);
        cut.WaitForAssertion(() => Assert.Contains(original.Admission.LifetimeId.ToString("D"), cut.Find("[data-testid=project-structure-delete-failure]").TextContent));
        Assert.Contains((await Workbench(harness).GetStructureAsync(original.ProjectId)).Nodes, node => node.Id == original.Node.Id);
    }

    [Fact]
    public async Task Insights_batch_status_and_border_changes_use_native_selected_nodes_and_view_state() {
        await using var harness = await CreateHarnessAsync();
        var original = await CreateTargetAsync(harness, "Insights batch");
        var second = await Workbench(harness).CreateObjectAsync(original.ProjectId,
            new(ProjectObjectType.Note, "Second selected", "", "", $"project:{original.ProjectId}"));
        var cut = Render(harness, original.ProjectId);
        var ids = new[] { original.Node.Id, second.Id };
        await cut.InvokeAsync(() => cut.FindComponent<CanvasWorkbench>().Instance.OnSelectionChanged(ids[0], System.Text.Json.JsonSerializer.Serialize(ids)));
        await InvokeSelectionAsync(cut, new InsightsSelectionCommand.Status(InsightsStatus.Done));
        await InvokeSelectionAsync(cut, new InsightsSelectionCommand.CreateBorder("Accepted group"));
        var accepted = await Workbench(harness).GetStructureAsync(original.ProjectId);
        Assert.All(accepted.Nodes.Where(node => ids.Contains(node.Id)), node => Assert.Equal("Done", node.Status));
        var frame = Assert.Single(CanvasWorkbenchUiState.Parse(accepted.ViewStateJson).GroupFrames);
        Assert.Equal(ids.Order(), frame.AnchorNodeIds.Order());
        await InvokeSelectionAsync(cut, new InsightsSelectionCommand.ClearBorders());
        Assert.Empty(CanvasWorkbenchUiState.Parse((await Workbench(harness).GetStructureAsync(original.ProjectId)).ViewStateJson).GroupFrames);
    }

    private static Task InvokeSelectionAsync(IRenderedComponent<ProjectStructurePage> cut, InsightsSelectionCommand command)
        => cut.InvokeAsync(() => {
            var renderer = cut.FindComponent<ProjectStructureSelectionPanel>().Instance;
            return renderer.Dispatch.InvokeAsync(new(renderer.Origin, renderer.State.SelectedNodes.Select(node => node.Id).ToArray(), command));
        });
}

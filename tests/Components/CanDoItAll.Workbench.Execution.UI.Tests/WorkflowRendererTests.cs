using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Workbench.Execution.UI.Workflows;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.WorkbenchExecution;

public sealed class WorkflowRendererTests {
    [Fact]
    public async Task Raw_json_and_typed_selection_reach_the_opening_owner_unchanged() {
        using var context = Context();
        var view = AddView();
        string? raw = null;
        WorkflowId? selected = null;
        var actions = AddActions() with {
            ManualInput = text => {
                raw = text;
                return Task.CompletedTask;
            },
            Select = id => {
                selected = id;
                return Task.CompletedTask;
            }
        };
        var cut = context.Render<WorkflowAddDialog>(p => p.Add(c => c.View, view).Add(c => c.Actions, actions));
        await cut.Find("[data-testid='project-structure-workflow-add-manual-json']").InputAsync("{\"unfinished\":");
        await cut.Find($"[data-testid='project-structure-workflow-add-option-{view.Options[0].WorkflowId.Value:N}']").ClickAsync(new MouseEventArgs());
        Assert.Equal("{\"unfinished\":", raw);
        Assert.Equal(view.Options[0].WorkflowId, selected);
        Assert.Contains("Retained input preview", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_retained_submit_callback_keeps_its_original_actions_after_replacement() {
        using var context = Context();
        var original = 0;
        var replacement = 0;
        var cut = context.Render<WorkflowAddDialog>(p => p.Add(c => c.View, AddView()).Add(c => c.Actions,
            AddActions() with { Submit = () => {
                original++;
                return Task.CompletedTask;
            } }));
        var oldSubmit = cut.FindComponents<Button>().Single(button => button.Instance.Text == "Add workflow").Instance.Click;
        cut.Render(p => p.Add(c => c.View, AddView()).Add(c => c.Actions,
            AddActions() with { Submit = () => {
                replacement++;
                return Task.CompletedTask;
            } }));
        await cut.InvokeAsync(() => oldSubmit.InvokeAsync(new MouseEventArgs()));
        Assert.Equal(1, original);
        Assert.Equal(0, replacement);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void A_locked_add_keeps_close_available_and_blocks_submit(bool busy, bool unknown) {
        using var context = Context();
        var cut = context.Render<WorkflowAddDialog>(p => p.Add(c => c.View, AddView() with { IsBusy = busy, RequiresObservation = unknown })
            .Add(c => c.Actions, AddActions()));
        Assert.True(cut.Find("[data-testid='project-structure-workflow-add-submit']").HasAttribute("disabled"));
        var close = cut.FindComponents<Button>().Single(button => button.Instance.Text == "Cancel");
        Assert.False(close.Instance.Disabled);
    }

    [Fact]
    public async Task Original_start_observation_keeps_the_simulation_locked_and_close_available() {
        using var context = Context();
        var observations = 0;
        var view = new WorkflowStartView(Guid.NewGuid(), "Original workflow", new("Accepted", 1, 2, "Original run"),
            [new(new("write"), "Write artifact", "Preview write", true)], WorkflowRuntimeBackendKind.InProcess,
            WorkflowRuntimeBackendKind.InProcess, [], string.Empty, false, true, "Observe the original intent");
        var cut = context.Render<WorkflowStartDialog>(p => p.Add(c => c.View, view).Add(c => c.Actions,
            new(_ => Task.CompletedTask, () => {
                observations++;
                return Task.CompletedTask;
            }, () => Task.CompletedTask)));
        Assert.True(cut.Find("[data-testid='project-structure-workflow-start-simulate-write']").HasAttribute("disabled"));
        Assert.Contains("Observe original start", cut.Markup, StringComparison.Ordinal);
        await cut.Find("[data-testid='project-structure-workflow-start-submit']").ClickAsync(new MouseEventArgs());
        Assert.Equal(1, observations);
        Assert.False(cut.FindComponents<Button>().Single(button => button.Instance.Text == "Cancel").Instance.Disabled);
    }

    private static BunitContext Context() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }

    private static WorkflowAddView AddView() {
        var id = WorkflowId.New();
        var version = WorkflowVersionId.New();
        return new(Guid.NewGuid(), "Parent", [new(id, version, "Saved Workflow", "Original saved version", WorkflowLifecycleStatus.Active,
            WorkflowRuntimeBackendKind.InProcess, true, string.Empty)], id, version,
            new(false, true, "{}", []), new([new("Retained input preview", "Parent", [new("Node", "Original node")])]), false, false, string.Empty);
    }

    private static WorkflowAddActions AddActions()
        => new(_ => Task.CompletedTask, _ => Task.CompletedTask, _ => Task.CompletedTask, _ => Task.CompletedTask,
            _ => Task.CompletedTask, _ => Task.CompletedTask, _ => Task.CompletedTask, _ => Task.CompletedTask,
            () => Task.CompletedTask, () => Task.CompletedTask);
}

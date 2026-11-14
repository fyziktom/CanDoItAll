using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.WorkflowAuthoring.UI;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.WorkflowAuthoring;

public sealed class WorkflowNodeToolbarTests {
    [Fact]
    public void Authoring_toolbar_reuses_only_the_existing_remove_command() {
        var canvas = WorkflowCanvasDefinitionMapper.BuildSurface(
            WorkflowCanvasDefinitionMapper.CreateDraft([]), [], [], [], [], new(), null);

        Assert.NotEmpty(canvas.Nodes);
        Assert.All(canvas.Nodes, node => {
            var action = Assert.Single(node.ToolbarActions);
            Assert.Equal(WorkflowCanvasDefinitionMapper.RemoveNodeActionId, action.ActionId);
            Assert.Same(node.ContextActions.Single(candidate => candidate.ActionId == action.ActionId), action);
            Assert.Equal(CanvasWorkbenchKeyboardCommand.None, action.KeyboardCommand);
        });
    }

    [Fact]
    public async Task Read_only_template_preview_does_not_advertise_authoring_toolbar_actions() {
        await using var context = new BunitContext();
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var definition = WorkflowCanvasDefinitionMapper.ToDefinition(WorkflowCanvasDefinitionMapper.CreateDraft([]));
        var component = new LlmCallComponent(WorkflowComponentId.New(), "Template component", Guid.NewGuid(),
            "fixture-model", WorkflowModality.Text, new(.2, 800, false, string.Empty), "Instructions",
            WorkflowValueShape.Text, WorkflowValueShape.Text, AgentPermissionsPolicy.Default,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var rendered = context.Render<WorkflowTemplatePreviewDialog>(parameters => parameters
            .Add(dialog => dialog.Template, new WorkflowTemplatePreviewHeader("Preview", "Read-only fixture"))
            .Add(dialog => dialog.Definition, definition)
            .Add(dialog => dialog.Component, component));

        var canvas = rendered.FindComponent<CanvasWorkbench>().Instance.Surface;
        Assert.NotEmpty(canvas.Nodes);
        Assert.All(canvas.Nodes, node => {
            Assert.True(node.IsReadOnly);
            Assert.Empty(node.ContextActions);
            Assert.Empty(node.ToolbarActions);
        });
    }
}

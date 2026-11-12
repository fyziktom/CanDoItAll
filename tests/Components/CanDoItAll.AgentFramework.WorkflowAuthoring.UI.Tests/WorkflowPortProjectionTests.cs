using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.WorkflowAuthoring.UI;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.WorkflowAuthoring;

public sealed class WorkflowPortProjectionTests {
    [Fact]
    public void Canvas_displays_native_port_identities_names_and_required_flags() {
        var original = Definition();
        var draft = WorkflowCanvasDefinitionMapper.FromDefinition(original, []);
        var canvas = WorkflowCanvasDefinitionMapper.BuildSurface(draft, [], [], [], [], new(), null);
        var start = canvas.Nodes.Single(node => node.Id == "start");
        var port = Assert.Single(start.OutputPorts);
        Assert.Equal("named-output", port.Id);
        Assert.Equal("Rich output", port.Label);
        Assert.False(port.IsRequired);
        var link = Assert.Single(canvas.Links);
        Assert.Equal("named-output", link.SourcePortId);
        Assert.Equal("named-input", link.TargetPortId);
    }

    [Fact]
    public void Editing_a_node_value_shape_does_not_replace_explicit_port_schemas() {
        var original = Definition();
        var draft = WorkflowCanvasDefinitionMapper.FromDefinition(original, []);
        draft.Nodes[0].ResultShape = WorkflowValueShape.Text;
        var saved = WorkflowCanvasDefinitionMapper.ToDefinition(draft);
        Assert.Equal(original.Graph.Nodes[0].Ports, saved.Graph.Nodes[0].Ports);
        Assert.Equal(WorkflowValueShape.Text, saved.Graph.Nodes[0].Settings.ResultShape);
    }

    [Fact]
    public async Task Canvas_connection_preserves_the_chosen_native_port_endpoints() {
        await using var context = new BunitContext();
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var original = Definition();
        original = original with { Graph = original.Graph with { Edges = [] } };
        WorkflowDefinitionSaveRequest? saved = null;
        var cut = context.Render<WorkflowCanvasSurface>(p => p.Add(x => x.Definition, original)
            .Add(x => x.DocumentOperations, new((request, _) => {
                saved = request;
                return Task.FromResult<WorkflowDefinitionSaveOutcome>(new WorkflowDefinitionSaveOutcome.Rejected(WorkflowSaveRejection.Validation));
            }, (_, _) => Task.FromResult(WorkflowValidationResult.Success)))
            .Add(x => x.BindPrompt, (_, _) => throw new InvalidOperationException())
            .Add(x => x.PreviewOperations, new(_ => WorkflowPreviewRequirements.Empty,
                _ => Task.FromResult<IReadOnlyList<WorkflowPreviewProject>>([]), (_, _, _) => throw new InvalidOperationException())));
        await cut.InvokeAsync(() => cut.FindComponent<CanvasWorkbench>().Instance.ContextActionRequested.InvokeAsync(
            new(null, "connection:create", 0, 0, "link", "start", "end", null, "named-output", "named-input")));
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-save']").ClickAsync());
        var edge = Assert.Single(saved!.Graph.Edges);
        Assert.Equal(new WorkflowPortId("named-output"), edge.SourcePortId);
        Assert.Equal(new WorkflowPortId("named-input"), edge.TargetPortId);
    }

    private static WorkflowDefinition Definition() {
        var definition = WorkflowCanvasDefinitionMapper.ToDefinition(WorkflowCanvasDefinitionMapper.CreateDraft([]));
        var shape = new WorkflowValueShape(WorkflowValueShapeKind.Json, "{\"type\":\"object\"}", "Rich shape");
        return definition with { Graph = definition.Graph with {
            Nodes = definition.Graph.Nodes.Select(node => node.Kind == WorkflowNodeKind.Start ? node with {
                Ports = [new(new("named-output"), "Rich output", WorkflowPortDirection.Output, shape, false)],
                Settings = node.Settings with { ResultShape = shape }
            } : node with { Ports = [new(new("named-input"), "Rich input", WorkflowPortDirection.Input, shape, true)] }).ToArray(),
            Edges = definition.Graph.Edges.Select(edge => edge with {
                SourcePortId = new("named-output"), TargetPortId = new("named-input")
            }).ToArray()
        } };
    }
}

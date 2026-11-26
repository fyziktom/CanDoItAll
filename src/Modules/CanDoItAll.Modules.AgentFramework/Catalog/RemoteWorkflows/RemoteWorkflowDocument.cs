using System.ComponentModel;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework;

public static class RemoteWorkflowRoutes {
    public const string Catalog = "api/shared-workflows/v1/templates";
    public static string Details(Guid id) => $"{Catalog}/{id:D}";
}

[Description("A saved workflow prepared for creating a private local draft. References to models and components are indices within this document.")]
public sealed record RemoteWorkflowDocument(
    [property: Description("Published workflow identity, current native version and catalog timestamp.")] RemoteTemplateRow Row,
    [property: Description("Workflow nodes, ports, routing and canvas layout with publisher-local bindings removed.")] WorkflowGraph Graph,
    [property: Description("Authored input descriptors and their portable defaults.")] IReadOnlyList<WorkflowInputParameterDescriptor> Inputs,
    [property: Description("LLM component templates used by the graph, in binding order.")] IReadOnlyList<RemoteWorkflowComponent> Components,
    [property: Description("Model and component indices for individual graph nodes.")] IReadOnlyList<RemoteWorkflowBinding> Bindings,
    [property: Description("The provider capabilities and public model preference required by each binding.")] IReadOnlyList<RemoteTemplateModelRequirement> Models,
    [property: Description("Publisher-assigned creation time identifying this workflow's lifetime across edits.")] DateTimeOffset CreatedAtUtc);

[Description("The portable instructions and IO contract of a localizable workflow LLM component.")]
public sealed record RemoteWorkflowComponent(
    [property: Description("Display name of the component in the receiving library.")] string Name,
    [property: Description("Input modality used to select a compatible local model, 0 Text, 1 Vision, 2 Audio, 3 Image or 4 Multimodal.")] WorkflowModality Modality,
    [property: Description("Authored parameters for generating the model response.")] WorkflowModelSettings ModelSettings,
    [property: Description("Versioned prompt content used by the component.")] string Instructions,
    [property: Description("Shape accepted by the component.")] WorkflowValueShape InputShape,
    [property: Description("Shape produced by the component.")] WorkflowValueShape ResultShape,
    [property: Description("Zero-based index of the component's model requirement.")] int ModelIndex);

[Description("The portable component and model used by a particular graph node.")]
public sealed record RemoteWorkflowBinding(
    [property: Description("Graph-scoped identifier of the relevant node.")] WorkflowNodeId NodeId,
    [property: Description("Zero-based component index for LLM calls, or null for a model-based executor.")] int? ComponentIndex,
    [property: Description("Zero-based index of the effective model used by the node.")] int ModelIndex);

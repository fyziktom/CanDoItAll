using CanDoItAll.AgentFramework.Models;
using CanDoItAll.RemoteInstances;

namespace CanDoItAll.Modules.AgentFramework;

internal static class RemoteWorkflowProjection {
    internal static bool ValidRow(RemoteTemplateRow? row) => row is not null && row.Id != Guid.Empty && row.PublisherId != Guid.Empty &&
        row.VersionId.GetValueOrDefault() != Guid.Empty && row.UpdatedAtUtc != default && !string.IsNullOrWhiteSpace(row.Name) &&
        row.Name.Length <= WorkflowDefinitionPersistenceConstraints.NameLength && row.Summary?.Length <= WorkflowDefinitionPersistenceConstraints.DescriptionLength;

    internal static async Task<RemoteWorkflowDocument> ExportAsync(RemoteTemplateRow row, WorkflowDefinition definition,
        IReadOnlyDictionary<WorkflowComponentId, LlmCallComponent> library,
        Func<RemoteTemplateModelRequirement, Guid?, string?, CancellationToken, Task<RemoteTemplateModelRequirement>> describe,
        CancellationToken token) {
        var nodes = new List<WorkflowNode>();
        var components = new List<RemoteWorkflowComponent>();
        var bindings = new List<RemoteWorkflowBinding>();
        var models = new List<RemoteTemplateModelRequirement>();
        var variants = new Dictionary<(WorkflowComponentId Component, Guid? Provider, string Model), int>();
        foreach (var node in definition.Graph.Nodes) {
            token.ThrowIfCancellationRequested();
            RequirePortableReferences(node);
            var settings = node.Settings;
            var executor = RemoteWorkflowExecutorSettings.Read(node);
            if (node.Kind == WorkflowNodeKind.LlmCall) {
                if (settings.ExecutorId.HasValue || settings.ComponentId is not { } id || !library.TryGetValue(id, out var component)) {
                    throw RemoteWorkflowExecutorSettings.Error(node, "Select an available LLM component before sharing.");
                }
                var provider = settings.ProviderProfileId ?? component.ProviderProfileId;
                var model = string.IsNullOrWhiteSpace(settings.Model) ? component.Model : settings.Model;
                if (!variants.TryGetValue((id, provider, model), out var index)) {
                    index = components.Count;
                    variants.Add((id, provider, model), index);
                    var template = new RemoteWorkflowComponent(component.Name, component.Modality, component.ModelSettings,
                        component.Instructions, component.InputShape, component.ResultShape, models.Count);
                    components.Add(template);
                    models.Add(await describe(ForComponent(template, node.Name), provider, model, token));
                }
                bindings.Add(new(node.Id, index, components[index].ModelIndex));
            } else if (settings.ComponentId.HasValue || settings.ProviderProfileId.HasValue || !string.IsNullOrWhiteSpace(settings.Model)) {
                throw RemoteWorkflowExecutorSettings.Error(node, "Remove the incompatible component or model override.");
            } else if (executor.Purpose is { } purpose) {
                bindings.Add(new(node.Id, null, models.Count));
                models.Add(await describe(new(node.Name, purpose, Vision: executor.Vision), executor.Provider, executor.Model, token));
            }
            nodes.Add(node with { Settings = settings with { ComponentId = null, ProviderProfileId = null, Model = string.Empty, ExecutorSettingsJson = executor.Settings } });
        }
        RequirePortableInputs(definition.InputParameters);
        return new(row, definition.Graph with { Nodes = nodes }, definition.InputParameters, components, bindings, models, definition.CreatedAtUtc);
    }

    internal static void Validate(RemoteWorkflowDocument document) {
        if (!ValidRow(document.Row) || document.CreatedAtUtc == default || document.Graph?.Nodes is not { Count: > 0 } nodes || document.Graph.Edges is null || document.Bindings is null ||
            document.Inputs is null || document.Components is null || document.Models is null || document.Models.Any(model => model is null || string.IsNullOrWhiteSpace(model.Label)) ||
            document.Bindings.Any(binding => binding is null) || document.Components.Any(component => component is null || string.IsNullOrWhiteSpace(component.Name) ||
                component.Name.Length > WorkflowDefinitionPersistenceConstraints.NameLength ||
                string.IsNullOrWhiteSpace(component.Instructions) || component.ModelSettings is null || component.InputShape is null || component.ResultShape is null) ||
            nodes.Any(node => node?.Settings is null || node.Ports is null || string.IsNullOrWhiteSpace(node.Id.Value)) ||
            document.Graph.Edges.Any(edge => edge is null) || nodes.DistinctBy(node => node.Id).Count() != nodes.Count ||
            document.Bindings.DistinctBy(binding => binding.NodeId).Count() != document.Bindings.Count) {
            throw Invalid();
        }
        RequirePortableInputs(document.Inputs);
        var bindings = document.Bindings.ToDictionary(binding => binding.NodeId);
        var usedModels = new HashSet<int>();
        var usedComponents = new HashSet<int>();
        foreach (var node in nodes) {
            RequirePortableReferences(node);
            var settings = node.Settings;
            var executor = RemoteWorkflowExecutorSettings.Read(node);
            if (settings.ComponentId.HasValue || settings.ProviderProfileId.HasValue || !string.IsNullOrWhiteSpace(settings.Model) ||
                executor.Provider.HasValue || !string.IsNullOrWhiteSpace(executor.Model)) {
                throw Invalid();
            }
            if (!bindings.Remove(node.Id, out var binding)) {
                if (node.Kind == WorkflowNodeKind.LlmCall || executor.Purpose.HasValue) {
                    throw Invalid();
                }
                continue;
            }
            if (binding.ModelIndex < 0 || binding.ModelIndex >= document.Models.Count) {
                throw Invalid();
            }
            var model = document.Models[binding.ModelIndex];
            if (node.Kind == WorkflowNodeKind.LlmCall && settings.ExecutorId is null && binding.ComponentIndex is { } index && index >= 0 && index < document.Components.Count) {
                var component = document.Components[index];
                var required = ForComponent(component, node.Name);
                if (component.ModelIndex != binding.ModelIndex || model.Purpose != required.Purpose ||
                    required.StructuredOutput && !model.StructuredOutput || required.Vision && !model.Vision) {
                    throw Invalid();
                }
                usedComponents.Add(index);
            } else if (binding.ComponentIndex.HasValue || executor.Purpose != model.Purpose || executor.Vision && !model.Vision || node.Kind == WorkflowNodeKind.LlmCall) {
                throw Invalid();
            }
            usedModels.Add(binding.ModelIndex);
        }
        if (bindings.Count != 0 || usedModels.Count != document.Models.Count || usedComponents.Count != document.Components.Count) {
            throw Invalid();
        }
    }

    private static RemoteTemplateModelRequirement ForComponent(RemoteWorkflowComponent component, string label) =>
        new(label, ProviderProfilePurpose.Chat, StructuredOutput: component.ModelSettings.RequireJsonOutput || !string.IsNullOrWhiteSpace(component.ModelSettings.ResponseFormatJsonSchema),
            Vision: component.Modality is WorkflowModality.Vision or WorkflowModality.Multimodal) { RequiresModelPrice = true };

    private static void RequirePortableReferences(WorkflowNode node) {
        if (node.Settings.AgentId.HasValue || node.Settings.SubworkflowId.HasValue) {
            throw RemoteWorkflowExecutorSettings.Error(node, "This node references a publisher-local agent or subworkflow. Replace that dependency before sharing.");
        }
    }

    private static void RequirePortableInputs(IReadOnlyList<WorkflowInputParameterDescriptor> inputs) {
        foreach (var input in inputs) {
            if (input?.OptionSource?.StaticOptions is null) {
                throw Invalid();
            }
            if (input.Kind is WorkflowInputParameterKind.ProjectId or WorkflowInputParameterKind.ProjectNodeId or
                WorkflowInputParameterKind.CrmContactEmail or WorkflowInputParameterKind.ExternalConnectionId &&
                (!string.IsNullOrWhiteSpace(input.DefaultValue) || input.OptionSource.StaticOptions.Count > 0)) {
                throw new RemoteInstanceReadException($"Workflow input '{input.Label}' contains a publisher-local selection. Clear its default and static choices before sharing.");
            }
        }
    }

    private static RemoteInstanceReadException Invalid() => new("The shared workflow has invalid component or model bindings. Refresh the catalog and review its definition on the publisher.");
}

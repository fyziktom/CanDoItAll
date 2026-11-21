using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.RemoteInstances;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class RemoteWorkflowSharingTests {
    [Fact]
    public async Task Llm_components_and_image_steps_round_trip_with_local_providers_and_preserve_graph_and_inputs() {
        var component = Component();
        var definition = WorkflowDefinitionValidator.CreateComponentValidationDefinition(component);
        var first = definition.Graph.Nodes[1] with { CanvasX = 88, CanvasY = 123 };
        var second = first with { Id = new("secondary"), Settings = first.Settings with { Model = "other-model" } };
        var imageProvider = Guid.NewGuid();
        var image = ImageNode(WorkflowExecutorIds.ImageGeneration, WorkflowExecutorJson.Serialize(
            new WorkflowImageGenerationExecutorSettings { ProviderProfileId = imageProvider, Model = "upstream-image", Prompt = "Illustrate the topic" }));
        var analysis = ImageNode(WorkflowExecutorIds.ImageAnalyze, JsonSerializer.Serialize(
            new WorkflowImageAnalyzeExecutorSettings { ProviderProfileId = imageProvider, Model = "upstream-vision", Path = "inputs/input.png" }));
        var nodes = new[] { definition.Graph.Nodes[0], first, second, image, analysis, definition.Graph.Nodes[^1] };
        var edges = nodes.Zip(nodes.Skip(1), (left, right) => new WorkflowEdge(new($"{left.Id.Value}-{right.Id.Value}"),
            left.Id, null, right.Id, null, WorkflowEdgeKind.Direct, string.Empty)).ToArray();
        var input = new WorkflowInputParameterDescriptor("topic", "Topic", WorkflowInputParameterKind.Text, true, "Choose a topic", "$.topic",
            "Orchards", WorkflowInputParameterOptionSource.None, null, null, "The topic");
        definition = definition with { Graph = definition.Graph with { Nodes = nodes, Edges = edges }, InputParameters = [input] };
        var document = await ExportAsync(component, definition);
        var wire = JsonSerializer.Serialize(document, JsonSerializerOptions.Web);
        foreach (var privateId in new[] { component.Id.Value, component.ProviderProfileId!.Value, component.PromptArtifactId!.Value, imageProvider }) {
            Assert.DoesNotContain(privateId.ToString("D"), wire, StringComparison.OrdinalIgnoreCase);
        }
        Assert.DoesNotContain("permissions", wire, StringComparison.OrdinalIgnoreCase);
        var received = JsonSerializer.Deserialize<RemoteWorkflowDocument>(wire, JsonSerializerOptions.Web)!;
        RemoteWorkflowProjection.Validate(received);
        Assert.Equal(4, received.Models.Count);
        Assert.Equal(2, received.Components.Count);
        Assert.True(received.Models[0].StructuredOutput);
        Assert.True(received.Models[0].RequiresModelPrice);
        Assert.False(received.Models[2].RequiresModelPrice);
        Assert.True(received.Models[3].Vision);
        Assert.Equal(ProviderProfilePurpose.ImageGeneration, received.Models[2].Purpose);
        Assert.Equal("other-model", received.Models[1].ModelName);
        var choices = received.Models.Select((_, index) => new RemoteTemplateModelChoice(Guid.NewGuid(), $"local-{index}")).ToArray();
        var source = Guid.NewGuid();
        var draft = RemoteWorkflowDraftWriter.Prepare(source, received, choices, WorkflowSettings.Default.DefaultRuntimePolicy);
        Assert.NotEqual(definition.Id, draft.Definition.Id);
        Assert.Equal(WorkflowLifecycleStatus.Draft, draft.Definition.Status);
        Assert.Equal(draft.Definition.Id, RemoteWorkflowDraftWriter.Prepare(source, received, choices, draft.Definition.RuntimePolicy).Definition.Id);
        Assert.NotEqual(draft.Definition.Id, RemoteWorkflowDraftWriter.Prepare(Guid.NewGuid(), received, choices, draft.Definition.RuntimePolicy).Definition.Id);
        Assert.Equal(definition.Graph.StartNodeId, draft.Definition.Graph.StartNodeId);
        Assert.Equal(edges, draft.Definition.Graph.Edges);
        Assert.Equal(input.Key, Assert.Single(draft.Definition.InputParameters).Key);
        for (var index = 0; index < 2; index++) {
            var savedNode = draft.Definition.Graph.Nodes[index + 1];
            var localComponent = draft.Components[index];
            Assert.Equal(localComponent.Id, savedNode.Settings.ComponentId);
            Assert.Equal(choices[index].ProviderId, savedNode.Settings.ProviderProfileId);
            Assert.Equal(choices[index].Model, localComponent.Model);
            Assert.Equal(first.CanvasX, savedNode.CanvasX);
            Assert.Null(localComponent.PromptArtifactId);
            Assert.False(localComponent.Permissions.CanUseTools);
            Assert.True(localComponent.Permissions.RequiresApprovalForExternalCalls);
        }
        var generation = WorkflowExecutorJson.Deserialize<WorkflowImageGenerationExecutorSettings>(draft.Definition.Graph.Nodes[3].Settings.ExecutorSettingsJson);
        var inspection = WorkflowExecutorJson.Deserialize<WorkflowImageAnalyzeExecutorSettings>(draft.Definition.Graph.Nodes[4].Settings.ExecutorSettingsJson);
        Assert.Equal(choices[2].ProviderId, generation.ProviderProfileId);
        Assert.Equal(choices[2].Model, generation.Model);
        Assert.Equal("Illustrate the topic", generation.Prompt);
        Assert.Equal(choices[3].ProviderId, inspection.ProviderProfileId);
        Assert.Equal(choices[3].Model, inspection.Model);
        Assert.Equal("inputs/input.png", inspection.Path);
    }

    [Fact]
    public async Task Refuses_unresolvable_ids_secret_bindings_missing_models_and_publisher_local_input_defaults() {
        var component = Component();
        var definition = WorkflowDefinitionValidator.CreateComponentValidationDefinition(component);
        var node = definition.Graph.Nodes[1];
        var secret = ImageNode(WorkflowExecutorIds.HttpFetch, JsonSerializer.Serialize(new WorkflowHttpExecutorSettings {
            SecretHeader = new() { SecretId = Guid.NewGuid(), SecretNameSnapshot = "sensitive secret name" }
        }));
        var rejected = await Assert.ThrowsAsync<RemoteInstanceReadException>(() => ExportAsync(component, definition with {
            Graph = definition.Graph with { Nodes = [definition.Graph.Nodes[0], secret, definition.Graph.Nodes[^1]] }
        }));
        Assert.DoesNotContain("sensitive secret name", rejected.Message);
        await Assert.ThrowsAsync<RemoteInstanceReadException>(() => ExportAsync(component, definition with {
            Graph = definition.Graph with { Nodes = [node with { Settings = node.Settings with { AgentId = Guid.NewGuid() } }] }
        }));
        await Assert.ThrowsAsync<RemoteInstanceReadException>(() => ExportAsync(component, definition with {
            InputParameters = [new("project", "Project", WorkflowInputParameterKind.ProjectId, false, string.Empty, "$.project",
                Guid.NewGuid().ToString("D"), WorkflowInputParameterOptionSource.None, null, null, string.Empty)]
        }));
        var document = await ExportAsync(component, definition);
        Assert.Throws<RemoteInstanceReadException>(() => RemoteWorkflowProjection.Validate(document with { Bindings = [] }));
        Assert.Throws<RemoteInstanceReadException>(() => RemoteWorkflowProjection.Validate(document with {
            Components = [document.Components[0] with { ModelIndex = -1 }]
        }));
        Assert.Throws<RemoteInstanceReadException>(() => RemoteWorkflowProjection.Validate(document with {
            Graph = document.Graph with { Nodes = definition.Graph.Nodes }
        }));
    }

    private static LlmCallComponent Component() => new(WorkflowComponentId.New(), "Classify", Guid.NewGuid(), "publisher-model", WorkflowModality.Text,
        new(0.2, 200, true, """{"type":"object"}"""), "Return a classification", WorkflowValueShape.Text, WorkflowValueShape.Text,
        AgentPermissionsPolicy.Default, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow) { PromptArtifactId = Guid.NewGuid() };

    private static WorkflowNode ImageNode(WorkflowExecutorId executor, string settings) => new(new(executor.Value), WorkflowNodeKind.Executor,
        executor.Value, [], new(null, null, null, null, string.Empty, WorkflowValueShape.Text, WorkflowValueShape.Text) {
            ExecutorId = executor, ExecutorSettingsJson = settings
        });

    private static Task<RemoteWorkflowDocument> ExportAsync(LlmCallComponent component, WorkflowDefinition definition) =>
        RemoteWorkflowProjection.ExportAsync(new(definition.Id.Value, Guid.NewGuid(), definition.Name, definition.Description, null,
            definition.UpdatedAtUtc, VersionId: definition.VersionId.Value), definition, new Dictionary<WorkflowComponentId, LlmCallComponent> { [component.Id] = component },
            (requirement, _, model, _) => Task.FromResult(requirement with { ModelName = model }), CancellationToken.None);
}

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.RemoteInstances;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class RemoteWorkflowDraftWriter(PersistentWorkflowCatalogService workflows, IWorkflowSettingsService settings,
    IAgentFrameworkWorkspaceService workspace) {
    internal const string PackKey = "remote-workflows";

    public async Task<Guid> CreateAsync(Guid sourceId, RemoteWorkflowDocument document, IReadOnlyList<RemoteTemplateModelChoice> models,
        AgentExecutionActivityWorkspaceIdentity original, Func<CancellationToken, ValueTask> checkSource, CancellationToken token) {
        RemoteWorkflowProjection.Validate(document);
        if (models.Count != document.Models.Count || models.Any(model => model is null || model.ProviderId == Guid.Empty || string.IsNullOrWhiteSpace(model.Model))) {
            throw new RemoteInstanceReadException("Choose a compatible provider and model for each workflow step.");
        }
        if (document.Components.Any(component => models[component.ModelIndex].Model.Length > WorkflowDefinitionPersistenceConstraints.ComponentModelLength)) {
            throw new RemoteInstanceReadException("A selected model ID exceeds the local workflow component limit. Choose a shorter model ID.");
        }
        var localPolicy = (await settings.GetSettingsAsync(token)).DefaultRuntimePolicy with {
            ExposeAzureFunctionsStatusEndpoint = false, ExposeAzureFunctionsMcpTool = false
        };
        var draft = Prepare(sourceId, document, models, localPolicy);
        async ValueTask Authorize(CancellationToken cancellation) {
            await checkSource(cancellation);
            if (await workspace.ReadAgentDecisionDataPolicyContextAsync(cancellation) != original) {
                throw new RemoteInstanceReadException("The local workspace changed. Reopen the workflow import.");
            }
        }
        return await workflows.CreateRemoteDraftAsync(draft.Definition, draft.Components, Authorize, token);
    }

    internal static (WorkflowDefinition Definition, LlmCallComponent[] Components) Prepare(Guid sourceId, RemoteWorkflowDocument document,
        IReadOnlyList<RemoteTemplateModelChoice> models, WorkflowRuntimePolicy localPolicy) {
        var row = document.Row;
        var origin = SHA256.HashData(Encoding.UTF8.GetBytes($"{PackKey}/{sourceId:N}/{row.PublisherId:N}/{row.Id:N}/{document.CreatedAtUtc.UtcTicks:x}"));
        var workflowId = new WorkflowId(new Guid(origin.AsSpan(0, 16)));
        var now = DateTimeOffset.UtcNow;
        var components = document.Components.Select(item => {
            var choice = models[item.ModelIndex];
            return new LlmCallComponent(WorkflowComponentId.New(), item.Name, choice.ProviderId, choice.Model, item.Modality,
                item.ModelSettings, item.Instructions, item.InputShape, item.ResultShape, WorkflowTemplateDraftService.ComponentPermissions, now, now);
        }).ToArray();
        var bindings = document.Bindings.ToDictionary(item => item.NodeId);
        var nodes = document.Graph.Nodes.Select(node => {
            if (!bindings.TryGetValue(node.Id, out var binding)) {
                return node;
            }
            var choice = models[binding.ModelIndex];
            var settings = binding.ComponentIndex is { } index
                ? node.Settings with { ComponentId = components[index].Id, ProviderProfileId = choice.ProviderId, Model = choice.Model }
                : node.Settings with { ExecutorSettingsJson = RemoteWorkflowExecutorSettings.Read(node, choice).Settings };
            return node with { Settings = settings };
        }).ToArray();
        var definition = new WorkflowDefinition(workflowId, WorkflowVersionId.New(), row.Name.Trim(), row.Summary.Trim(), WorkflowLifecycleStatus.Draft,
            document.Graph with { Nodes = nodes }, localPolicy, now, now) {
            InputParameters = document.Inputs, TemplateKey = workflowId.Value.ToString("N"), TemplatePackKey = PackKey,
            TemplatePackVersion = row.VersionId!.Value.ToString("N"),
            SourceHash = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(document, JsonSerializerOptions.Web)))
        };
        return (definition, components);
    }
}

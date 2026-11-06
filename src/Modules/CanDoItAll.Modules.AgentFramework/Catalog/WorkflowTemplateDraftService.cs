using CanDoItAll.AgentFramework.Models;
using System.ComponentModel;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.AgentFramework.Workflows.Templates;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework;

[Description("Workflow template metadata; reading the catalog never executes a workflow or model.")]
public sealed record WorkflowTemplateCatalogItem(
    [property: Description("Template-pack key used to create a draft; lookup is case-insensitive.")] string Key,
    [property: Description("Human-readable template name.")] string Name,
    [property: Description("Explanation of the template purpose.")] string Description,
    [property: Description("Number of nodes in the template graph.")] int NodeCount,
    [property: Description("Number of directed edges in the template graph.")] int EdgeCount,
    [property: Description("Number of input parameters materialized from the template.")] int InputParameterCount,
    [property: Description("Preferred workflow execution backend; 0 InProcess, 1 DurableTask, 2 AzureFunctions.")] WorkflowRuntimeBackendKind PreferredBackend,
    [property: Description("Display summary grouped by node kind and count; not an executable graph or guaranteed execution order.")] string FlowShape);

public sealed class WorkflowTemplateDraftService(
    WorkflowTemplatePackLoader templates,
    IWorkflowComponentLibraryService components,
    IWorkflowCatalogService catalog,
    IWorkflowDefinitionValidator validator,
    ILogger<WorkflowTemplateDraftService> logger) {
    public async Task<WorkflowDraftInspection> InspectAsync(WorkflowId workflowId, WorkflowComponentId componentId,
        CancellationToken cancellationToken = default) {
        var definitionRead = catalog.GetDefinitionAsync(workflowId, cancellationToken: cancellationToken);
        var componentRead = components.GetComponentAsync(componentId, cancellationToken);
        await Task.WhenAll(definitionRead, componentRead);
        var definition = (await definitionRead)?.Definition;
        var component = await componentRead;
        if (definition is not null && definition.Id != workflowId || component is not null && component.Id != componentId) {
            throw new InvalidOperationException("Draft inspection returned a different resource identity.");
        }
        return new(definition, component);
    }

    public IReadOnlyList<WorkflowTemplateCatalogItem> List() {
        var pack = templates.Load();
        return pack.Workflows.Select(template => new WorkflowTemplateCatalogItem(template.Key, template.Name, template.Description,
            template.Graph.Nodes.Count, template.Graph.Edges.Count, pack.CreateInputParameters(template).Count,
            pack.RuntimePolicy.PreferredBackend, string.Join(" → ", template.Graph.Nodes.GroupBy(node => node.Kind)
                .Select(group => $"{group.Key} × {group.Count()}")))).ToArray();
    }

    public async Task<WorkflowDefinition> CreateAsync(string templateKey, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(templateKey) || templateKey.Length > 256) {
            throw new ArgumentException("A bounded template key is required.");
        }
        var pack = templates.Load();
        var template = pack.Workflows.FirstOrDefault(item => string.Equals(item.Key, templateKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException("The workflow template was not found.");
        var providers = await components.ListProviderOptionsAsync(cancellationToken);
        var provider = providers.FirstOrDefault(item => item.IsEnabled && item.SupportsStructuredOutput &&
            !string.IsNullOrWhiteSpace(item.DefaultModel) && item.ModelOptions.Contains(item.DefaultModel, StringComparer.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("An enabled structured-output provider with an available default model is required.");
        var workflowId = WorkflowId.New();
        var componentId = WorkflowComponentId.New();
        var name = ChooseName(template.Name, await catalog.ListDefinitionsAsync(cancellationToken));
        var now = DateTimeOffset.UtcNow;
        var candidate = new LlmCallComponent(componentId, $"Draft LLM: {name}", provider.ProviderProfileId,
            provider.DefaultModel, WorkflowModality.Text, pack.CreateModelSettings(), pack.CreateComponentInstructions(template),
            pack.JsonShape, pack.JsonShape, ComponentPermissions, now, now);
        var definition = pack.CreateDefinition(template, candidate) with {
            Id = workflowId, Name = name, Status = WorkflowLifecycleStatus.Draft,
            RuntimePolicy = pack.RuntimePolicy, InputParameters = pack.CreateInputParameters(template)
        };
        return await SavePreparedDraftAsync(candidate, definition, cancellationToken);
    }

    internal async Task<WorkflowDefinition> SavePreparedDraftAsync(LlmCallComponent candidate, WorkflowDefinition definition,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(definition);
        var componentId = candidate.Id;
        var workflowId = definition.Id;
        if (componentId.Value == Guid.Empty || workflowId.Value == Guid.Empty || candidate.Revision != Guid.Empty) {
            throw new ArgumentException("A prepared creation requires fresh, explicit workflow and component identities.");
        }
        var validation = validator.Validate(definition, [candidate]);
        if (validation.Issues.Count > 0) {
            throw new WorkflowDefinitionValidationException("The prepared workflow does not produce a valid draft.");
        }
        LlmCallComponent? acknowledgedComponent = null;
        try {
            acknowledgedComponent = await components.SaveComponentAsync(new(candidate.Id, candidate.Name, candidate.ProviderProfileId,
                candidate.Model, candidate.Modality, candidate.ModelSettings, candidate.Instructions, candidate.InputShape,
                candidate.ResultShape, candidate.Permissions) {
                PromptArtifactId = candidate.PromptArtifactId,
                PromptVersionId = candidate.PromptVersionId
            }, cancellationToken);
            return await catalog.SaveDefinitionAsync(new(workflowId, null, definition.Name, definition.Description,
                WorkflowLifecycleStatus.Draft, definition.Graph, definition.RuntimePolicy) {
                InputParameters = definition.InputParameters,
                TemplateProvenance = string.IsNullOrEmpty(definition.TemplateKey) ? null
                    : new(definition.TemplateKey, definition.TemplatePackKey, definition.TemplatePackVersion, definition.SourceHash),
                ExternalNamespace = definition.ExternalNamespace,
                ExternalKey = definition.ExternalKey
            }, cancellationToken);
        } catch (Exception error) {
            if (acknowledgedComponent is null && error is WorkflowComponentConcurrencyException) {
                throw;
            }
            if (acknowledgedComponent is not null && error is WorkflowDefinitionConcurrencyException or WorkflowDefinitionValidationException) {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                try {
                    await components.DeleteComponentAsync(new(componentId, acknowledgedComponent.Revision), cleanup.Token);
                } catch (Exception cleanupError) {
                    logger.LogError("Conditional cleanup needs review for component {ComponentId}, workflow {WorkflowId}: {ErrorType}.",
                        componentId.Value, workflowId.Value, cleanupError.GetType().Name);
                    throw new WorkflowDraftCreationUnknownException(workflowId, componentId, new AggregateException(error, cleanupError));
                }
                throw;
            }
            logger.LogWarning("Creation acknowledgement needs review for workflow {WorkflowId}, component {ComponentId}: {FailureType}.",
                workflowId.Value, componentId.Value, error.GetType().Name);
            throw new WorkflowDraftCreationUnknownException(workflowId, componentId, error);
        }
    }

    public static AgentPermissionsPolicy ComponentPermissions => AgentPermissionsPolicy.Default with {
        CanUseTools = false,
        CanAskOtherAgents = false,
        CanEscalateToHuman = false,
        RequiresApprovalForExternalCalls = true
    };

    private static string ChooseName(string name, IReadOnlyList<WorkflowCatalogItem> definitions) {
        var trimmed = name.Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) {
            throw new InvalidOperationException("A workflow template name is required.");
        }
        var existing = definitions.Select(definition => definition.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!existing.Contains(trimmed)) {
            return trimmed;
        }
        for (var index = 1; index <= 999; index++) {
            var candidate = $"{index:00} {trimmed}";
            if (!existing.Contains(candidate)) {
                return candidate;
            }
        }
        throw new InvalidOperationException("No numbered draft name is available for this template.");
    }
}

public sealed class WorkflowDraftCreationUnknownException(WorkflowId workflowId, WorkflowComponentId componentId, Exception inner)
    : Exception($"Draft creation could not be confirmed. Review workflow {workflowId.Value:D} and component {componentId.Value:D} in the catalog. Do not repeat this creation while its outcome is unconfirmed.", inner) {
    public WorkflowId WorkflowId { get; } = workflowId;
    public WorkflowComponentId ComponentId { get; } = componentId;
}

public sealed record WorkflowDraftInspection(WorkflowDefinition? Definition, LlmCallComponent? Component);

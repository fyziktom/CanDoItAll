using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

public sealed class InMemoryWorkflowCatalogService :
    IWorkflowCatalogService,
    IWorkflowComponentLibraryService,
    IWorkflowSettingsService
{
    private readonly InMemoryWorkflowCatalogStore store;
    private readonly IWorkflowDefinitionValidator validator;
    private readonly IProviderProfileRegistry? providerRegistry;
    private readonly IProviderProfileService? providerProfileService;
    private readonly IWorkflowRuntimeBackendCatalog runtimeBackendCatalog;

    public InMemoryWorkflowCatalogService(IWorkflowDefinitionValidator validator)
        : this(new InMemoryWorkflowCatalogStore(), validator)
    {
    }

    public InMemoryWorkflowCatalogService(
        InMemoryWorkflowCatalogStore store,
        IWorkflowDefinitionValidator validator,
        IProviderProfileRegistry? providerRegistry = null,
        IProviderProfileService? providerProfileService = null,
        IWorkflowRuntimeBackendCatalog? runtimeBackendCatalog = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(validator);

        this.store = store;
        this.validator = validator;
        this.providerRegistry = providerRegistry;
        this.providerProfileService = providerProfileService;
        this.runtimeBackendCatalog = runtimeBackendCatalog ?? new WorkflowRuntimeBackendCatalog();
    }

    public async Task<IReadOnlyList<WorkflowCatalogItem>> ListDefinitionsAsync(
        CancellationToken cancellationToken = default)
    {
        await store.Gate.WaitAsync(cancellationToken);
        try
        {
            return store.Definitions.Values
                .Select(versions => versions[^1])
                .OrderByDescending(definition => definition.UpdatedAtUtc)
                .Select(definition => new WorkflowCatalogItem(
                    definition.Id,
                    definition.VersionId,
                    definition.Name,
                    definition.Description,
                    definition.Status,
                    definition.RuntimePolicy.PreferredBackend,
                    definition.UpdatedAtUtc))
                .ToArray();
        }
        finally
        {
            store.Gate.Release();
        }
    }

    public async Task<WorkflowDefinitionDetail?> GetDefinitionAsync(
        WorkflowId workflowId,
        WorkflowVersionId? versionId = null,
        CancellationToken cancellationToken = default)
    {
        WorkflowDefinition? definition;
        await store.Gate.WaitAsync(cancellationToken);
        try
        {
            if (!store.Definitions.TryGetValue(workflowId, out var versions))
            {
                return null;
            }

            definition = versionId is null
                ? versions[^1]
                : versions.SingleOrDefault(item => item.VersionId == versionId.Value);
        }
        finally
        {
            store.Gate.Release();
        }

        return definition is null
            ? null
            : new WorkflowDefinitionDetail(
                definition,
                await ValidateDefinitionAsync(definition, cancellationToken));
    }

    public async Task<WorkflowDefinitionDetail?> GetLatestDefinitionByStatusAsync(
        WorkflowId workflowId,
        WorkflowLifecycleStatus status,
        CancellationToken cancellationToken = default)
    {
        WorkflowDefinition? definition;
        await store.Gate.WaitAsync(cancellationToken);
        try
        {
            definition = store.Definitions.TryGetValue(workflowId, out var versions)
                ? versions
                    .Where(item => item.Status == status)
                    .OrderByDescending(item => item.UpdatedAtUtc)
                    .ThenByDescending(item => item.CreatedAtUtc)
                    .ThenByDescending(item => item.VersionId.Value)
                    .FirstOrDefault()
                : null;
        }
        finally
        {
            store.Gate.Release();
        }

        return definition is null
            ? null
            : new WorkflowDefinitionDetail(
                definition,
                await ValidateDefinitionAsync(definition, cancellationToken));
    }

    public async Task<WorkflowDefinition> SaveDefinitionAsync(
        WorkflowDefinitionSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);
        ArgumentNullException.ThrowIfNull(request.Graph);
        ArgumentNullException.ThrowIfNull(request.RuntimePolicy);

        var now = DateTimeOffset.UtcNow;
        WorkflowDefinition definition;
        await store.Gate.WaitAsync(cancellationToken);
        try
        {
            var workflowId = request.Id ?? WorkflowId.New();
            store.Definitions.TryGetValue(workflowId, out var versions);
            var current = versions is { Count: > 0 } ? versions[^1] : null;
            if (request.ExpectedVersionId is { } expectedVersionId &&
                current is not null &&
                current.VersionId != expectedVersionId)
            {
                throw new InvalidOperationException($"Workflow definition '{workflowId}' was updated by another request.");
            }

            definition = new WorkflowDefinition(
                workflowId,
                WorkflowVersionId.New(),
                request.Name.Trim(),
                request.Description.Trim(),
                request.Status,
                SnapshotGraph(request.Graph, store.Components),
                request.RuntimePolicy,
                current?.CreatedAtUtc ?? now,
                now)
            {
                InputParameters = SnapshotInputParameters(request.InputParameters)
            };
        }
        finally
        {
            store.Gate.Release();
        }

        ThrowIfValidationFailed(
            await ValidateDefinitionAsync(definition, cancellationToken),
            "Workflow definition save failed validation");

        await store.Gate.WaitAsync(cancellationToken);
        try
        {
            store.Definitions.TryGetValue(definition.Id, out var versions);
            var current = versions is { Count: > 0 } ? versions[^1] : null;
            if (request.ExpectedVersionId is { } expectedVersionId &&
                current is not null &&
                current.VersionId != expectedVersionId)
            {
                throw new InvalidOperationException($"Workflow definition '{definition.Id}' was updated by another request.");
            }

            if (versions is null)
            {
                store.Definitions[definition.Id] = [definition];
            }
            else
            {
                versions.Add(definition);
            }

            return definition;
        }
        finally
        {
            store.Gate.Release();
        }
    }

    public async Task<WorkflowDefinition> ChangeDefinitionStatusAsync(
        WorkflowDefinitionStatusChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var detail = await GetDefinitionAsync(request.WorkflowId, versionId: null, cancellationToken)
            ?? throw new KeyNotFoundException($"Workflow definition '{request.WorkflowId}' was not found.");
        if (request.Status == WorkflowLifecycleStatus.Active)
        {
            ThrowIfValidationFailed(await ValidateDefinitionAsync(detail.Definition, cancellationToken), "Workflow definition cannot be published");
        }

        return await SaveDefinitionAsync(
            new WorkflowDefinitionSaveRequest(
                detail.Definition.Id,
                request.ExpectedVersionId,
                detail.Definition.Name,
                detail.Definition.Description,
                request.Status,
                detail.Definition.Graph,
                detail.Definition.RuntimePolicy)
            {
                InputParameters = detail.Definition.InputParameters
            },
            cancellationToken);
    }

    public async Task<WorkflowDefinitionExportEnvelope?> ExportDefinitionAsync(
        WorkflowId workflowId,
        WorkflowVersionId? versionId = null,
        CancellationToken cancellationToken = default)
    {
        var detail = await GetDefinitionAsync(workflowId, versionId, cancellationToken);
        return detail is null
            ? null
            : new WorkflowDefinitionExportEnvelope(
                WorkflowDefinitionExchangeFormats.Current,
                detail.Definition,
                detail.Validation,
                DateTimeOffset.UtcNow);
    }

    public async Task<WorkflowDefinition> ImportDefinitionAsync(
        WorkflowDefinitionImportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Envelope);
        ArgumentNullException.ThrowIfNull(request.Envelope.Definition);
        if (!string.Equals(
            request.Envelope.SourceFormat,
            WorkflowDefinitionExchangeFormats.Current,
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Workflow definition import format '{request.Envelope.SourceFormat}' is not supported.");
        }

        var source = request.Envelope.Definition;
        var workflowId = request.PreserveWorkflowId ? source.Id : (WorkflowId?)null;
        var importedName = string.IsNullOrWhiteSpace(request.Name) ? source.Name : request.Name.Trim();
        var importedStatus = request.Status ?? WorkflowLifecycleStatus.Draft;

        return await SaveDefinitionAsync(
            new WorkflowDefinitionSaveRequest(
                workflowId,
                null,
                importedName,
                source.Description,
                importedStatus,
                source.Graph,
                source.RuntimePolicy)
            {
                InputParameters = source.InputParameters
            },
            cancellationToken);
    }

    public async Task DeleteDefinitionAsync(
        WorkflowId workflowId,
        CancellationToken cancellationToken = default)
    {
        await store.Gate.WaitAsync(cancellationToken);
        try
        {
            store.Definitions.Remove(workflowId);
        }
        finally
        {
            store.Gate.Release();
        }
    }

    public async Task<WorkflowValidationResult> ValidateDefinitionAsync(
        WorkflowDefinition definition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var componentSnapshot = await ListReferencedComponentsAsync(definition, cancellationToken);
        var result = validator.Validate(definition, componentSnapshot);
        var issues = result.Issues.ToList();

        if (definition.RuntimePolicy.RequireDurableProductionRuns &&
            definition.RuntimePolicy.PreferredBackend == WorkflowRuntimeBackendKind.InProcess)
        {
            issues.Add(new WorkflowValidationIssue(
                WorkflowValidationIssueCode.InvalidWorkflowSettings,
                "Durable production workflows cannot prefer the in-process runtime backend."));
        }

        issues.AddRange(WorkflowRuntimePolicyValidator.ValidateRegisteredBackendAvailability(
            definition.RuntimePolicy,
            runtimeBackendCatalog));

        var currentSettings = await GetSettingsAsync(cancellationToken);
        if (!currentSettings.HumanInLoopPolicy.AllowHumanInputNodes &&
            definition.Graph.Nodes.Any(node => node.Kind == WorkflowNodeKind.HumanInput))
        {
            issues.Add(new WorkflowValidationIssue(
                WorkflowValidationIssueCode.InvalidWorkflowSettings,
                "Human-in-loop nodes are disabled by workflow settings."));
        }

        foreach (var component in componentSnapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var providerIssues = await ValidateProviderCompatibilityAsync(component, cancellationToken);
            issues.AddRange(providerIssues);
        }

        return new WorkflowValidationResult(issues);
    }

    public async Task<IReadOnlyList<WorkflowProviderOption>> ListProviderOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        if (providerRegistry is null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return [];
        }

        var providers = await providerRegistry.ListProvidersAsync(cancellationToken);
        return providers
            .Select(NormalizeProvider)
            .Where(provider => provider.Purpose == ProviderProfilePurpose.Chat)
            .OrderByDescending(provider => provider.IsEnabled)
            .ThenBy(provider => provider.Name, StringComparer.OrdinalIgnoreCase)
            .Select(CreateProviderOption)
            .ToArray();
    }

    public async Task<IReadOnlyList<LlmCallComponent>> ListComponentsAsync(CancellationToken cancellationToken = default)
    {
        await store.Gate.WaitAsync(cancellationToken);
        try
        {
            return store.Components.Values
                .OrderBy(component => component.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        finally
        {
            store.Gate.Release();
        }
    }

    public async Task<LlmCallComponent?> GetComponentAsync(
        WorkflowComponentId componentId,
        CancellationToken cancellationToken = default)
    {
        await store.Gate.WaitAsync(cancellationToken);
        try
        {
            store.Components.TryGetValue(componentId, out var component);
            return component;
        }
        finally
        {
            store.Gate.Release();
        }
    }

    public async Task<LlmCallComponent> SaveComponentAsync(
        LlmCallComponentSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Model);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Instructions);
        ArgumentNullException.ThrowIfNull(request.ModelSettings);
        ArgumentNullException.ThrowIfNull(request.InputShape);
        ArgumentNullException.ThrowIfNull(request.ResultShape);

        var now = DateTimeOffset.UtcNow;
        var component = new LlmCallComponent(
            request.Id ?? WorkflowComponentId.New(),
            request.Name.Trim(),
            request.ProviderProfileId,
            request.Model.Trim(),
            request.Modality,
            request.ModelSettings,
            request.Instructions.Trim(),
            request.InputShape,
            request.ResultShape,
            request.Permissions,
            CreatedAtUtc: now,
            UpdatedAtUtc: now);

        var validation = validator.Validate(
            CreateComponentValidationDefinition(component),
            [component]);
        var providerIssues = await ValidateProviderCompatibilityAsync(component, cancellationToken);
        var issues = validation.Issues.Concat(providerIssues).ToArray();
        if (issues.Length > 0)
        {
            throw new InvalidOperationException(string.Join(" ", issues.Select(issue => issue.Message)));
        }

        await store.Gate.WaitAsync(cancellationToken);
        try
        {
            if (store.Components.TryGetValue(component.Id, out var current))
            {
                component = component with { CreatedAtUtc = current.CreatedAtUtc };
            }

            store.Components[component.Id] = component;
            return component;
        }
        finally
        {
            store.Gate.Release();
        }
    }

    public async Task DeleteComponentAsync(
        WorkflowComponentId componentId,
        CancellationToken cancellationToken = default)
    {
        await store.Gate.WaitAsync(cancellationToken);
        try
        {
            store.Components.Remove(componentId);
        }
        finally
        {
            store.Gate.Release();
        }
    }

    public Task<WorkflowSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(store.Settings);
    }

    public Task<WorkflowSettings> SaveSettingsAsync(
        WorkflowSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.ArtifactPolicy.MaxInlinePayloadCharacters <= 0)
        {
            throw new InvalidOperationException("Workflow artifact inline payload limit must be positive.");
        }

        if (settings.HumanInLoopPolicy.DefaultRequestTimeoutMinutes <= 0)
        {
            throw new InvalidOperationException("Workflow human-in-loop timeout must be positive.");
        }

        var runtimeIssues = WorkflowRuntimePolicyValidator.ValidateRegisteredBackendAvailability(
            settings.DefaultRuntimePolicy,
            runtimeBackendCatalog);
        if (runtimeIssues.Count > 0)
        {
            throw new InvalidOperationException(
                $"Workflow default runtime policy is invalid: {string.Join(" ", runtimeIssues.Select(issue => issue.Message))}");
        }

        cancellationToken.ThrowIfCancellationRequested();
        store.Settings = settings;
        return Task.FromResult(settings);
    }

    private async Task<IReadOnlyList<WorkflowValidationIssue>> ValidateProviderCompatibilityAsync(
        LlmCallComponent component,
        CancellationToken cancellationToken)
    {
        if (!component.ProviderProfileId.HasValue || providerRegistry is null)
        {
            return [];
        }

        var provider = await providerRegistry.GetProviderAsync(component.ProviderProfileId.Value, cancellationToken);
        if (provider is null)
        {
            return
            [
                new WorkflowValidationIssue(
                    WorkflowValidationIssueCode.InvalidProviderModel,
                    $"LLM Call Component '{component.Id}' references provider '{component.ProviderProfileId.Value:D}', which does not exist.")
            ];
        }

        if (!provider.IsEnabled)
        {
            return
            [
                new WorkflowValidationIssue(
                    WorkflowValidationIssueCode.InvalidProviderModel,
                    $"LLM Call Component '{component.Id}' references disabled provider '{provider.Name}'.")
            ];
        }

        provider = NormalizeProvider(provider);
        if (provider.Purpose != ProviderProfilePurpose.Chat)
        {
            return
            [
                new WorkflowValidationIssue(
                    WorkflowValidationIssueCode.InvalidProviderModel,
                    $"LLM Call Component '{component.Id}' references provider '{provider.Name}', which is not a chat provider.")
            ];
        }

        var featureMatrix = ResolveProviderFeatureMatrix(provider);
        var providerSupportsVision = featureMatrix?.SupportsVision ?? true;
        if (component.Modality is (WorkflowModality.Vision or WorkflowModality.Multimodal) && !providerSupportsVision)
        {
            return
            [
                new WorkflowValidationIssue(
                    WorkflowValidationIssueCode.UnsupportedModality,
                    $"LLM Call Component '{component.Id}' requires vision support but provider '{provider.Name}' does not support vision.")
            ];
        }

        var providerSupportsStructuredOutput = featureMatrix?.SupportsStructuredOutput ?? true;
        if (component.ModelSettings.RequireJsonOutput && !providerSupportsStructuredOutput)
        {
            return
            [
                new WorkflowValidationIssue(
                    WorkflowValidationIssueCode.InvalidProviderModel,
                    $"LLM Call Component '{component.Id}' requires structured JSON output but provider '{provider.Name}' does not support structured output.")
            ];
        }

        var effectiveModel = string.IsNullOrWhiteSpace(component.Model)
            ? provider.DefaultModel
            : component.Model.Trim();
        if (!ProviderPricingDefaults.TryFindPrice(provider.ModelPrices, effectiveModel, out _))
        {
            return
            [
                new WorkflowValidationIssue(
                    WorkflowValidationIssueCode.InvalidProviderModel,
                    $"LLM Call Component '{component.Id}' model '{effectiveModel}' requires a model price row on provider '{provider.Name}'.")
            ];
        }

        return [];
    }

    private ProviderProfile NormalizeProvider(ProviderProfile provider)
    {
        return providerProfileService?.NormalizeImportedProfile(provider) ?? provider;
    }

    private ProviderFeatureMatrix? ResolveProviderFeatureMatrix(ProviderProfile provider)
    {
        return providerProfileService?.ResolveFeatureMatrix(provider);
    }

    private WorkflowProviderOption CreateProviderOption(ProviderProfile provider)
    {
        var featureMatrix = ResolveProviderFeatureMatrix(provider);
        return new WorkflowProviderOption(
            provider.Id,
            provider.Name,
            provider.Kind,
            provider.Transport,
            provider.Purpose,
            provider.DefaultModel,
            BuildModelOptions(provider),
            provider.IsEnabled,
            provider.SupportsStreaming,
            provider.SupportsTools,
            featureMatrix?.SupportsStructuredOutput ?? true,
            featureMatrix?.SupportsVision ?? true,
            provider.SupportsBackgroundResponses);
    }

    private static IReadOnlyList<string> BuildModelOptions(ProviderProfile provider)
    {
        return provider.SuggestedModels
            .Prepend(provider.DefaultModel)
            .Where(model => !string.IsNullOrWhiteSpace(model))
            .Select(model => model.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static WorkflowGraph SnapshotGraph(
        WorkflowGraph graph,
        IReadOnlyDictionary<WorkflowComponentId, LlmCallComponent> components)
    {
        return new WorkflowGraph(
            graph.StartNodeId,
            graph.Nodes
                .Select(node => SnapshotNode(node, components))
                .ToArray(),
            graph.Edges.ToArray());
    }

    private static WorkflowNode SnapshotNode(
        WorkflowNode node,
        IReadOnlyDictionary<WorkflowComponentId, LlmCallComponent> components)
    {
        if (node.Kind != WorkflowNodeKind.LlmCall ||
            !string.IsNullOrWhiteSpace(node.Settings.Instructions) ||
            node.Settings.ComponentId is not { } componentId ||
            !components.TryGetValue(componentId, out var component))
        {
            return node with { Ports = node.Ports.ToArray() };
        }

        return node with
        {
            Ports = node.Ports.ToArray(),
            Settings = node.Settings with { Instructions = component.Instructions }
        };
    }

    private static IReadOnlyList<WorkflowInputParameterDescriptor> SnapshotInputParameters(
        IReadOnlyList<WorkflowInputParameterDescriptor> inputParameters)
        => inputParameters
            .Select(parameter => parameter with
            {
                OptionSource = parameter.OptionSource with
                {
                    StaticOptions = parameter.OptionSource.StaticOptions.ToArray()
                }
            })
            .ToArray();

    private static void ThrowIfValidationFailed(
        WorkflowValidationResult validation,
        string messagePrefix)
    {
        if (validation.Succeeded)
        {
            return;
        }

        throw WorkflowFailureDiagnosticMapper.CreateValidationException(
            $"{messagePrefix}: {string.Join(" ", validation.Issues.Select(issue => issue.Message))}",
            validation,
            correlationId: "workflow-validation");
    }

    private async Task<IReadOnlyList<LlmCallComponent>> ListReferencedComponentsAsync(
        WorkflowDefinition definition,
        CancellationToken cancellationToken)
    {
        var referencedComponentIds = definition.Graph.Nodes
            .Where(node => node.Kind == WorkflowNodeKind.LlmCall && node.Settings.ComponentId.HasValue)
            .Select(node => node.Settings.ComponentId!.Value)
            .ToHashSet();
        if (referencedComponentIds.Count == 0)
        {
            return [];
        }

        var allComponents = await ListComponentsAsync(cancellationToken);
        return allComponents
            .Where(component => referencedComponentIds.Contains(component.Id))
            .ToArray();
    }

    private static WorkflowDefinition CreateComponentValidationDefinition(LlmCallComponent component)
    {
        var start = new WorkflowNodeId("start");
        var llm = new WorkflowNodeId("llm");
        var end = new WorkflowNodeId("end");
        return new WorkflowDefinition(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            "Component validation",
            "Internal component validation workflow.",
            WorkflowLifecycleStatus.Draft,
            new WorkflowGraph(
                start,
                [
                    new WorkflowNode(
                        start,
                        WorkflowNodeKind.Start,
                        "Start",
                        [],
                        new WorkflowNodeSettings(
                            ComponentId: null,
                            AgentId: null,
                            SubworkflowId: null,
                            ExternalRequestKind: null,
                            Instructions: string.Empty,
                            InputShape: component.InputShape,
                            ResultShape: component.InputShape)),
                    new WorkflowNode(
                        llm,
                        WorkflowNodeKind.LlmCall,
                        "LLM",
                        [],
                        new WorkflowNodeSettings(
                            component.Id,
                            AgentId: null,
                            SubworkflowId: null,
                            ExternalRequestKind: null,
                            Instructions: component.Instructions,
                            InputShape: component.InputShape,
                            ResultShape: component.ResultShape)),
                    new WorkflowNode(
                        end,
                        WorkflowNodeKind.End,
                        "End",
                        [],
                        new WorkflowNodeSettings(
                            ComponentId: null,
                            AgentId: null,
                            SubworkflowId: null,
                            ExternalRequestKind: null,
                            Instructions: string.Empty,
                            InputShape: component.ResultShape,
                            ResultShape: component.ResultShape))
                ],
                [
                    new WorkflowEdge(
                        new WorkflowEdgeId("start-to-llm"),
                        start,
                        SourcePortId: null,
                        llm,
                        TargetPortId: null,
                        WorkflowEdgeKind.Direct,
                        ConditionExpression: string.Empty),
                    new WorkflowEdge(
                        new WorkflowEdgeId("llm-to-end"),
                        llm,
                        SourcePortId: null,
                        end,
                        TargetPortId: null,
                        WorkflowEdgeKind.Direct,
                        ConditionExpression: string.Empty)
                ]),
            WorkflowSettings.Default.DefaultRuntimePolicy,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }
}

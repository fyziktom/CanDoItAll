using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

public sealed class InMemoryWorkflowCatalogService :
    IWorkflowCatalogService,
    IWorkflowCatalogSearchService,
    IWorkflowCatalogLookupService,
    IWorkflowComponentLibraryService,
    IWorkflowSettingsService
{
    private readonly InMemoryWorkflowCatalogStore store;
    private readonly IWorkflowDefinitionValidator validator;
    private readonly WorkflowProviderCompatibilityService providerCompatibility;
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
        IWorkflowRuntimeBackendCatalog? runtimeBackendCatalog = null,
        IProviderRuntimeProfileSource? providerSource = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(validator);

        this.store = store;
        this.validator = validator;
        providerCompatibility = new WorkflowProviderCompatibilityService(
            providerRegistry,
            providerSource ?? providerRegistry as IProviderRuntimeProfileSource,
            providerProfileService);
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
                .Select(MapCatalogItem)
                .ToArray();
        }
        finally
        {
            store.Gate.Release();
        }
    }

    public async Task<WorkflowCatalogSearchPage> SearchDefinitionsAsync(
        WorkflowCatalogSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        await store.Gate.WaitAsync(cancellationToken);
        try
        {
            var filtered = store.Definitions.Values
                .Select(versions => versions[^1])
                .Where(definition => !query.Status.HasValue || definition.Status == query.Status.Value)
                .Where(definition => query.Text is null ||
                                     definition.Name.Contains(query.Text, StringComparison.OrdinalIgnoreCase) ||
                                     definition.Description.Contains(query.Text, StringComparison.OrdinalIgnoreCase));
            var totalCount = filtered.Count();
            cancellationToken.ThrowIfCancellationRequested();
            var items = filtered
                .OrderByDescending(definition => definition.UpdatedAtUtc)
                .ThenBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(definition => definition.Name, StringComparer.Ordinal)
                .ThenBy(definition => definition.Id.Value)
                .Skip(query.Offset)
                .Take(query.PageSize)
                .Select(MapCatalogItem)
                .ToArray();

            return new WorkflowCatalogSearchPage(
                items,
                query.PageIndex,
                query.PageSize,
                totalCount);
        }
        finally
        {
            store.Gate.Release();
        }
    }

    public async Task<IReadOnlyList<WorkflowCatalogItem>> LookupDefinitionsAsync(
        WorkflowCatalogLookupQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.WorkflowIds.Count == 0)
        {
            return [];
        }

        await store.Gate.WaitAsync(cancellationToken);
        try
        {
            return query.WorkflowIds
                .Where(store.Definitions.ContainsKey)
                .Select(workflowId => store.Definitions[workflowId][^1])
                .Select(MapCatalogItem)
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
            if (!store.Definitions.TryGetValue(workflowId, out var versions))
            {
                definition = null;
            }
            else
            {
                var current = versions[^1];
                var currentAllowsActiveLookup =
                    status != WorkflowLifecycleStatus.Active ||
                    current.Status is WorkflowLifecycleStatus.Draft or WorkflowLifecycleStatus.Active;
                definition = currentAllowsActiveLookup
                    ? versions
                        .Where(item => item.Status == status)
                        .OrderByDescending(item => item.UpdatedAtUtc)
                        .ThenByDescending(item => item.CreatedAtUtc)
                        .ThenByDescending(item => item.VersionId.Value)
                        .FirstOrDefault()
                    : null;
            }
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
            if (current?.VersionId != request.ExpectedVersionId) {
                throw new WorkflowDefinitionConcurrencyException($"Workflow definition '{workflowId}' was updated by another request.");
            }

            var stableIdentity = WorkflowStableIdentityPolicy.Resolve(request, current);
            WorkflowStableIdentityPolicy.EnsureExternalIdentityIsUnique(
                store.Definitions,
                workflowId,
                stableIdentity.ExternalNamespace,
                stableIdentity.ExternalKey);
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
                InputParameters = SnapshotInputParameters(request.InputParameters),
                TemplateKey = stableIdentity.TemplateKey,
                TemplatePackKey = stableIdentity.TemplatePackKey,
                TemplatePackVersion = stableIdentity.TemplatePackVersion,
                SourceHash = stableIdentity.SourceHash,
                ExternalNamespace = stableIdentity.ExternalNamespace,
                ExternalKey = stableIdentity.ExternalKey
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
            if (current?.VersionId != request.ExpectedVersionId) {
                throw new WorkflowDefinitionConcurrencyException($"Workflow definition '{definition.Id}' was updated by another request.");
            }

            WorkflowStableIdentityPolicy.EnsureExternalIdentityIsUnique(
                store.Definitions,
                definition.Id,
                definition.ExternalNamespace,
                definition.ExternalKey);
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
        if (request.ExpectedVersionId is null) {
            throw new WorkflowDefinitionConcurrencyException(
                $"Workflow definition '{request.WorkflowId}' requires its original version for a status change.");
        }

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
        if (!request.PreserveWorkflowId && request.ExpectedVersionId.HasValue) {
            throw new InvalidOperationException("A workflow import original version requires preserving the target workflow identifier.");
        }
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
                request.ExpectedVersionId,
                importedName,
                source.Description,
                importedStatus,
                source.Graph,
                source.RuntimePolicy)
            {
                InputParameters = source.InputParameters,
                ExternalNamespace = request.PreserveWorkflowId
                    ? source.ExternalNamespace
                    : string.Empty,
                ExternalKey = request.PreserveWorkflowId
                    ? source.ExternalKey
                    : string.Empty,
                TemplateProvenance = request.PreserveWorkflowId &&
                                     !string.IsNullOrWhiteSpace(source.TemplateKey)
                    ? new WorkflowTemplateProvenance(
                        source.TemplateKey,
                        source.TemplatePackKey,
                        source.TemplatePackVersion,
                        source.SourceHash)
                    : null
            },
            cancellationToken);
    }

    public async Task DeleteDefinitionAsync(
        WorkflowDefinitionDeleteRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        await store.Gate.WaitAsync(cancellationToken);
        try {
            store.Definitions.TryGetValue(request.Id, out var versions);
            var current = versions is { Count: > 0 } ? versions[^1] : null;
            if (request.ExpectedVersionId.Value == Guid.Empty || current?.VersionId != request.ExpectedVersionId) {
                throw new WorkflowDefinitionConcurrencyException($"Workflow definition '{request.Id}' changed before deletion.");
            }
            store.Definitions.Remove(request.Id);
        } finally {
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

        issues.AddRange(await providerCompatibility.ValidateDefinitionAsync(
            definition,
            componentSnapshot,
            cancellationToken));

        return new WorkflowValidationResult(issues);
    }

    public Task<IReadOnlyList<WorkflowProviderOption>> ListProviderOptionsAsync(
        CancellationToken cancellationToken = default)
        => providerCompatibility.ListProviderOptionsAsync(cancellationToken);

    public async Task<IReadOnlyList<LlmCallComponent>> ListComponentsAsync(CancellationToken cancellationToken = default)
    {
        await store.Gate.WaitAsync(cancellationToken);
        try
        {
            return store.Components.Values
                .OrderBy(component => component.Name, StringComparer.OrdinalIgnoreCase)
                .Select(SnapshotComponent)
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
            return component is null ? null : SnapshotComponent(component);
        }
        finally
        {
            store.Gate.Release();
        }
    }

    public async Task<LlmCallComponent> SaveComponentAsync(
        LlmCallComponentSaveRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        request = JsonSerializer.Deserialize<LlmCallComponentSaveRequest>(JsonSerializer.Serialize(request))
            ?? throw new InvalidOperationException("The workflow component submission could not be captured.");
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Model);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Instructions);
        ArgumentNullException.ThrowIfNull(request.ModelSettings);
        ArgumentNullException.ThrowIfNull(request.InputShape);
        ArgumentNullException.ThrowIfNull(request.ResultShape);

        var id = request.Id ?? WorkflowComponentId.New();
        LlmCallComponent? original;
        await store.Gate.WaitAsync(cancellationToken);
        try {
            store.Components.TryGetValue(id, out original);
            DemandComponentOriginal(id, request.ExpectedRevision, original);
        } finally {
            store.Gate.Release();
        }
        var now = DateTimeOffset.UtcNow;
        var revision = Guid.NewGuid();
        var component = SnapshotComponent(new LlmCallComponent(
            id,
            request.Name.Trim(),
            request.ProviderProfileId,
            request.Model.Trim(),
            request.Modality,
            request.ModelSettings,
            request.Instructions.Trim(),
            request.InputShape,
            request.ResultShape,
            request.Permissions,
            CreatedAtUtc: original?.CreatedAtUtc ?? now,
            UpdatedAtUtc: now) {
            Revision = revision,
            LifetimeId = original?.LifetimeId ?? revision,
            PromptArtifactId = request.PromptArtifactId ?? original?.PromptArtifactId,
            PromptVersionId = request.PromptVersionId ?? original?.PromptVersionId,
            PromptUpdatedAtUtc = original?.PromptUpdatedAtUtc
        });

        var validation = validator.Validate(
            CreateComponentValidationDefinition(component),
            [component]);
        var providerIssues = await providerCompatibility.ValidateComponentAsync(
            component,
            cancellationToken);
        var issues = validation.Issues.Concat(providerIssues).ToArray();
        if (issues.Length > 0) {
            throw new InvalidOperationException(string.Join(" ", issues.Select(issue => issue.Message)));
        }

        await store.Gate.WaitAsync(cancellationToken);
        try {
            store.Components.TryGetValue(component.Id, out var current);
            DemandComponentOriginal(component.Id, request.ExpectedRevision, current);
            store.Components[component.Id] = component;
            return SnapshotComponent(component);
        } finally {
            store.Gate.Release();
        }
    }

    public async Task DeleteComponentAsync(
        WorkflowComponentDeleteRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        await store.Gate.WaitAsync(cancellationToken);
        try {
            if (!store.Components.TryGetValue(request.Id, out var current) || request.ExpectedRevision == Guid.Empty ||
                current.Revision != request.ExpectedRevision) {
                throw new WorkflowComponentConcurrencyException(request.Id);
            }
            store.Components.Remove(request.Id);
        } finally {
            store.Gate.Release();
        }
    }

    private static void DemandComponentOriginal(WorkflowComponentId id, Guid? expected, LlmCallComponent? current) {
        if (expected == Guid.Empty || current?.Revision != expected) {
            throw new WorkflowComponentConcurrencyException(id);
        }
    }

    private static LlmCallComponent SnapshotComponent(LlmCallComponent component)
        => JsonSerializer.Deserialize<LlmCallComponent>(JsonSerializer.Serialize(component))
            ?? throw new InvalidOperationException("The workflow component snapshot could not be read.");

    public async Task<WorkflowSettings> GetSettingsAsync(CancellationToken cancellationToken = default) {
        await store.Gate.WaitAsync(cancellationToken);
        try {
            return SnapshotSettings(store.Settings);
        } finally {
            store.Gate.Release();
        }
    }

    public async Task<WorkflowSettings> SaveSettingsAsync(
        WorkflowSettings settings,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(settings);
        settings = SnapshotSettings(settings);
        if (settings.ArtifactPolicy.MaxInlinePayloadCharacters <= 0) {
            throw new InvalidOperationException("Workflow artifact inline payload limit must be positive.");
        }

        if (settings.HumanInLoopPolicy.DefaultRequestTimeoutMinutes <= 0) {
            throw new InvalidOperationException("Workflow human-in-loop timeout must be positive.");
        }

        var runtimeIssues = WorkflowRuntimePolicyValidator.ValidateRegisteredBackendAvailability(
            settings.DefaultRuntimePolicy,
            runtimeBackendCatalog);
        if (runtimeIssues.Count > 0) {
            throw new InvalidOperationException(
                $"Workflow default runtime policy is invalid: {string.Join(" ", runtimeIssues.Select(issue => issue.Message))}");
        }

        await store.Gate.WaitAsync(cancellationToken);
        try {
            if (settings.UpdatedAtUtc != store.Settings.UpdatedAtUtc) {
                throw new WorkflowSettingsConcurrencyException();
            }
            var accepted = settings with { UpdatedAtUtc = WorkflowEditRevision.Next(settings.UpdatedAtUtc) };
            store.Settings = accepted;
            return SnapshotSettings(accepted);
        } finally {
            store.Gate.Release();
        }
    }

    private static WorkflowSettings SnapshotSettings(WorkflowSettings settings)
        => JsonSerializer.Deserialize<WorkflowSettings>(JsonSerializer.Serialize(settings))
            ?? throw new InvalidOperationException("The workflow settings snapshot could not be read.");

    private static WorkflowCatalogItem MapCatalogItem(WorkflowDefinition definition)
        => WorkflowStableIdentityLookupService.MapCatalogItem(definition);

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
            node.Settings.ComponentId is not { } componentId ||
            !components.TryGetValue(componentId, out var component))
        {
            return node with { Ports = node.Ports.ToArray() };
        }

        return node with
        {
            Ports = node.Ports.ToArray(),
            Settings = node.Settings with
            {
                ProviderProfileId = node.Settings.ProviderProfileId ?? component.ProviderProfileId,
                Model = string.IsNullOrWhiteSpace(node.Settings.Model)
                    ? component.Model
                    : node.Settings.Model.Trim(),
                Instructions = string.IsNullOrWhiteSpace(node.Settings.Instructions)
                    ? component.Instructions
                    : node.Settings.Instructions
            }
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

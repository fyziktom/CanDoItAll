using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class PersistentWorkflowCatalogService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IWorkflowDefinitionValidator validator,
    IProviderProfileRegistry? providerRegistry = null,
    IProviderProfileService? providerProfileService = null) :
    IWorkflowCatalogService,
    IWorkflowComponentLibraryService,
    IWorkflowSettingsService
{
    private const string DefaultSettingsId = "default";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<WorkflowCatalogItem>> ListDefinitionsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var records = await dbContext.Set<WorkflowDefinitionRecord>()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return records
            .GroupBy(item => item.WorkflowId)
            .Select(group => group
                .OrderByDescending(item => item.UpdatedAtUtc)
                .ThenByDescending(item => item.CreatedAtUtc)
                .First())
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Select(item => new WorkflowCatalogItem(
                new WorkflowId(item.WorkflowId),
                new WorkflowVersionId(item.VersionId),
                item.Name,
                item.Description,
                item.Status,
                item.PreferredBackend,
                item.UpdatedAtUtc))
            .ToArray();
    }

    public async Task<WorkflowDefinitionDetail?> GetDefinitionAsync(
        WorkflowId workflowId,
        WorkflowVersionId? versionId = null,
        CancellationToken cancellationToken = default)
    {
        var definition = await LoadDefinitionAsync(workflowId, versionId, cancellationToken);
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

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var workflowId = request.Id ?? WorkflowId.New();
        var currentRecords = await dbContext.Set<WorkflowDefinitionRecord>()
            .Where(item => item.WorkflowId == workflowId.Value)
            .ToListAsync(cancellationToken);
        var current = currentRecords
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ThenByDescending(item => item.CreatedAtUtc)
            .FirstOrDefault();
        if (request.ExpectedVersionId is { } expectedVersionId &&
            current is not null &&
            current.VersionId != expectedVersionId.Value)
        {
            throw new InvalidOperationException($"Workflow definition '{workflowId}' was updated by another request.");
        }

        var now = DateTimeOffset.UtcNow;
        var definition = new WorkflowDefinition(
            workflowId,
            WorkflowVersionId.New(),
            request.Name.Trim(),
            request.Description.Trim(),
            request.Status,
            SnapshotGraph(request.Graph),
            request.RuntimePolicy,
            current?.CreatedAtUtc ?? now,
            now);

        dbContext.Set<WorkflowDefinitionRecord>().Add(WorkflowDefinitionRecord.FromDefinition(definition));
        await dbContext.SaveChangesAsync(cancellationToken);
        return definition;
    }

    public async Task DeleteDefinitionAsync(
        WorkflowId workflowId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var records = await dbContext.Set<WorkflowDefinitionRecord>()
            .Where(item => item.WorkflowId == workflowId.Value)
            .ToListAsync(cancellationToken);
        if (records.Count == 0)
        {
            return;
        }

        dbContext.RemoveRange(records);
        await dbContext.SaveChangesAsync(cancellationToken);
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
            issues.AddRange(await ValidateProviderCompatibilityAsync(component, cancellationToken));
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

    public async Task<IReadOnlyList<LlmCallComponent>> ListComponentsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var records = await dbContext.Set<WorkflowComponentRecord>()
            .AsNoTracking()
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

        return records
            .Select(item => Deserialize<LlmCallComponent>(item.ComponentJson))
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<LlmCallComponent?> GetComponentAsync(
        WorkflowComponentId componentId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await dbContext.Set<WorkflowComponentRecord>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == componentId.Value, cancellationToken);

        return record is null
            ? null
            : Deserialize<LlmCallComponent>(record.ComponentJson);
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
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var current = request.Id.HasValue
            ? await dbContext.Set<WorkflowComponentRecord>()
                .SingleOrDefaultAsync(item => item.Id == request.Id.Value.Value, cancellationToken)
            : null;
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
            current?.CreatedAtUtc ?? now,
            now);

        var validation = validator.Validate(CreateComponentValidationDefinition(component), [component]);
        var providerIssues = await ValidateProviderCompatibilityAsync(component, cancellationToken);
        var issues = validation.Issues.Concat(providerIssues).ToArray();
        if (issues.Length > 0)
        {
            throw new InvalidOperationException(string.Join(" ", issues.Select(issue => issue.Message)));
        }

        if (current is null)
        {
            dbContext.Set<WorkflowComponentRecord>().Add(WorkflowComponentRecord.FromComponent(component));
        }
        else
        {
            current.Name = component.Name;
            current.ProviderProfileId = component.ProviderProfileId;
            current.Model = component.Model;
            current.Modality = component.Modality;
            current.ComponentJson = Serialize(component);
            current.UpdatedAtUtc = component.UpdatedAtUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return component;
    }

    public async Task DeleteComponentAsync(
        WorkflowComponentId componentId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await dbContext.Set<WorkflowComponentRecord>()
            .SingleOrDefaultAsync(item => item.Id == componentId.Value, cancellationToken);
        if (record is null)
        {
            return;
        }

        dbContext.Remove(record);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<WorkflowSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await dbContext.Set<WorkflowSettingsRecord>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == DefaultSettingsId, cancellationToken);

        return record is null
            ? WorkflowSettings.Default
            : Deserialize<WorkflowSettings>(record.SettingsJson);
    }

    public async Task<WorkflowSettings> SaveSettingsAsync(
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

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var record = await dbContext.Set<WorkflowSettingsRecord>()
            .SingleOrDefaultAsync(item => item.Id == DefaultSettingsId, cancellationToken);
        if (record is null)
        {
            dbContext.Set<WorkflowSettingsRecord>().Add(new WorkflowSettingsRecord
            {
                Id = DefaultSettingsId,
                SettingsJson = Serialize(settings),
                UpdatedAtUtc = now
            });
        }
        else
        {
            record.SettingsJson = Serialize(settings);
            record.UpdatedAtUtc = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return settings;
    }

    private async Task<WorkflowDefinition?> LoadDefinitionAsync(
        WorkflowId workflowId,
        WorkflowVersionId? versionId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = dbContext.Set<WorkflowDefinitionRecord>()
            .AsNoTracking()
            .Where(item => item.WorkflowId == workflowId.Value);
        var record = versionId.HasValue
            ? await query.SingleOrDefaultAsync(item => item.VersionId == versionId.Value.Value, cancellationToken)
            : (await query.ToListAsync(cancellationToken))
                .OrderByDescending(item => item.UpdatedAtUtc)
                .ThenByDescending(item => item.CreatedAtUtc)
                .FirstOrDefault();

        return record is null
            ? null
            : Deserialize<WorkflowDefinition>(record.DefinitionJson);
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

    private static IReadOnlyList<string> BuildModelOptions(ProviderProfile provider)
    {
        return provider.SuggestedModels
            .Prepend(provider.DefaultModel)
            .Where(model => !string.IsNullOrWhiteSpace(model))
            .Select(model => model.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static WorkflowGraph SnapshotGraph(WorkflowGraph graph)
    {
        return new WorkflowGraph(
            graph.StartNodeId,
            graph.Nodes
                .Select(node => node with { Ports = node.Ports.ToArray() })
                .ToArray(),
            graph.Edges.ToArray());
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
                            Instructions: string.Empty,
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

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

    private static T Deserialize<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Stored workflow JSON could not be deserialized as '{typeof(T).Name}'.");
    }
}

public sealed class PersistentWorkflowRunStore(IDbContextFactory<AppDbContext> dbContextFactory) :
    IWorkflowRunStore,
    IWorkflowArtifactStore,
    IWorkflowExternalRequestStore
{
    public async Task SaveRunAsync(
        WorkflowRunSnapshot run,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await dbContext.Set<WorkflowRunRecordEntity>()
            .SingleOrDefaultAsync(item => item.RunId == run.RunId.Value, cancellationToken);
        if (record is null)
        {
            dbContext.Set<WorkflowRunRecordEntity>().Add(WorkflowRunRecordEntity.FromSnapshot(run));
        }
        else
        {
            record.WorkflowId = run.WorkflowId.Value;
            record.VersionId = run.VersionId.Value;
            record.State = run.State;
            record.Backend = run.Backend;
            record.BackendRunId = run.BackendRunId;
            record.Summary = run.Summary;
            record.CreatedAtUtc = run.CreatedAtUtc;
            record.UpdatedAtUtc = run.UpdatedAtUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<WorkflowRunSnapshot?> GetRunAsync(
        WorkflowRunId runId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await dbContext.Set<WorkflowRunRecordEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.RunId == runId.Value, cancellationToken);

        return record?.ToSnapshot();
    }

    public async Task<IReadOnlyList<WorkflowRunSnapshot>> ListRunsAsync(
        WorkflowId? workflowId = null,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = dbContext.Set<WorkflowRunRecordEntity>()
            .AsNoTracking();
        if (workflowId.HasValue)
        {
            query = query.Where(item => item.WorkflowId == workflowId.Value.Value);
        }

        var records = await query
            .ToListAsync(cancellationToken);

        return records
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Select(item => item.ToSnapshot())
            .ToArray();
    }

    public async Task<WorkflowListPage<WorkflowRunSnapshot>> ListRunPageAsync(
        WorkflowRunPageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var pageIndex = NormalizePageIndex(request.PageIndex);
        var pageSize = NormalizePageSize(request.PageSize);
        var query = dbContext.Set<WorkflowRunRecordEntity>()
            .AsNoTracking();
        if (request.WorkflowId.HasValue)
        {
            query = query.Where(item => item.WorkflowId == request.WorkflowId.Value.Value);
        }

        if (request.State.HasValue)
        {
            query = query.Where(item => item.State == request.State.Value);
        }

        if (request.Backend.HasValue)
        {
            query = query.Where(item => item.Backend == request.Backend.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(item => item.Summary.Contains(search) || item.BackendRunId.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var orderedQuery = dbContext.Database.IsSqlite()
            ? query.OrderByDescending(item => item.RunId)
            : query.OrderByDescending(item => item.UpdatedAtUtc).ThenByDescending(item => item.RunId);
        var records = await orderedQuery
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new WorkflowListPage<WorkflowRunSnapshot>(
            records.Select(item => item.ToSnapshot()).ToArray(),
            pageIndex,
            pageSize,
            totalCount);
    }

    public async Task SaveEventAsync(
        WorkflowEventRecord workflowEvent,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await dbContext.Set<WorkflowEventRecordEntity>()
            .SingleOrDefaultAsync(item => item.Id == workflowEvent.Id, cancellationToken);
        if (record is null)
        {
            dbContext.Set<WorkflowEventRecordEntity>().Add(WorkflowEventRecordEntity.FromEvent(workflowEvent));
        }
        else
        {
            record.RunId = workflowEvent.RunId.Value;
            record.Kind = workflowEvent.Kind;
            record.NodeId = workflowEvent.NodeId?.Value;
            record.Message = workflowEvent.Message;
            record.PayloadJson = workflowEvent.PayloadJson;
            record.CreatedAtUtc = workflowEvent.CreatedAtUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkflowEventRecord>> ListEventsAsync(
        WorkflowRunId runId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var records = await dbContext.Set<WorkflowEventRecordEntity>()
            .AsNoTracking()
            .Where(item => item.RunId == runId.Value)
            .ToListAsync(cancellationToken);

        return records
            .OrderBy(item => item.CreatedAtUtc)
            .Select(item => item.ToEvent())
            .ToArray();
    }

    public async Task<WorkflowListPage<WorkflowEventRecord>> ListEventPageAsync(
        WorkflowEventPageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var pageIndex = NormalizePageIndex(request.PageIndex);
        var pageSize = NormalizePageSize(request.PageSize);
        var query = dbContext.Set<WorkflowEventRecordEntity>()
            .AsNoTracking()
            .Where(item => item.RunId == request.RunId.Value);
        var totalCount = await query.CountAsync(cancellationToken);
        var orderedQuery = dbContext.Database.IsSqlite()
            ? query.OrderBy(item => item.Id)
            : query.OrderBy(item => item.CreatedAtUtc).ThenBy(item => item.Id);
        var records = await orderedQuery
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new WorkflowListPage<WorkflowEventRecord>(
            records.Select(item => item.ToEvent()).ToArray(),
            pageIndex,
            pageSize,
            totalCount);
    }

    public async Task SaveExternalRequestAsync(
        WorkflowExternalRequestRecord request,
        CancellationToken cancellationToken = default)
    {
        await SaveExternalRequestCoreAsync(request, cancellationToken);
    }

    public async Task<WorkflowExternalRequestRecord?> GetExternalRequestAsync(
        WorkflowExternalRequestId requestId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await dbContext.Set<WorkflowExternalRequestRecordEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == requestId.Value, cancellationToken);

        return record?.ToRequest();
    }

    public async Task<IReadOnlyList<WorkflowExternalRequestRecord>> ListPendingExternalRequestsAsync(
        WorkflowRunId runId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var records = await dbContext.Set<WorkflowExternalRequestRecordEntity>()
            .AsNoTracking()
            .Where(item => item.RunId == runId.Value && item.RespondedAtUtc == null)
            .ToListAsync(cancellationToken);

        return records
            .OrderBy(item => item.CreatedAtUtc)
            .Select(item => item.ToRequest())
            .ToArray();
    }

    Task<IReadOnlyList<WorkflowExternalRequestRecord>> IWorkflowExternalRequestStore.ListPendingRequestsAsync(
        WorkflowRunId runId,
        CancellationToken cancellationToken)
    {
        return ListPendingExternalRequestsAsync(runId, cancellationToken);
    }

    public Task<WorkflowExternalRequestRecord> SaveRequestAsync(
        WorkflowExternalRequestRecord request,
        CancellationToken cancellationToken = default)
    {
        return SaveExternalRequestCoreAsync(request, cancellationToken);
    }

    public async Task<WorkflowExternalRequestRecord> MarkRespondedAsync(
        WorkflowExternalRequestId requestId,
        string responseJson,
        DateTimeOffset respondedAtUtc,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await dbContext.Set<WorkflowExternalRequestRecordEntity>()
            .SingleOrDefaultAsync(item => item.Id == requestId.Value, cancellationToken)
            ?? throw new KeyNotFoundException($"Workflow external request '{requestId}' was not found.");

        record.ResponseJson = responseJson;
        record.RespondedAtUtc = respondedAtUtc;
        await dbContext.SaveChangesAsync(cancellationToken);
        return record.ToRequest();
    }

    public async Task SaveArtifactAsync(
        WorkflowArtifactRecord artifact,
        CancellationToken cancellationToken = default)
    {
        await SaveArtifactCoreAsync(artifact, cancellationToken);
    }

    async Task<WorkflowArtifactRecord> IWorkflowArtifactStore.SaveArtifactAsync(
        WorkflowArtifactRecord artifact,
        CancellationToken cancellationToken)
    {
        return await SaveArtifactCoreAsync(artifact, cancellationToken);
    }

    public async Task<IReadOnlyList<WorkflowArtifactRecord>> ListArtifactsAsync(
        WorkflowRunId runId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var records = await dbContext.Set<WorkflowArtifactRecordEntity>()
            .AsNoTracking()
            .Where(item => item.RunId == runId.Value)
            .ToListAsync(cancellationToken);

        return records
            .OrderBy(item => item.CreatedAtUtc)
            .Select(item => item.ToArtifact())
            .ToArray();
    }

    private async Task<WorkflowExternalRequestRecord> SaveExternalRequestCoreAsync(
        WorkflowExternalRequestRecord request,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await dbContext.Set<WorkflowExternalRequestRecordEntity>()
            .SingleOrDefaultAsync(item => item.Id == request.Id.Value, cancellationToken);
        if (record is null)
        {
            dbContext.Set<WorkflowExternalRequestRecordEntity>().Add(WorkflowExternalRequestRecordEntity.FromRequest(request));
        }
        else
        {
            record.RunId = request.RunId.Value;
            record.Kind = request.Kind;
            record.NodeId = request.NodeId.Value;
            record.EventName = request.EventName;
            record.RequestJson = request.RequestJson;
            record.ResponseJson = request.ResponseJson;
            record.CreatedAtUtc = request.CreatedAtUtc;
            record.RespondedAtUtc = request.RespondedAtUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return request;
    }

    private async Task<WorkflowArtifactRecord> SaveArtifactCoreAsync(
        WorkflowArtifactRecord artifact,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await dbContext.Set<WorkflowArtifactRecordEntity>()
            .SingleOrDefaultAsync(item => item.Id == artifact.Id.Value, cancellationToken);
        if (record is null)
        {
            dbContext.Set<WorkflowArtifactRecordEntity>().Add(WorkflowArtifactRecordEntity.FromArtifact(artifact));
        }
        else
        {
            record.RunId = artifact.RunId.Value;
            record.Kind = artifact.Kind;
            record.NodeId = artifact.NodeId?.Value;
            record.Name = artifact.Name;
            record.ContentType = artifact.ContentType;
            record.StoragePath = artifact.StoragePath;
            record.Summary = artifact.Summary;
            record.CreatedAtUtc = artifact.CreatedAtUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return artifact;
    }

    private static int NormalizePageIndex(int pageIndex)
        => Math.Max(0, pageIndex);

    private static int NormalizePageSize(int pageSize)
        => Math.Clamp(pageSize, 1, 100);
}

public sealed class WorkflowDefinitionRecord
{
    public Guid WorkflowId { get; set; }

    public Guid VersionId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public WorkflowLifecycleStatus Status { get; set; }

    public WorkflowRuntimeBackendKind PreferredBackend { get; set; }

    public string DefinitionJson { get; set; } = "{}";

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public static WorkflowDefinitionRecord FromDefinition(WorkflowDefinition definition) => new()
    {
        WorkflowId = definition.Id.Value,
        VersionId = definition.VersionId.Value,
        Name = definition.Name,
        Description = definition.Description,
        Status = definition.Status,
        PreferredBackend = definition.RuntimePolicy.PreferredBackend,
        DefinitionJson = JsonSerializer.Serialize(definition, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
        CreatedAtUtc = definition.CreatedAtUtc,
        UpdatedAtUtc = definition.UpdatedAtUtc
    };
}

public sealed class WorkflowComponentRecord
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public Guid? ProviderProfileId { get; set; }

    public string Model { get; set; } = string.Empty;

    public WorkflowModality Modality { get; set; }

    public string ComponentJson { get; set; } = "{}";

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public static WorkflowComponentRecord FromComponent(LlmCallComponent component) => new()
    {
        Id = component.Id.Value,
        Name = component.Name,
        ProviderProfileId = component.ProviderProfileId,
        Model = component.Model,
        Modality = component.Modality,
        ComponentJson = JsonSerializer.Serialize(component, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
        CreatedAtUtc = component.CreatedAtUtc,
        UpdatedAtUtc = component.UpdatedAtUtc
    };
}

public sealed class WorkflowSettingsRecord
{
    public string Id { get; set; } = string.Empty;

    public string SettingsJson { get; set; } = "{}";

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class WorkflowRunRecordEntity
{
    public Guid RunId { get; set; }

    public Guid WorkflowId { get; set; }

    public Guid VersionId { get; set; }

    public WorkflowRunState State { get; set; }

    public WorkflowRuntimeBackendKind Backend { get; set; }

    public string BackendRunId { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public static WorkflowRunRecordEntity FromSnapshot(WorkflowRunSnapshot run) => new()
    {
        RunId = run.RunId.Value,
        WorkflowId = run.WorkflowId.Value,
        VersionId = run.VersionId.Value,
        State = run.State,
        Backend = run.Backend,
        BackendRunId = run.BackendRunId,
        Summary = run.Summary,
        CreatedAtUtc = run.CreatedAtUtc,
        UpdatedAtUtc = run.UpdatedAtUtc
    };

    public WorkflowRunSnapshot ToSnapshot() => new(
        new WorkflowRunId(RunId),
        new WorkflowId(WorkflowId),
        new WorkflowVersionId(VersionId),
        State,
        Backend,
        BackendRunId,
        Summary,
        CreatedAtUtc,
        UpdatedAtUtc);
}

public sealed class WorkflowEventRecordEntity
{
    public Guid Id { get; set; }

    public Guid RunId { get; set; }

    public WorkflowEventKind Kind { get; set; }

    public string? NodeId { get; set; }

    public string Message { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public static WorkflowEventRecordEntity FromEvent(WorkflowEventRecord workflowEvent) => new()
    {
        Id = workflowEvent.Id,
        RunId = workflowEvent.RunId.Value,
        Kind = workflowEvent.Kind,
        NodeId = workflowEvent.NodeId?.Value,
        Message = workflowEvent.Message,
        PayloadJson = workflowEvent.PayloadJson,
        CreatedAtUtc = workflowEvent.CreatedAtUtc
    };

    public WorkflowEventRecord ToEvent() => new(
        Id,
        new WorkflowRunId(RunId),
        Kind,
        string.IsNullOrWhiteSpace(NodeId) ? null : new WorkflowNodeId(NodeId),
        Message,
        PayloadJson,
        CreatedAtUtc);
}

public sealed class WorkflowExternalRequestRecordEntity
{
    public Guid Id { get; set; }

    public Guid RunId { get; set; }

    public WorkflowExternalRequestKind Kind { get; set; }

    public string NodeId { get; set; } = string.Empty;

    public string EventName { get; set; } = string.Empty;

    public string RequestJson { get; set; } = string.Empty;

    public string ResponseJson { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? RespondedAtUtc { get; set; }

    public static WorkflowExternalRequestRecordEntity FromRequest(WorkflowExternalRequestRecord request) => new()
    {
        Id = request.Id.Value,
        RunId = request.RunId.Value,
        Kind = request.Kind,
        NodeId = request.NodeId.Value,
        EventName = request.EventName,
        RequestJson = request.RequestJson,
        ResponseJson = request.ResponseJson,
        CreatedAtUtc = request.CreatedAtUtc,
        RespondedAtUtc = request.RespondedAtUtc
    };

    public WorkflowExternalRequestRecord ToRequest() => new(
        new WorkflowExternalRequestId(Id),
        new WorkflowRunId(RunId),
        Kind,
        new WorkflowNodeId(NodeId),
        EventName,
        RequestJson,
        ResponseJson,
        CreatedAtUtc,
        RespondedAtUtc);
}

public sealed class WorkflowArtifactRecordEntity
{
    public Guid Id { get; set; }

    public Guid RunId { get; set; }

    public WorkflowArtifactKind Kind { get; set; }

    public string? NodeId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public string StoragePath { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public static WorkflowArtifactRecordEntity FromArtifact(WorkflowArtifactRecord artifact) => new()
    {
        Id = artifact.Id.Value,
        RunId = artifact.RunId.Value,
        Kind = artifact.Kind,
        NodeId = artifact.NodeId?.Value,
        Name = artifact.Name,
        ContentType = artifact.ContentType,
        StoragePath = artifact.StoragePath,
        Summary = artifact.Summary,
        CreatedAtUtc = artifact.CreatedAtUtc
    };

    public WorkflowArtifactRecord ToArtifact() => new(
        new WorkflowArtifactId(Id),
        new WorkflowRunId(RunId),
        Kind,
        string.IsNullOrWhiteSpace(NodeId) ? null : new WorkflowNodeId(NodeId),
        Name,
        ContentType,
        StoragePath,
        Summary,
        CreatedAtUtc);
}

internal sealed class WorkflowDefinitionRecordConfiguration : IEntityTypeConfiguration<WorkflowDefinitionRecord>
{
    public void Configure(EntityTypeBuilder<WorkflowDefinitionRecord> builder)
    {
        builder.ToTable("AgentFramework_WorkflowDefinitions");
        builder.HasKey(item => item.VersionId);
        builder.Property(item => item.Name).HasMaxLength(240).IsRequired();
        builder.Property(item => item.Description).HasMaxLength(2000);
        builder.Property(item => item.Status).HasConversion<int>();
        builder.Property(item => item.PreferredBackend).HasConversion<int>();
        builder.Property(item => item.DefinitionJson).HasColumnType("TEXT");
        builder.HasIndex(item => item.WorkflowId);
        builder.HasIndex(item => new { item.WorkflowId, item.UpdatedAtUtc });
    }
}

internal sealed class WorkflowComponentRecordConfiguration : IEntityTypeConfiguration<WorkflowComponentRecord>
{
    public void Configure(EntityTypeBuilder<WorkflowComponentRecord> builder)
    {
        builder.ToTable("AgentFramework_WorkflowComponents");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Name).HasMaxLength(240).IsRequired();
        builder.Property(item => item.Model).HasMaxLength(240).IsRequired();
        builder.Property(item => item.Modality).HasConversion<int>();
        builder.Property(item => item.ComponentJson).HasColumnType("TEXT");
        builder.HasIndex(item => item.Name);
        builder.HasIndex(item => item.ProviderProfileId);
    }
}

internal sealed class WorkflowSettingsRecordConfiguration : IEntityTypeConfiguration<WorkflowSettingsRecord>
{
    public void Configure(EntityTypeBuilder<WorkflowSettingsRecord> builder)
    {
        builder.ToTable("AgentFramework_WorkflowSettings");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasMaxLength(64);
        builder.Property(item => item.SettingsJson).HasColumnType("TEXT");
    }
}

internal sealed class WorkflowRunRecordEntityConfiguration : IEntityTypeConfiguration<WorkflowRunRecordEntity>
{
    public void Configure(EntityTypeBuilder<WorkflowRunRecordEntity> builder)
    {
        builder.ToTable("AgentFramework_WorkflowRuns");
        builder.HasKey(item => item.RunId);
        builder.Property(item => item.State).HasConversion<int>();
        builder.Property(item => item.Backend).HasConversion<int>();
        builder.Property(item => item.BackendRunId).HasMaxLength(300);
        builder.Property(item => item.Summary).HasColumnType("TEXT");
        builder.HasIndex(item => item.WorkflowId);
        builder.HasIndex(item => item.UpdatedAtUtc);
    }
}

internal sealed class WorkflowEventRecordEntityConfiguration : IEntityTypeConfiguration<WorkflowEventRecordEntity>
{
    public void Configure(EntityTypeBuilder<WorkflowEventRecordEntity> builder)
    {
        builder.ToTable("AgentFramework_WorkflowEvents");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Kind).HasConversion<int>();
        builder.Property(item => item.NodeId).HasMaxLength(200);
        builder.Property(item => item.Message).HasColumnType("TEXT");
        builder.Property(item => item.PayloadJson).HasColumnType("TEXT");
        builder.HasIndex(item => new { item.RunId, item.CreatedAtUtc });
    }
}

internal sealed class WorkflowExternalRequestRecordEntityConfiguration : IEntityTypeConfiguration<WorkflowExternalRequestRecordEntity>
{
    public void Configure(EntityTypeBuilder<WorkflowExternalRequestRecordEntity> builder)
    {
        builder.ToTable("AgentFramework_WorkflowExternalRequests");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Kind).HasConversion<int>();
        builder.Property(item => item.NodeId).HasMaxLength(200).IsRequired();
        builder.Property(item => item.EventName).HasMaxLength(240).IsRequired();
        builder.Property(item => item.RequestJson).HasColumnType("TEXT");
        builder.Property(item => item.ResponseJson).HasColumnType("TEXT");
        builder.HasIndex(item => new { item.RunId, item.RespondedAtUtc });
    }
}

internal sealed class WorkflowArtifactRecordEntityConfiguration : IEntityTypeConfiguration<WorkflowArtifactRecordEntity>
{
    public void Configure(EntityTypeBuilder<WorkflowArtifactRecordEntity> builder)
    {
        builder.ToTable("AgentFramework_WorkflowArtifacts");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Kind).HasConversion<int>();
        builder.Property(item => item.NodeId).HasMaxLength(200);
        builder.Property(item => item.Name).HasMaxLength(300).IsRequired();
        builder.Property(item => item.ContentType).HasMaxLength(200);
        builder.Property(item => item.StoragePath).HasMaxLength(1200);
        builder.Property(item => item.Summary).HasColumnType("TEXT");
        builder.HasIndex(item => new { item.RunId, item.CreatedAtUtc });
    }
}

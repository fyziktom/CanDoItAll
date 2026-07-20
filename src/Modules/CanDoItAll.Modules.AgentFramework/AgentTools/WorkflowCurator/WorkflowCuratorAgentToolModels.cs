using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.Modules.AgentFramework;

public sealed record WorkflowCuratorCatalogSearchInput
{
    [JsonConstructor]
    public WorkflowCuratorCatalogSearchInput(
        string? text = null,
        WorkflowLifecycleStatus? status = null,
        int pageIndex = 0,
        int pageSize = WorkflowCatalogSearchQuery.DefaultPageSize)
    {
        var query = new WorkflowCatalogSearchQuery(text, status, pageIndex, pageSize);
        Text = query.Text;
        Status = query.Status;
        PageIndex = query.PageIndex;
        PageSize = query.PageSize;
    }

    public string? Text { get; }

    public WorkflowLifecycleStatus? Status { get; }

    public int PageIndex { get; }

    public int PageSize { get; }
}

public sealed record WorkflowCuratorDefinitionEditorInput
{
    [JsonConstructor]
    public WorkflowCuratorDefinitionEditorInput(Guid workflowId, Guid? versionId = null)
    {
        if (workflowId == Guid.Empty)
        {
            throw new ArgumentException("Workflow id cannot be empty.", nameof(workflowId));
        }

        if (versionId == Guid.Empty)
        {
            throw new ArgumentException("Workflow version id cannot be empty.", nameof(versionId));
        }

        WorkflowId = workflowId;
        VersionId = versionId;
    }

    public Guid WorkflowId { get; }

    public Guid? VersionId { get; }
}

public sealed record WorkflowCuratorRuntimePolicyInput
{
    [JsonConstructor]
    public WorkflowCuratorRuntimePolicyInput(
        WorkflowRuntimeBackendKind preferredBackend = WorkflowRuntimeBackendKind.InProcess,
        bool allowInProcessPreviewRuns = true,
        bool requireDurableProductionRuns = false,
        bool exposeAzureFunctionsStatusEndpoint = false,
        bool exposeAzureFunctionsMcpTool = false)
    {
        if (!Enum.IsDefined(preferredBackend))
        {
            throw new ArgumentOutOfRangeException(
                nameof(preferredBackend),
                preferredBackend,
                "Workflow runtime backend is not defined.");
        }

        PreferredBackend = preferredBackend;
        AllowInProcessPreviewRuns = allowInProcessPreviewRuns;
        RequireDurableProductionRuns = requireDurableProductionRuns;
        ExposeAzureFunctionsStatusEndpoint = exposeAzureFunctionsStatusEndpoint;
        ExposeAzureFunctionsMcpTool = exposeAzureFunctionsMcpTool;
    }

    public WorkflowRuntimeBackendKind PreferredBackend { get; }

    public bool AllowInProcessPreviewRuns { get; }

    public bool RequireDurableProductionRuns { get; }

    public bool ExposeAzureFunctionsStatusEndpoint { get; }

    public bool ExposeAzureFunctionsMcpTool { get; }
}

public sealed record WorkflowCuratorValueShapeInput
{
    [JsonConstructor]
    public WorkflowCuratorValueShapeInput(
        WorkflowValueShapeKind kind = WorkflowValueShapeKind.Text,
        string? schemaJson = null,
        string? description = null)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Workflow value shape is not defined.");
        }

        Kind = kind;
        SchemaJson = schemaJson ?? string.Empty;
        Description = description ?? GetDefaultDescription(kind);
    }

    public WorkflowValueShapeKind Kind { get; }

    public string SchemaJson { get; }

    public string Description { get; }

    internal WorkflowValueShape ToValueShape()
        => new(Kind, SchemaJson, Description);

    private static string GetDefaultDescription(WorkflowValueShapeKind kind)
        => kind == WorkflowValueShapeKind.Text ? WorkflowValueShape.Text.Description : kind.ToString();
}

public sealed record WorkflowCuratorPortInput
{
    [JsonConstructor]
    public WorkflowCuratorPortInput(
        string id,
        string? name = null,
        WorkflowPortDirection direction = WorkflowPortDirection.Input,
        WorkflowCuratorValueShapeInput? shape = null,
        bool required = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!Enum.IsDefined(direction))
        {
            throw new ArgumentOutOfRangeException(nameof(direction), direction, "Workflow port direction is not defined.");
        }

        Id = id.Trim();
        Name = name ?? Id;
        Direction = direction;
        Shape = shape ?? new WorkflowCuratorValueShapeInput();
        Required = required;
    }

    public string Id { get; }

    public string Name { get; }

    public WorkflowPortDirection Direction { get; }

    public WorkflowCuratorValueShapeInput Shape { get; }

    public bool Required { get; }
}

public enum WorkflowCuratorNodeOmittedValueBehavior
{
    ApplyAuthoringDefaults,
    PreserveNulls
}

public sealed record WorkflowCuratorNodeInput
{
    [JsonConstructor]
    public WorkflowCuratorNodeInput(
        string id,
        WorkflowNodeKind kind,
        string? name = null,
        string? instructions = null,
        WorkflowCuratorValueShapeInput? inputShape = null,
        WorkflowCuratorValueShapeInput? resultShape = null,
        IReadOnlyList<WorkflowCuratorPortInput>? ports = null,
        Guid? componentId = null,
        Guid? providerProfileId = null,
        string? model = null,
        Guid? agentId = null,
        Guid? subworkflowId = null,
        WorkflowExternalRequestKind? externalRequestKind = null,
        string? executorId = null,
        string? executorSettingsJson = null,
        WorkflowExecutorExecutionPolicy? executionPolicy = null,
        double canvasX = 0,
        double canvasY = 0,
        WorkflowCuratorNodeOmittedValueBehavior omittedValueBehavior =
            WorkflowCuratorNodeOmittedValueBehavior.ApplyAuthoringDefaults)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Workflow node kind is not defined.");
        }

        if (componentId == Guid.Empty || providerProfileId == Guid.Empty || agentId == Guid.Empty || subworkflowId == Guid.Empty)
        {
            throw new ArgumentException("Optional workflow identifiers cannot be empty GUIDs.");
        }

        if (executionPolicy is not null && !WorkflowExecutorPolicyLimits.IsValid(executionPolicy))
        {
            throw new ArgumentOutOfRangeException(nameof(executionPolicy), "Workflow executor policy limits are invalid.");
        }

        if (!Enum.IsDefined(omittedValueBehavior))
        {
            throw new ArgumentOutOfRangeException(
                nameof(omittedValueBehavior),
                omittedValueBehavior,
                "Workflow node omitted-value behavior is not defined.");
        }

        Id = id.Trim();
        Kind = kind;
        Name = string.IsNullOrWhiteSpace(name) ? kind.ToString() : name.Trim();
        Instructions = instructions?.Trim() ?? string.Empty;
        InputShape = inputShape;
        ResultShape = resultShape;
        Ports = ports ?? [];
        ComponentId = componentId;
        ProviderProfileId = providerProfileId;
        Model = model?.Trim() ?? string.Empty;
        AgentId = agentId;
        SubworkflowId = subworkflowId;
        ExternalRequestKind = externalRequestKind;
        ExecutorId = string.IsNullOrWhiteSpace(executorId) ? null : executorId.Trim();
        ExecutorSettingsJson = executorSettingsJson?.Trim() ?? string.Empty;
        ExecutionPolicy = executionPolicy;
        CanvasX = canvasX;
        CanvasY = canvasY;
        OmittedValueBehavior = omittedValueBehavior;
    }

    public string Id { get; }

    public WorkflowNodeKind Kind { get; }

    public string Name { get; }

    public string Instructions { get; }

    public WorkflowCuratorValueShapeInput? InputShape { get; }

    public WorkflowCuratorValueShapeInput? ResultShape { get; }

    public IReadOnlyList<WorkflowCuratorPortInput> Ports { get; }

    public Guid? ComponentId { get; }

    public Guid? ProviderProfileId { get; }

    public string Model { get; }

    public Guid? AgentId { get; }

    public Guid? SubworkflowId { get; }

    public WorkflowExternalRequestKind? ExternalRequestKind { get; }

    public string? ExecutorId { get; }

    public string ExecutorSettingsJson { get; }

    public WorkflowExecutorExecutionPolicy? ExecutionPolicy { get; }

    public double CanvasX { get; }

    public double CanvasY { get; }

    public WorkflowCuratorNodeOmittedValueBehavior OmittedValueBehavior { get; }
}

public sealed record WorkflowCuratorEdgeInput
{
    [JsonConstructor]
    public WorkflowCuratorEdgeInput(
        string sourceNodeId,
        string targetNodeId,
        string? id = null,
        WorkflowEdgeKind kind = WorkflowEdgeKind.Direct,
        WorkflowRouteKind routeKind = WorkflowRouteKind.Always,
        string? label = null,
        string? jsonPath = null,
        WorkflowRouteOperator routeOperator = WorkflowRouteOperator.Exists,
        string? expectedValueJson = null,
        WorkflowRouteValueKind expectedValueKind = WorkflowRouteValueKind.Json,
        bool caseSensitive = false,
        int? fanOutTargetIndex = null,
        string? sourcePortId = null,
        string? targetPortId = null,
        string? conditionExpression = null,
        string? routingLanguage = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceNodeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetNodeId);
        if (!Enum.IsDefined(kind) || !Enum.IsDefined(routeKind) || !Enum.IsDefined(routeOperator) || !Enum.IsDefined(expectedValueKind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), "Workflow edge routing value is not defined.");
        }

        SourceNodeId = sourceNodeId.Trim();
        TargetNodeId = targetNodeId.Trim();
        Id = string.IsNullOrWhiteSpace(id)
            ? $"{SourceNodeId}-to-{TargetNodeId}"
            : id.Trim();
        Kind = kind;
        RouteKind = routeKind;
        Label = label?.Trim() ?? string.Empty;
        JsonPath = jsonPath?.Trim() ?? string.Empty;
        RouteOperator = routeOperator;
        ExpectedValueJson = expectedValueJson?.Trim() ?? string.Empty;
        ExpectedValueKind = expectedValueKind;
        CaseSensitive = caseSensitive;
        FanOutTargetIndex = fanOutTargetIndex;
        SourcePortId = sourcePortId is null ? null : new WorkflowPortId(sourcePortId).Value;
        TargetPortId = targetPortId is null ? null : new WorkflowPortId(targetPortId).Value;
        ConditionExpression = conditionExpression ?? string.Empty;
        RoutingLanguage = routingLanguage ?? WorkflowRoutingLanguages.BuiltInJsonV1;
    }

    public string Id { get; }

    public string SourceNodeId { get; }

    public string TargetNodeId { get; }

    public string? SourcePortId { get; }

    public string? TargetPortId { get; }

    public WorkflowEdgeKind Kind { get; }

    public WorkflowRouteKind RouteKind { get; }

    public string Label { get; }

    public string JsonPath { get; }

    public WorkflowRouteOperator RouteOperator { get; }

    public string ExpectedValueJson { get; }

    public WorkflowRouteValueKind ExpectedValueKind { get; }

    public bool CaseSensitive { get; }

    public int? FanOutTargetIndex { get; }

    public string ConditionExpression { get; }

    public string RoutingLanguage { get; }
}

public sealed record WorkflowCuratorDraftCreateInput
{
    [JsonConstructor]
    public WorkflowCuratorDraftCreateInput(
        string name,
        string? description = null,
        string? startNodeId = null,
        IReadOnlyList<WorkflowCuratorNodeInput>? nodes = null,
        IReadOnlyList<WorkflowCuratorEdgeInput>? edges = null,
        WorkflowCuratorRuntimePolicyInput? runtimePolicy = null,
        IReadOnlyList<WorkflowInputParameterDescriptor>? inputParameters = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        StartNodeId = startNodeId?.Trim();
        Nodes = nodes ?? [];
        Edges = edges ?? [];
        RuntimePolicy = runtimePolicy ?? new WorkflowCuratorRuntimePolicyInput();
        InputParameters = inputParameters ?? [];
    }

    public string Name { get; }

    public string Description { get; }

    public string? StartNodeId { get; }

    public IReadOnlyList<WorkflowCuratorNodeInput> Nodes { get; }

    public IReadOnlyList<WorkflowCuratorEdgeInput> Edges { get; }

    public WorkflowCuratorRuntimePolicyInput RuntimePolicy { get; }

    public IReadOnlyList<WorkflowInputParameterDescriptor> InputParameters { get; }
}

public sealed record WorkflowCuratorDraftUpdateInput
{
    [JsonConstructor]
    public WorkflowCuratorDraftUpdateInput(
        Guid workflowId,
        Guid expectedVersionId,
        string? name = null,
        string? description = null,
        string? startNodeId = null,
        IReadOnlyList<WorkflowCuratorNodeInput>? nodes = null,
        IReadOnlyList<WorkflowCuratorEdgeInput>? edges = null,
        WorkflowCuratorRuntimePolicyInput? runtimePolicy = null,
        IReadOnlyList<WorkflowInputParameterDescriptor>? inputParameters = null)
    {
        ValidateWorkflowVersionIds(workflowId, expectedVersionId);
        WorkflowId = workflowId;
        ExpectedVersionId = expectedVersionId;
        Name = name?.Trim();
        Description = description?.Trim();
        StartNodeId = startNodeId?.Trim();
        Nodes = nodes;
        Edges = edges;
        RuntimePolicy = runtimePolicy;
        InputParameters = inputParameters;
    }

    public Guid WorkflowId { get; }

    public Guid ExpectedVersionId { get; }

    public string? Name { get; }

    public string? Description { get; }

    public string? StartNodeId { get; }

    public IReadOnlyList<WorkflowCuratorNodeInput>? Nodes { get; }

    public IReadOnlyList<WorkflowCuratorEdgeInput>? Edges { get; }

    public WorkflowCuratorRuntimePolicyInput? RuntimePolicy { get; }

    public IReadOnlyList<WorkflowInputParameterDescriptor>? InputParameters { get; }

    internal static void ValidateWorkflowVersionIds(Guid workflowId, Guid expectedVersionId)
    {
        if (workflowId == Guid.Empty)
        {
            throw new ArgumentException("Workflow id cannot be empty.", nameof(workflowId));
        }

        if (expectedVersionId == Guid.Empty)
        {
            throw new ArgumentException("Expected workflow version id cannot be empty.", nameof(expectedVersionId));
        }
    }
}

public sealed record WorkflowCuratorNodeUpdateInput
{
    [JsonConstructor]
    public WorkflowCuratorNodeUpdateInput(
        Guid workflowId,
        Guid expectedVersionId,
        string nodeId,
        string? name = null,
        string? instructions = null,
        string? model = null,
        string? executorSettingsJson = null)
    {
        WorkflowCuratorDraftUpdateInput.ValidateWorkflowVersionIds(workflowId, expectedVersionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        if (name is null && instructions is null && model is null && executorSettingsJson is null)
        {
            throw new ArgumentException("At least one workflow node field must be supplied.");
        }

        WorkflowId = workflowId;
        ExpectedVersionId = expectedVersionId;
        NodeId = nodeId.Trim();
        Name = name?.Trim();
        Instructions = instructions?.Trim();
        Model = model?.Trim();
        ExecutorSettingsJson = executorSettingsJson?.Trim();
    }

    public Guid WorkflowId { get; }

    public Guid ExpectedVersionId { get; }

    public string NodeId { get; }

    public string? Name { get; }

    public string? Instructions { get; }

    public string? Model { get; }

    public string? ExecutorSettingsJson { get; }
}

public sealed record WorkflowCuratorLifecycleChangeInput
{
    [JsonConstructor]
    public WorkflowCuratorLifecycleChangeInput(
        Guid workflowId,
        Guid expectedVersionId,
        WorkflowLifecycleStatus status)
    {
        WorkflowCuratorDraftUpdateInput.ValidateWorkflowVersionIds(workflowId, expectedVersionId);
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Workflow lifecycle status is not defined.");
        }

        WorkflowId = workflowId;
        ExpectedVersionId = expectedVersionId;
        Status = status;
    }

    public Guid WorkflowId { get; }

    public Guid ExpectedVersionId { get; }

    public WorkflowLifecycleStatus Status { get; }
}

public sealed record WorkflowCuratorCatalogSearchResult(
    IReadOnlyList<WorkflowCatalogItem> Items,
    int PageIndex,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record WorkflowCuratorDefinitionEditorResult(
    WorkflowDefinition Definition,
    WorkflowValidationResult Validation);

public sealed record WorkflowCuratorComponentOption(
    Guid ComponentId,
    string Name,
    Guid? ProviderProfileId,
    string Model,
    string Instructions,
    WorkflowValueShape InputShape,
    WorkflowValueShape ResultShape);

public sealed record WorkflowCuratorExecutorOption(
    string ExecutorId,
    string Name,
    string Description,
    WorkflowExecutorCategoryKind Category,
    bool CanExecute,
    WorkflowValueShape InputShape,
    WorkflowValueShape ResultShape,
    string DefaultSettingsJson,
    string SettingsSchemaJson,
    WorkflowExecutorExecutionPolicy DefaultPolicy);

public sealed record WorkflowCuratorAuthoringOptionsResult(
    IReadOnlyList<WorkflowProviderOption> Providers,
    IReadOnlyList<WorkflowCuratorComponentOption> Components,
    IReadOnlyList<WorkflowCuratorExecutorOption> Executors,
    IReadOnlyList<WorkflowRuntimeBackendDescriptor> RuntimeBackends);

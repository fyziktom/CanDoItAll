using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class WorkflowAgentRuntimeToolProvider : IAgentRuntimeToolProvider
{
    public const string ProviderKey = "workflows.runtime-tools";

    private const int ProviderOrder = 940;

    private readonly IWorkflowCatalogService catalog;
    private readonly IWorkflowLaunchService launchService;
    private readonly IWorkflowRuntimeManager runtimeManager;
    private readonly WorkflowAgentRuntimeAuthorizationService authorizationService;

    public WorkflowAgentRuntimeToolProvider(
        IWorkflowCatalogService catalog,
        IWorkflowLaunchService launchService,
        IWorkflowRuntimeManager runtimeManager,
        WorkflowAgentRuntimeAuthorizationService authorizationService)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(launchService);
        ArgumentNullException.ThrowIfNull(runtimeManager);
        ArgumentNullException.ThrowIfNull(authorizationService);
        this.catalog = catalog;
        this.launchService = launchService;
        this.runtimeManager = runtimeManager;
        this.authorizationService = authorizationService;
    }

    public int Order => ProviderOrder;

    public AgentRuntimeToolProviderDescriptor Descriptor { get; } = new(
        ProviderKey,
        "Workflow runtime tools",
        "Lists and launches governed workflows and inspects or controls their runtime lifecycle.",
        ["agent-framework", "workflow", "runtime"],
        WorkflowAgentRuntimeAuthorizationPolicy.SupportedPurposes);

    public ValueTask<IReadOnlyList<AITool>> CreateToolsAsync(
        AgentRuntimeToolProviderContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (!WorkflowAgentRuntimeAuthorizationPolicy.CanAttach(context))
        {
            return ValueTask.FromResult<IReadOnlyList<AITool>>([]);
        }

        var tools = new List<AITool>(WorkflowAgentCapabilityKeys.ToolNameToCapabilityKey.Count);
        AddToolIfAuthorized(
            tools,
            context,
            AgentToolInvocationPolicyMetadata.WorkflowsDefinitionsList,
            () => AIFunctionFactory.Create(
                (CancellationToken token = default) => ExecuteAuthorizedAsync(
                    context.Agent.Id,
                    AgentToolInvocationPolicyMetadata.WorkflowsDefinitionsList,
                    ListActiveDefinitionsAsync,
                    token),
                AgentToolInvocationPolicyMetadata.WorkflowsDefinitionsList,
                "Lists the latest Active version of each saved workflow. Use the returned workflowId and versionId with workflows_run_start."));
        AddToolIfAuthorized(
            tools,
            context,
            AgentToolInvocationPolicyMetadata.WorkflowsRunStart,
            () => AIFunctionFactory.Create(
                (WorkflowAgentStartInput request, CancellationToken token = default) =>
                    ExecuteAuthorizedAsync(
                        context.Agent.Id,
                        AgentToolInvocationPolicyMetadata.WorkflowsRunStart,
                        authorizedToken => StartAsync(context, request, authorizedToken),
                        token),
                AgentToolInvocationPolicyMetadata.WorkflowsRunStart,
                "Starts an Active saved workflow in Production mode and waits until it stops or waits for external input. Select LatestActive or ExactSavedVersion explicitly. Supply a stable idempotencyKey for retries; it is required outside interactive chat. Runtime backend and launch origin are governed by the host."));
        AddToolIfAuthorized(
            tools,
            context,
            AgentToolInvocationPolicyMetadata.WorkflowsRunStatusGet,
            () => AIFunctionFactory.Create(
                (WorkflowAgentRunInput request, CancellationToken token = default) =>
                    ExecuteAuthorizedAsync(
                        context.Agent.Id,
                        AgentToolInvocationPolicyMetadata.WorkflowsRunStatusGet,
                        authorizedToken => GetStatusAsync(request, authorizedToken),
                        token),
                AgentToolInvocationPolicyMetadata.WorkflowsRunStatusGet,
                "Gets the current persisted status of one workflow run."));
        AddToolIfAuthorized(
            tools,
            context,
            AgentToolInvocationPolicyMetadata.WorkflowsRunCancel,
            () => AIFunctionFactory.Create(
                (WorkflowAgentRunInput request, CancellationToken token = default) =>
                    ExecuteAuthorizedAsync(
                        context.Agent.Id,
                        AgentToolInvocationPolicyMetadata.WorkflowsRunCancel,
                        authorizedToken => RequestCancellationAsync(request, authorizedToken),
                        token),
                AgentToolInvocationPolicyMetadata.WorkflowsRunCancel,
                "Requests cancellation for an active workflow run and returns the authoritative capability outcome. A requested cancellation is not terminal until the backend observes it."));
        AddToolIfAuthorized(
            tools,
            context,
            AgentToolInvocationPolicyMetadata.WorkflowsExternalResponseSubmit,
            () => AIFunctionFactory.Create(
                (WorkflowAgentExternalResponseInput request, CancellationToken token = default) =>
                    ExecuteAuthorizedAsync(
                        context.Agent.Id,
                        AgentToolInvocationPolicyMetadata.WorkflowsExternalResponseSubmit,
                        authorizedToken => SubmitExternalResponseAsync(request, authorizedToken),
                        token),
                AgentToolInvocationPolicyMetadata.WorkflowsExternalResponseSubmit,
                "Submits one response to a pending workflow external request. Unsupported backend resume remains explicit and does not fabricate completion."));

        return ValueTask.FromResult<IReadOnlyList<AITool>>(tools);
    }

    public IReadOnlyList<AgentRuntimeToolMetadata> GetToolMetadata(
        AgentRuntimeToolProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!WorkflowAgentRuntimeAuthorizationPolicy.CanAttach(context))
        {
            return [];
        }

        return new[]
        {
            CreateMetadata(
                AgentToolInvocationPolicyMetadata.WorkflowsDefinitionsList,
                AgentRuntimeToolOperationKind.Read,
                requiresApprovalByDefault: false),
            CreateMetadata(
                AgentToolInvocationPolicyMetadata.WorkflowsRunStart,
                AgentRuntimeToolOperationKind.Mutation,
                requiresApprovalByDefault: true),
            CreateMetadata(
                AgentToolInvocationPolicyMetadata.WorkflowsRunStatusGet,
                AgentRuntimeToolOperationKind.Read,
                requiresApprovalByDefault: false),
            CreateMetadata(
                AgentToolInvocationPolicyMetadata.WorkflowsRunCancel,
                AgentRuntimeToolOperationKind.Mutation,
                requiresApprovalByDefault: true),
            CreateMetadata(
                AgentToolInvocationPolicyMetadata.WorkflowsExternalResponseSubmit,
                AgentRuntimeToolOperationKind.Mutation,
                requiresApprovalByDefault: true)
        }
            .Where(item => WorkflowAgentRuntimeAuthorizationPolicy.IsToolAuthorized(
                context.Agent,
                context.Capabilities,
                item.ToolName))
            .ToArray();
    }

    private async Task<WorkflowAgentDefinitionListResult> ListActiveDefinitionsAsync(
        CancellationToken cancellationToken)
    {
        var catalogItems = await catalog.ListDefinitionsAsync(cancellationToken);
        var definitions = new List<WorkflowAgentDefinitionDescriptor>();
        foreach (var workflowId in catalogItems
                     .Select(item => item.Id)
                     .Distinct()
                     .OrderBy(id => id.Value))
        {
            var detail = await catalog.GetLatestDefinitionByStatusAsync(
                workflowId,
                WorkflowLifecycleStatus.Active,
                cancellationToken);
            if (detail is null)
            {
                continue;
            }

            var definition = detail.Definition;
            definitions.Add(new WorkflowAgentDefinitionDescriptor(
                definition.Id.Value,
                definition.VersionId.Value,
                definition.Name,
                definition.Description,
                definition.RuntimePolicy.PreferredBackend,
                detail.Validation.Succeeded,
                detail.Validation.Issues
                    .Take(5)
                    .Select(issue => $"{issue.Code}: {issue.Message}")
                    .ToArray()));
        }

        return new WorkflowAgentDefinitionListResult(
            definitions
                .OrderBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(definition => definition.WorkflowId)
                .ToArray());
    }

    private async Task<WorkflowAgentStartResult> StartAsync(
        AgentRuntimeToolProviderContext context,
        WorkflowAgentStartInput request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var origin = CreateOrigin(context);
        WorkflowDefinitionSelection selection = request.SelectionMode switch
        {
            WorkflowAgentDefinitionSelectionMode.LatestActive =>
                new WorkflowDefinitionSelection.LatestActive(new WorkflowId(request.WorkflowId)),
            WorkflowAgentDefinitionSelectionMode.ExactSavedVersion =>
                new WorkflowDefinitionSelection.ExactSavedVersion(
                    new WorkflowId(request.WorkflowId),
                    new WorkflowVersionId(request.VersionId!.Value)),
            _ => throw new ArgumentOutOfRangeException(
                nameof(request),
                request.SelectionMode,
                "Workflow definition selection mode is not defined.")
        };
        var intent = new WorkflowLaunchIntent(
            selection,
            WorkflowLaunchMode.Production,
            origin,
            request.InputJson,
            WorkflowLaunchCompletionPolicy.WaitForStopped,
            ResolveIdempotency(context, request));
        var result = await launchService.LaunchAsync(intent, cancellationToken);

        return new WorkflowAgentStartResult(
            MapRun(result.Run),
            request.SelectionMode,
            result.ResolvedRequest.Backend.Kind,
            result.IdempotencyDisposition,
            "Workflow launch completed through the governed launch service.");
    }

    private async Task<WorkflowAgentRunStatusResult> GetStatusAsync(
        WorkflowAgentRunInput request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var run = await runtimeManager.GetRunAsync(
            new WorkflowRunId(request.RunId),
            cancellationToken);
        return run is null
            ? new WorkflowAgentRunStatusResult(
                WorkflowAgentRunLookupOutcome.NotFound,
                Run: null,
                $"Workflow run '{request.RunId:D}' was not found.")
            : new WorkflowAgentRunStatusResult(
                WorkflowAgentRunLookupOutcome.Found,
                MapRun(run),
                "Workflow run status was loaded from the authoritative runtime store.");
    }

    private async Task<WorkflowAgentCancellationResult> RequestCancellationAsync(
        WorkflowAgentRunInput request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await runtimeManager.RequestCancellationAsync(
            new WorkflowRunId(request.RunId),
            cancellationToken);
        return new WorkflowAgentCancellationResult(
            result.Outcome,
            result.Succeeded,
            result.Run is null ? null : MapRun(result.Run),
            result.Message);
    }

    private async Task<WorkflowAgentExternalResponseResult> SubmitExternalResponseAsync(
        WorkflowAgentExternalResponseInput request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await runtimeManager.SubmitExternalResponseAsync(
            new WorkflowExternalRequestId(request.ExternalRequestId),
            request.ResponseJson,
            cancellationToken);
        return new WorkflowAgentExternalResponseResult(
            result.Outcome,
            result.Succeeded,
            result.Run is null ? null : MapRun(result.Run),
            result.Request?.Id.Value,
            result.Request?.RespondedAtUtc,
            result.Message);
    }

    private static WorkflowLaunchOrigin.AgentRuntimeInvocation CreateOrigin(
        AgentRuntimeToolProviderContext context)
    {
        if (context.Agent.Id == Guid.Empty)
        {
            throw new InvalidOperationException("Workflow launch requires a persisted agent id.");
        }

        if (string.IsNullOrWhiteSpace(context.RuntimeSessionKey))
        {
            throw new InvalidOperationException(
                "Workflow launch requires the host's runtime session key; the tool cannot invent lineage.");
        }

        var correlationId = ResolveCorrelationId(context);
        return new WorkflowLaunchOrigin.AgentRuntimeInvocation(
            new WorkflowLaunchActor(
                WorkflowLaunchActorKind.Agent,
                context.Agent.Id.ToString("D")),
            new WorkflowLaunchSessionId(context.RuntimeSessionKey),
            context.Purpose.ToString(),
            new WorkflowLaunchCorrelationId(correlationId));
    }

    private static WorkflowLaunchIdempotency ResolveIdempotency(
        AgentRuntimeToolProviderContext context,
        WorkflowAgentStartInput request)
    {
        if (request.IdempotencyKey is not null)
        {
            return new WorkflowLaunchIdempotency.CallerSupplied(
                new WorkflowLaunchIdempotencyKey(request.IdempotencyKey));
        }

        if (context.Purpose != AgentRuntimeToolProviderPurpose.InteractiveChat)
        {
            throw new InvalidOperationException(
                $"Workflow launch purpose '{context.Purpose}' requires a caller-supplied idempotency key so a tool retry cannot create an accidental duplicate run.");
        }

        return new WorkflowLaunchIdempotency.NotRequested();
    }

    private static string ResolveCorrelationId(AgentRuntimeToolProviderContext context)
    {
        if (!string.IsNullOrWhiteSpace(context.ContextIntent.ProcessRunId))
        {
            return context.ContextIntent.ProcessRunId;
        }

        if (!string.IsNullOrWhiteSpace(context.ContextIntent.SourceId))
        {
            return context.ContextIntent.SourceId;
        }

        return context.RuntimeSessionKey;
    }

    private static AgentRuntimeToolMetadata CreateMetadata(
        string toolName,
        AgentRuntimeToolOperationKind operationKind,
        bool requiresApprovalByDefault)
        => new(
            ProviderKey,
            toolName,
            operationKind,
            requiresApprovalByDefault,
            ["workflow", "runtime", "governed-launch"]);

    private static void AddToolIfAuthorized(
        ICollection<AITool> tools,
        AgentRuntimeToolProviderContext context,
        string toolName,
        Func<AITool> createTool)
    {
        if (WorkflowAgentRuntimeAuthorizationPolicy.IsToolAuthorized(
                context.Agent,
                context.Capabilities,
                toolName))
        {
            tools.Add(createTool());
        }
    }

    private async Task<TResult> ExecuteAuthorizedAsync<TResult>(
        Guid actorAgentId,
        string toolName,
        Func<CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken)
    {
        await authorizationService.EnsureToolInvocationAuthorizedAsync(
            actorAgentId,
            toolName,
            cancellationToken);
        return await action(cancellationToken);
    }

    private static WorkflowAgentRunDescriptor MapRun(WorkflowRunSnapshot run)
        => new(
            run.RunId.Value,
            run.WorkflowId.Value,
            run.VersionId.Value,
            run.State,
            run.Backend,
            run.Summary,
            run.CreatedAtUtc,
            run.UpdatedAtUtc,
            run.TerminalAtUtc);
}

public enum WorkflowAgentDefinitionSelectionMode
{
    LatestActive,
    ExactSavedVersion
}

public sealed record WorkflowAgentStartInput
{
    [JsonConstructor]
    public WorkflowAgentStartInput(
        Guid workflowId,
        WorkflowAgentDefinitionSelectionMode selectionMode,
        Guid? versionId = null,
        string inputJson = "{}",
        string? idempotencyKey = null)
    {
        if (workflowId == Guid.Empty)
        {
            throw new ArgumentException("Workflow id cannot be empty.", nameof(workflowId));
        }

        if (!Enum.IsDefined(selectionMode))
        {
            throw new ArgumentOutOfRangeException(nameof(selectionMode), selectionMode, "Workflow selection mode is not defined.");
        }

        if (selectionMode == WorkflowAgentDefinitionSelectionMode.ExactSavedVersion &&
            (!versionId.HasValue || versionId.Value == Guid.Empty))
        {
            throw new ArgumentException("ExactSavedVersion requires a non-empty version id.", nameof(versionId));
        }

        if (selectionMode == WorkflowAgentDefinitionSelectionMode.LatestActive && versionId.HasValue)
        {
            throw new ArgumentException("LatestActive does not accept a version id.", nameof(versionId));
        }

        WorkflowId = workflowId;
        SelectionMode = selectionMode;
        VersionId = versionId;
        InputJson = string.IsNullOrWhiteSpace(inputJson) ? "{}" : inputJson.Trim();
        IdempotencyKey = idempotencyKey is null
            ? null
            : new WorkflowLaunchIdempotencyKey(idempotencyKey).Value;
    }

    public Guid WorkflowId { get; }

    public WorkflowAgentDefinitionSelectionMode SelectionMode { get; }

    public Guid? VersionId { get; }

    public string InputJson { get; }

    public string? IdempotencyKey { get; }
}

public sealed record WorkflowAgentRunInput
{
    [JsonConstructor]
    public WorkflowAgentRunInput(Guid runId)
    {
        if (runId == Guid.Empty)
        {
            throw new ArgumentException("Workflow run id cannot be empty.", nameof(runId));
        }

        RunId = runId;
    }

    public Guid RunId { get; }
}

public sealed record WorkflowAgentExternalResponseInput
{
    [JsonConstructor]
    public WorkflowAgentExternalResponseInput(Guid externalRequestId, string responseJson)
    {
        if (externalRequestId == Guid.Empty)
        {
            throw new ArgumentException("Workflow external request id cannot be empty.", nameof(externalRequestId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(responseJson);
        ExternalRequestId = externalRequestId;
        ResponseJson = responseJson.Trim();
    }

    public Guid ExternalRequestId { get; }

    public string ResponseJson { get; }
}

public sealed record WorkflowAgentDefinitionDescriptor(
    Guid WorkflowId,
    Guid VersionId,
    string Name,
    string Description,
    WorkflowRuntimeBackendKind PreferredBackend,
    bool ValidationSucceeded,
    IReadOnlyList<string> ValidationIssues);

public sealed record WorkflowAgentDefinitionListResult(
    IReadOnlyList<WorkflowAgentDefinitionDescriptor> Definitions)
{
    public int Count => Definitions.Count;
}

public sealed record WorkflowAgentRunDescriptor(
    Guid RunId,
    Guid WorkflowId,
    Guid VersionId,
    WorkflowRunState State,
    WorkflowRuntimeBackendKind Backend,
    string Summary,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? TerminalAtUtc);

public sealed record WorkflowAgentStartResult(
    WorkflowAgentRunDescriptor Run,
    WorkflowAgentDefinitionSelectionMode SelectionMode,
    WorkflowRuntimeBackendKind ResolvedBackend,
    WorkflowLaunchIdempotencyDisposition IdempotencyDisposition,
    string Message);

public enum WorkflowAgentRunLookupOutcome
{
    Found,
    NotFound
}

public sealed record WorkflowAgentRunStatusResult(
    WorkflowAgentRunLookupOutcome Outcome,
    WorkflowAgentRunDescriptor? Run,
    string Message)
{
    public bool Found => Outcome == WorkflowAgentRunLookupOutcome.Found;
}

public sealed record WorkflowAgentCancellationResult(
    WorkflowRunCancellationOutcome Outcome,
    bool Succeeded,
    WorkflowAgentRunDescriptor? Run,
    string Message);

public sealed record WorkflowAgentExternalResponseResult(
    WorkflowExternalResponseOutcome Outcome,
    bool Succeeded,
    WorkflowAgentRunDescriptor? Run,
    Guid? ExternalRequestId,
    DateTimeOffset? RespondedAtUtc,
    string Message);

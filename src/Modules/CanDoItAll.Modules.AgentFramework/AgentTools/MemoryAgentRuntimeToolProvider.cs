using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class MemoryAgentRuntimeToolProvider(
    IMemoryOperationHandler operationHandler,
    TimeProvider timeProvider) : IAgentRuntimeToolProvider
{
    public const string ProviderKey = "memory.runtime-tools";

    private const int ProviderOrder = 925;

    private readonly IMemoryOperationHandler operationHandler = operationHandler;
    private readonly TimeProvider timeProvider = timeProvider;

    public int Order => ProviderOrder;

    public AgentRuntimeToolProviderDescriptor Descriptor { get; } = new(
        ProviderKey,
        "Memory runtime tools",
        "Provides generic Memory Protocol v1 tools backed by agent memory access settings.",
        ["agent-framework", "memory"],
        [
            AgentRuntimeToolProviderPurpose.InteractiveChat,
            AgentRuntimeToolProviderPurpose.GovernedProcessAutomation,
            AgentRuntimeToolProviderPurpose.AutoApprovedNonInteractive
        ]);

    public ValueTask<IReadOnlyList<AITool>> CreateToolsAsync(
        AgentRuntimeToolProviderContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var access = AgentMemoryAccessMetadata.Normalize(
            AgentMemoryAccessMetadata.Read(context.Agent.ConfigurationJson));
        if (!CanExposeTools(context.Agent, access))
        {
            return ValueTask.FromResult<IReadOnlyList<AITool>>([]);
        }

        var tools = new List<AITool>
        {
            AIFunctionFactory.Create(
                (MemoryContextQueryToolInput request, CancellationToken token = default) =>
                    QueryContextAsync(context, access, request, token),
                MemoryAgentRuntimeToolNames.ContextQuery,
                "Queries the agent's configured generic memory provider for contextual recall."),
            AIFunctionFactory.Create(
                (MemoryFeedbackSubmitToolInput request, CancellationToken token = default) =>
                    SubmitFeedbackAsync(context, access, request, token),
                MemoryAgentRuntimeToolNames.FeedbackSubmit,
                "Submits feedback for a delivered generic memory context pack."),
            AIFunctionFactory.Create(
                (MemoryOperationStatusToolInput request, CancellationToken token = default) =>
                    GetOperationStatusAsync(context, access, request, token),
                MemoryAgentRuntimeToolNames.OperationStatus,
                "Reads generic memory operation status."),
            AIFunctionFactory.Create(
                (MemoryOperationCancelToolInput request, CancellationToken token = default) =>
                    CancelOperationAsync(context, access, request, token),
                MemoryAgentRuntimeToolNames.OperationCancel,
                "Cancels a generic memory operation when the provider and operation policy allow it."),
            AIFunctionFactory.Create(
                (MemoryEventAcknowledgeToolInput request, CancellationToken token = default) =>
                    AcknowledgeEventAsync(context, access, request, token),
                MemoryAgentRuntimeToolNames.EventAcknowledge,
                "Acknowledges a provider-originated generic memory event.")
        };

        if (access.CanIngestSources)
        {
            tools.Insert(
                1,
                AIFunctionFactory.Create(
                    (MemoryIngestTextToolInput request, CancellationToken token = default) =>
                        IngestTextAsync(context, access, request, token),
                    MemoryAgentRuntimeToolNames.IngestText,
                    "Queues a manual text source snapshot for the agent's configured generic memory provider."));
        }

        return ValueTask.FromResult<IReadOnlyList<AITool>>(tools);
    }

    public IReadOnlyList<AgentRuntimeToolMetadata> GetToolMetadata(
        AgentRuntimeToolProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var access = AgentMemoryAccessMetadata.Normalize(
            AgentMemoryAccessMetadata.Read(context.Agent.ConfigurationJson));
        if (!CanExposeTools(context.Agent, access))
        {
            return [];
        }

        var metadata = new List<AgentRuntimeToolMetadata>
        {
            CreateMetadata(MemoryAgentRuntimeToolNames.ContextQuery, AgentRuntimeToolOperationKind.Read),
            CreateMetadata(MemoryAgentRuntimeToolNames.FeedbackSubmit, AgentRuntimeToolOperationKind.Mutation),
            CreateMetadata(MemoryAgentRuntimeToolNames.OperationStatus, AgentRuntimeToolOperationKind.Read),
            CreateMetadata(MemoryAgentRuntimeToolNames.OperationCancel, AgentRuntimeToolOperationKind.Mutation),
            CreateMetadata(MemoryAgentRuntimeToolNames.EventAcknowledge, AgentRuntimeToolOperationKind.Mutation)
        };

        if (access.CanIngestSources)
        {
            metadata.Insert(1, CreateMetadata(MemoryAgentRuntimeToolNames.IngestText, AgentRuntimeToolOperationKind.Mutation));
        }

        return metadata;
    }

    private async Task<MemoryContextQueryToolResult> QueryContextAsync(
        AgentRuntimeToolProviderContext context,
        AgentMemoryAccessSettings access,
        MemoryContextQueryToolInput request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return MemoryMafToolResultShaper.RejectedQuery(MemoryToolResultStatus.InvalidRequest, "Memory context query requires a non-empty query.");
        }

        var capability = request.AllowAsync
            ? MemoryCapabilityIds.ContextQueryAsync
            : MemoryCapabilityIds.ContextQuerySync;
        var policy = ResolvePolicy(context, access, capability, request.ProviderInstanceId, providerRequired: false);
        if (policy.Rejection is { } rejection)
        {
            return MemoryMafToolResultShaper.RejectedQuery(rejection.Status, rejection.Diagnostic);
        }

        var sourceSnapshotId = TryParseFirstSourceSnapshotId(request.SourceSnapshotIds);
        var query = new MemoryContextQueryRequest(
            request.Query.Trim(),
            [capability],
            sourceSnapshotId is null
                ? MemorySourceProvenance.None
                : new MemorySourceProvenance(sourceSnapshotId, SourceModule: null, SourceRecordIds: [], Citations: []));
        var handlerRequest = MemoryOperationRequestBuilder.Query(
            CreateCaller(context, MemoryAgentRuntimeToolNames.ContextQuery),
            policy.SelectionPolicy,
            query,
            CreateRetention());
        var result = await operationHandler.ExecuteQueryAsync(handlerRequest, cancellationToken);
        return MemoryMafToolResultShaper.ToQueryResult(result);
    }

    private async Task<MemoryIngestTextToolResult> IngestTextAsync(
        AgentRuntimeToolProviderContext context,
        AgentMemoryAccessSettings access,
        MemoryIngestTextToolInput request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!access.CanIngestSources)
        {
            return MemoryMafToolResultShaper.RejectedIngestion(MemoryToolResultStatus.ToolDisabled, "Memory source ingestion is not enabled for this agent.");
        }

        if (!IsSourceScopeAllowed(access, MemorySourceScope.Manual))
        {
            return MemoryMafToolResultShaper.RejectedIngestion(MemoryToolResultStatus.SourceScopeDenied, "Manual memory source ingestion is outside the agent's allowed source scopes.");
        }

        if (string.IsNullOrWhiteSpace(request.Title) ||
            string.IsNullOrWhiteSpace(request.ContentText))
        {
            return MemoryMafToolResultShaper.RejectedIngestion(MemoryToolResultStatus.InvalidRequest, "Manual memory ingestion requires a non-empty title and content text.");
        }

        var policy = ResolvePolicy(
            context,
            access,
            MemoryCapabilityIds.IngestionSnapshot,
            request.ProviderInstanceId,
            providerRequired: true);
        if (policy.Rejection is { } rejection)
        {
            return MemoryMafToolResultShaper.RejectedIngestion(rejection.Status, rejection.Diagnostic);
        }

        var providerInstanceId = policy.ProviderForPayload
            ?? throw new InvalidOperationException("Manual memory source ingestion requires a resolved provider instance id.");
        var requester = CreateRequester(context);
        var payload = ManualMemorySourcePayload.Text(
            request.Title.Trim(),
            request.ContentText.Trim(),
            string.IsNullOrWhiteSpace(request.SourceCategory) ? "agent-note" : request.SourceCategory.Trim(),
            request.Tags ?? []);
        var sourceRequest = new MemorySourceCaptureOperationRequest(
            providerInstanceId,
            payload.ToGatewayRequest(providerInstanceId, requester.RequesterId),
            "Manual text source captured for agent memory ingestion.");
        var handlerRequest = MemoryOperationRequestBuilder.SourceCapture(
            MemoryOperationCaller.ManualIngestion(MemoryAgentRuntimeToolNames.IngestText, requester),
            policy.SelectionPolicy,
            sourceRequest,
            CreateRetention());
        var result = await operationHandler.CaptureSourceForIngestionAsync(handlerRequest, cancellationToken);
        return MemoryMafToolResultShaper.ToIngestionResult(result);
    }

    private async Task<MemoryFeedbackSubmitToolResult> SubmitFeedbackAsync(
        AgentRuntimeToolProviderContext context,
        AgentMemoryAccessSettings access,
        MemoryFeedbackSubmitToolInput request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Guid.TryParse(request.ContextPackId, out var contextPackGuid) ||
            contextPackGuid == Guid.Empty)
        {
            return MemoryMafToolResultShaper.RejectedFeedback(MemoryToolResultStatus.InvalidRequest, "Memory feedback requires a valid context pack id.");
        }

        var policy = ResolvePolicy(context, access, MemoryCapabilityIds.FeedbackImmediate, request.ProviderInstanceId, providerRequired: false);
        if (policy.Rejection is { } rejection)
        {
            return MemoryMafToolResultShaper.RejectedFeedback(rejection.Status, rejection.Diagnostic);
        }

        var feedback = new MemoryFeedbackRequest(
            new MemoryContextPackId(contextPackGuid),
            request.Outcome,
            request.Comment?.Trim(),
            CreateEconomicImpact(request));
        var handlerRequest = MemoryOperationRequestBuilder.Feedback(
            CreateCaller(context, MemoryAgentRuntimeToolNames.FeedbackSubmit),
            policy.SelectionPolicy,
            feedback,
            CreateRetention());
        var result = await operationHandler.SubmitFeedbackAsync(handlerRequest, cancellationToken);
        return MemoryMafToolResultShaper.ToFeedbackResult(result);
    }

    private async Task<MemoryOperationStatusToolResult> GetOperationStatusAsync(
        AgentRuntimeToolProviderContext context,
        AgentMemoryAccessSettings access,
        MemoryOperationStatusToolInput request,
        CancellationToken cancellationToken)
    {
        if (request.OperationId == Guid.Empty)
        {
            return MemoryMafToolResultShaper.RejectedStatus(MemoryToolResultStatus.InvalidRequest, "Memory operation status requires a valid operation id.");
        }

        var policy = ResolvePolicy(context, access, MemoryCapabilityIds.OperationStatus, requestedProviderId: null, providerRequired: false);
        if (policy.Rejection is { } rejection)
        {
            return MemoryMafToolResultShaper.RejectedStatus(rejection.Status, rejection.Diagnostic);
        }

        var handlerRequest = MemoryOperationRequestBuilder.Status(
            CreateCaller(context, MemoryAgentRuntimeToolNames.OperationStatus),
            policy.SelectionPolicy,
            new MemoryOperationStatusRequest(new MemoryOperationId(request.OperationId)),
            CreateRetention());
        var result = await operationHandler.GetStatusAsync(handlerRequest, cancellationToken);
        return MemoryMafToolResultShaper.ToStatusResult(result);
    }

    private async Task<MemoryOperationCancelToolResult> CancelOperationAsync(
        AgentRuntimeToolProviderContext context,
        AgentMemoryAccessSettings access,
        MemoryOperationCancelToolInput request,
        CancellationToken cancellationToken)
    {
        if (request.OperationId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.Reason))
        {
            return MemoryMafToolResultShaper.RejectedCancel(MemoryToolResultStatus.InvalidRequest, "Memory operation cancellation requires a valid operation id and reason.");
        }

        var policy = ResolvePolicy(context, access, MemoryCapabilityIds.OperationStatus, requestedProviderId: null, providerRequired: false);
        if (policy.Rejection is { } rejection)
        {
            return MemoryMafToolResultShaper.RejectedCancel(rejection.Status, rejection.Diagnostic);
        }

        var handlerRequest = MemoryOperationRequestBuilder.Cancellation(
            CreateCaller(context, MemoryAgentRuntimeToolNames.OperationCancel),
            policy.SelectionPolicy,
            new MemoryOperationCancellationRequest(new MemoryOperationId(request.OperationId), request.Reason.Trim()),
            CreateRetention());
        var result = await operationHandler.CancelAsync(handlerRequest, cancellationToken);
        return MemoryMafToolResultShaper.ToCancelResult(result);
    }

    private async Task<MemoryEventAcknowledgeToolResult> AcknowledgeEventAsync(
        AgentRuntimeToolProviderContext context,
        AgentMemoryAccessSettings access,
        MemoryEventAcknowledgeToolInput request,
        CancellationToken cancellationToken)
    {
        if (request.EventId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.Reason))
        {
            return MemoryMafToolResultShaper.RejectedEventAcknowledge(MemoryToolResultStatus.InvalidRequest, "Memory event acknowledgement requires a valid event id and reason.");
        }

        var policy = ResolvePolicy(context, access, MemoryCapabilityIds.EventsProviderPush, request.ProviderInstanceId, providerRequired: false);
        if (policy.Rejection is { } rejection)
        {
            return MemoryMafToolResultShaper.RejectedEventAcknowledge(rejection.Status, rejection.Diagnostic);
        }

        var handlerRequest = MemoryOperationRequestBuilder.EventAcknowledge(
            CreateCaller(context, MemoryAgentRuntimeToolNames.EventAcknowledge),
            policy.SelectionPolicy,
            new MemoryEventAcknowledgeRequest(
                new MemoryProviderEventId(request.EventId),
                request.Accepted,
                request.Reason.Trim()),
            CreateRetention());
        var result = await operationHandler.AcknowledgeEventAsync(handlerRequest, cancellationToken);
        return MemoryMafToolResultShaper.ToEventAcknowledgeResult(result);
    }

    private MemoryMafProviderPolicyResolution ResolvePolicy(
        AgentRuntimeToolProviderContext context,
        AgentMemoryAccessSettings access,
        MemoryCapabilityId requiredCapability,
        string? requestedProviderId,
        bool providerRequired)
    {
        var matchedAssignmentProvider = ResolveAssignmentProvider(context, access);
        var assignments = access.ProviderAssignments
            .Select(MemoryMafProviderPolicyResolver.ToProviderAssignment)
            .ToArray();
        return MemoryMafProviderPolicyResolver.Resolve(new MemoryMafProviderPolicyRequest(
            requiredCapability,
            requestedProviderId,
            access.PreferredProviderInstanceId,
            access.DefaultProviderInstanceId,
            access.AllowedProviderInstanceIds,
            access.AllowedCapabilityIds,
            access.DeniedCapabilityIds,
            assignments,
            matchedAssignmentProvider,
            providerRequired,
            "the agent's allowed capability policy",
            "the agent's allowed provider policy",
            "This memory tool requires an explicit, assigned, preferred, or default provider instance id."));
    }

    private static bool CanExposeTools(
        AgentDefinition agent,
        AgentMemoryAccessSettings access)
    {
        return access.CanUseMemoryTools && agent.Permissions.CanUseTools;
    }

    private static bool IsSourceScopeAllowed(
        AgentMemoryAccessSettings access,
        MemorySourceScope sourceScope)
    {
        return access.AllowedSourceScopes.Count == 0 ||
               access.AllowedSourceScopes.Contains(sourceScope);
    }

    private static MemoryProviderInstanceId? ResolveAssignmentProvider(
        AgentRuntimeToolProviderContext context,
        AgentMemoryAccessSettings access)
    {
        foreach (var scope in AssignmentResolutionOrder)
        {
            var key = ResolveAssignmentKey(context, scope);
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var assignment = access.ProviderAssignments.FirstOrDefault(item =>
                item.Scope == scope &&
                string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
            var providerId = MemoryMafProviderPolicyResolver.NormalizeProviderId(assignment?.ProviderInstanceId);
            if (providerId is not null)
            {
                return providerId;
            }
        }

        return null;
    }

    private static string? ResolveAssignmentKey(
        AgentRuntimeToolProviderContext context,
        MemoryProviderAssignmentScope scope)
    {
        return scope switch
        {
            MemoryProviderAssignmentScope.Agent => context.Agent.Id.ToString("D"),
            MemoryProviderAssignmentScope.AgentRole => context.Agent.Workload.ToString(),
            MemoryProviderAssignmentScope.Workflow => TryGetTag(context, MemoryAgentRuntimeToolTags.WorkflowId),
            MemoryProviderAssignmentScope.WorkflowNode => TryGetTag(context, MemoryAgentRuntimeToolTags.WorkflowNodeId),
            MemoryProviderAssignmentScope.Process => TryGetTag(context, MemoryAgentRuntimeToolTags.ProcessId),
            _ => null
        };
    }

    private static string? TryGetTag(
        AgentRuntimeToolProviderContext context,
        string key)
    {
        return context.Tags.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
    }

    private static MemorySourceSnapshotId? TryParseFirstSourceSnapshotId(IReadOnlyList<string>? sourceSnapshotIds)
    {
        var sourceSnapshotId = sourceSnapshotIds?
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        return string.IsNullOrWhiteSpace(sourceSnapshotId)
            ? null
            : MemorySourceSnapshotId.Parse(sourceSnapshotId.Trim());
    }

    private static MemoryEconomicImpact? CreateEconomicImpact(MemoryFeedbackSubmitToolInput request)
    {
        if (!request.Amount.HasValue)
        {
            return null;
        }

        var currency = string.IsNullOrWhiteSpace(request.Currency)
            ? "USD"
            : request.Currency.Trim().ToUpperInvariant();
        return new MemoryEconomicImpact(currency, request.Amount.Value);
    }

    private MemoryLedgerRetentionPolicy CreateRetention()
    {
        return MemoryMafRetentionPolicyFactory.Create(timeProvider);
    }

    private static MemoryOperationCaller CreateCaller(
        AgentRuntimeToolProviderContext context,
        string route)
    {
        return MemoryOperationCaller.Tool(route, CreateRequester(context));
    }

    private static MemoryLedgerRequester CreateRequester(AgentRuntimeToolProviderContext context)
    {
        return new MemoryLedgerRequester(
            context.Agent.Id.ToString("D"),
            context.Agent.Id.ToString("D"),
            context.Agent.Workload.ToString(),
            context.RuntimeSessionKey,
            TryGetTag(context, MemoryAgentRuntimeToolTags.WorkflowId),
            TryGetTag(context, MemoryAgentRuntimeToolTags.WorkflowNodeId),
            TryGetTag(context, MemoryAgentRuntimeToolTags.ProcessId),
            TryGetTag(context, MemoryAgentRuntimeToolTags.ProcessStepId));
    }

    private static AgentRuntimeToolMetadata CreateMetadata(
        string toolName,
        AgentRuntimeToolOperationKind operationKind)
    {
        return new AgentRuntimeToolMetadata(
            ProviderKey,
            toolName,
            operationKind,
            requiresApprovalByDefault: false,
            ["memory", "generic-memory"]);
    }

    private static readonly MemoryProviderAssignmentScope[] AssignmentResolutionOrder =
    [
        MemoryProviderAssignmentScope.WorkflowNode,
        MemoryProviderAssignmentScope.Workflow,
        MemoryProviderAssignmentScope.Process,
        MemoryProviderAssignmentScope.Agent,
        MemoryProviderAssignmentScope.AgentRole
    ];
}

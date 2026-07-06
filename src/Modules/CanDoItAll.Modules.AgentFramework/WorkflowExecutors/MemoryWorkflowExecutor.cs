using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using MemorySourceProvenance = CanDoItAll.Memory.Abstractions.MemorySourceProvenance;
using MemorySourceSnapshotId = CanDoItAll.Memory.Abstractions.MemorySourceSnapshotId;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class MemoryWorkflowExecutor(
    IMemoryOperationHandler operationHandler,
    TimeProvider timeProvider) : IWorkflowExecutor
{
    private readonly IMemoryOperationHandler operationHandler = operationHandler;
    private readonly TimeProvider timeProvider = timeProvider;

    public WorkflowExecutorDescriptor Descriptor => MemoryWorkflowExecutorDescriptors.MemoryOperation;

    public async ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(input);

        var settings = WorkflowExecutorJson.Deserialize<MemoryWorkflowExecutorSettings>(context.SettingsJson);
        var result = settings.Operation switch
        {
            MemoryWorkflowOperation.ContextQuery => await QueryContextAsync(context, input, settings, cancellationToken).ConfigureAwait(false),
            MemoryWorkflowOperation.IngestText => await IngestTextAsync(context, settings, cancellationToken).ConfigureAwait(false),
            MemoryWorkflowOperation.FeedbackSubmit => await SubmitFeedbackAsync(context, settings, cancellationToken).ConfigureAwait(false),
            MemoryWorkflowOperation.OperationStatus => await GetOperationStatusAsync(context, settings, cancellationToken).ConfigureAwait(false),
            MemoryWorkflowOperation.OperationCancel => await CancelOperationAsync(context, settings, cancellationToken).ConfigureAwait(false),
            MemoryWorkflowOperation.EventAcknowledge => await AcknowledgeEventAsync(context, settings, cancellationToken).ConfigureAwait(false),
            _ => MemoryMafToolResultShaper.RejectedQuery(MemoryToolResultStatus.UnsupportedOperation, $"Unsupported memory workflow operation '{settings.Operation}'.")
        };

        return WorkflowExecutorJson.Result(context, result);
    }

    private async Task<object> QueryContextAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        MemoryWorkflowExecutorSettings settings,
        CancellationToken cancellationToken)
    {
        var queryText = ResolveQueryText(settings, input);
        if (string.IsNullOrWhiteSpace(queryText))
        {
            return MemoryMafToolResultShaper.RejectedQuery(MemoryToolResultStatus.InvalidRequest, "Memory workflow context query requires a non-empty query.");
        }

        var capability = settings.AllowAsync
            ? MemoryCapabilityIds.ContextQueryAsync
            : MemoryCapabilityIds.ContextQuerySync;
        var policy = ResolvePolicy(context, settings, capability, settings.ProviderInstanceId, providerRequired: false);
        if (policy.Rejection is { } rejection)
        {
            return MemoryMafToolResultShaper.RejectedQuery(rejection.Status, rejection.Diagnostic);
        }

        var sourceSnapshotId = TryParseFirstSourceSnapshotId(settings.SourceSnapshotIds);
        var query = new MemoryContextQueryRequest(
            queryText.Trim(),
            [capability],
            sourceSnapshotId is null
                ? MemorySourceProvenance.None
                : new MemorySourceProvenance(sourceSnapshotId, SourceModule: null, SourceRecordIds: [], Citations: []));
        var handlerRequest = MemoryOperationRequestBuilder.Query(
            CreateWorkflowCaller(context),
            policy.SelectionPolicy,
            query,
            CreateRetention());
        var result = await operationHandler.ExecuteQueryAsync(handlerRequest, cancellationToken).ConfigureAwait(false);
        return MemoryMafToolResultShaper.ToQueryResult(result);
    }

    private async Task<object> IngestTextAsync(
        WorkflowExecutorExecutionContext context,
        MemoryWorkflowExecutorSettings settings,
        CancellationToken cancellationToken)
    {
        if (!IsSourceScopeAllowed(settings, MemorySourceScope.Manual))
        {
            return MemoryMafToolResultShaper.RejectedIngestion(MemoryToolResultStatus.SourceScopeDenied, "Manual memory source ingestion is outside the workflow executor's allowed source scopes.");
        }

        if (string.IsNullOrWhiteSpace(settings.Title) ||
            string.IsNullOrWhiteSpace(settings.ContentText))
        {
            return MemoryMafToolResultShaper.RejectedIngestion(MemoryToolResultStatus.InvalidRequest, "Manual memory ingestion requires non-empty title and content text settings.");
        }

        var policy = ResolvePolicy(
            context,
            settings,
            MemoryCapabilityIds.IngestionSnapshot,
            settings.ProviderInstanceId,
            providerRequired: true);
        if (policy.Rejection is { } rejection)
        {
            return MemoryMafToolResultShaper.RejectedIngestion(rejection.Status, rejection.Diagnostic);
        }

        var providerInstanceId = policy.ProviderForPayload
            ?? throw new InvalidOperationException("Manual memory source ingestion requires a resolved provider instance id.");
        var requester = CreateRequester(context);
        var payload = ManualMemorySourcePayload.Text(
            settings.Title.Trim(),
            settings.ContentText.Trim(),
            string.IsNullOrWhiteSpace(settings.SourceCategory) ? "workflow-note" : settings.SourceCategory.Trim(),
            settings.Tags);
        var sourceRequest = new MemorySourceCaptureOperationRequest(
            providerInstanceId,
            payload.ToGatewayRequest(providerInstanceId, requester.RequesterId),
            "Manual text source captured for workflow memory ingestion.");
        var handlerRequest = MemoryOperationRequestBuilder.SourceCapture(
            MemoryOperationCaller.ManualIngestion(context.Node.Id.Value, requester),
            policy.SelectionPolicy,
            sourceRequest,
            CreateRetention());
        var result = await operationHandler.CaptureSourceForIngestionAsync(handlerRequest, cancellationToken).ConfigureAwait(false);
        return MemoryMafToolResultShaper.ToIngestionResult(result);
    }

    private async Task<object> SubmitFeedbackAsync(
        WorkflowExecutorExecutionContext context,
        MemoryWorkflowExecutorSettings settings,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(settings.ContextPackId, out var contextPackGuid) ||
            contextPackGuid == Guid.Empty)
        {
            return MemoryMafToolResultShaper.RejectedFeedback(MemoryToolResultStatus.InvalidRequest, "Memory workflow feedback requires a valid context pack id.");
        }

        var policy = ResolvePolicy(context, settings, MemoryCapabilityIds.FeedbackImmediate, settings.ProviderInstanceId, providerRequired: false);
        if (policy.Rejection is { } rejection)
        {
            return MemoryMafToolResultShaper.RejectedFeedback(rejection.Status, rejection.Diagnostic);
        }

        var feedback = new MemoryFeedbackRequest(
            new MemoryContextPackId(contextPackGuid),
            settings.Outcome,
            string.IsNullOrWhiteSpace(settings.Comment) ? null : settings.Comment.Trim(),
            CreateEconomicImpact(settings));
        var handlerRequest = MemoryOperationRequestBuilder.Feedback(
            CreateWorkflowCaller(context),
            policy.SelectionPolicy,
            feedback,
            CreateRetention());
        var result = await operationHandler.SubmitFeedbackAsync(handlerRequest, cancellationToken).ConfigureAwait(false);
        return MemoryMafToolResultShaper.ToFeedbackResult(result);
    }

    private async Task<object> GetOperationStatusAsync(
        WorkflowExecutorExecutionContext context,
        MemoryWorkflowExecutorSettings settings,
        CancellationToken cancellationToken)
    {
        if (settings.OperationId == Guid.Empty)
        {
            return MemoryMafToolResultShaper.RejectedStatus(MemoryToolResultStatus.InvalidRequest, "Memory workflow operation status requires a valid operation id.");
        }

        var policy = ResolvePolicy(context, settings, MemoryCapabilityIds.OperationStatus, requestedProviderId: null, providerRequired: false);
        if (policy.Rejection is { } rejection)
        {
            return MemoryMafToolResultShaper.RejectedStatus(rejection.Status, rejection.Diagnostic);
        }

        var handlerRequest = MemoryOperationRequestBuilder.Status(
            CreateWorkflowCaller(context),
            policy.SelectionPolicy,
            new MemoryOperationStatusRequest(new MemoryOperationId(settings.OperationId)),
            CreateRetention());
        var result = await operationHandler.GetStatusAsync(handlerRequest, cancellationToken).ConfigureAwait(false);
        return MemoryMafToolResultShaper.ToStatusResult(result);
    }

    private async Task<object> CancelOperationAsync(
        WorkflowExecutorExecutionContext context,
        MemoryWorkflowExecutorSettings settings,
        CancellationToken cancellationToken)
    {
        if (settings.OperationId == Guid.Empty ||
            string.IsNullOrWhiteSpace(settings.Reason))
        {
            return MemoryMafToolResultShaper.RejectedCancel(MemoryToolResultStatus.InvalidRequest, "Memory workflow operation cancellation requires a valid operation id and reason.");
        }

        var policy = ResolvePolicy(context, settings, MemoryCapabilityIds.OperationStatus, requestedProviderId: null, providerRequired: false);
        if (policy.Rejection is { } rejection)
        {
            return MemoryMafToolResultShaper.RejectedCancel(rejection.Status, rejection.Diagnostic);
        }

        var handlerRequest = MemoryOperationRequestBuilder.Cancellation(
            CreateWorkflowCaller(context),
            policy.SelectionPolicy,
            new MemoryOperationCancellationRequest(new MemoryOperationId(settings.OperationId), settings.Reason.Trim()),
            CreateRetention());
        var result = await operationHandler.CancelAsync(handlerRequest, cancellationToken).ConfigureAwait(false);
        return MemoryMafToolResultShaper.ToCancelResult(result);
    }

    private async Task<object> AcknowledgeEventAsync(
        WorkflowExecutorExecutionContext context,
        MemoryWorkflowExecutorSettings settings,
        CancellationToken cancellationToken)
    {
        if (settings.EventId == Guid.Empty ||
            string.IsNullOrWhiteSpace(settings.Reason))
        {
            return MemoryMafToolResultShaper.RejectedEventAcknowledge(MemoryToolResultStatus.InvalidRequest, "Memory workflow event acknowledgement requires a valid event id and reason.");
        }

        var policy = ResolvePolicy(context, settings, MemoryCapabilityIds.EventsProviderPush, settings.ProviderInstanceId, providerRequired: false);
        if (policy.Rejection is { } rejection)
        {
            return MemoryMafToolResultShaper.RejectedEventAcknowledge(rejection.Status, rejection.Diagnostic);
        }

        var handlerRequest = MemoryOperationRequestBuilder.EventAcknowledge(
            CreateWorkflowCaller(context),
            policy.SelectionPolicy,
            new MemoryEventAcknowledgeRequest(
                new MemoryProviderEventId(settings.EventId),
                settings.Accepted,
                settings.Reason.Trim()),
            CreateRetention());
        var result = await operationHandler.AcknowledgeEventAsync(handlerRequest, cancellationToken).ConfigureAwait(false);
        return MemoryMafToolResultShaper.ToEventAcknowledgeResult(result);
    }

    private MemoryMafProviderPolicyResolution ResolvePolicy(
        WorkflowExecutorExecutionContext context,
        MemoryWorkflowExecutorSettings settings,
        MemoryCapabilityId requiredCapability,
        string? requestedProviderId,
        bool providerRequired)
    {
        var matchedAssignmentProvider = ResolveAssignmentProvider(context, settings);
        var assignments = settings.ProviderAssignments
            .Select(MemoryMafProviderPolicyResolver.ToProviderAssignment)
            .ToArray();
        return MemoryMafProviderPolicyResolver.Resolve(new MemoryMafProviderPolicyRequest(
            requiredCapability,
            requestedProviderId,
            PreferredProviderInstanceId: null,
            settings.DefaultProviderInstanceId,
            settings.AllowedProviderInstanceIds,
            MemoryMafProviderPolicyResolver.ParseCapabilityIds(settings.AllowedCapabilityIds),
            MemoryMafProviderPolicyResolver.ParseCapabilityIds(settings.DeniedCapabilityIds),
            assignments,
            matchedAssignmentProvider,
            providerRequired,
            "the workflow executor's allowed capability policy",
            "the workflow executor's allowed provider policy",
            "This memory workflow operation requires an explicit, assigned, or default provider instance id."));
    }

    private static string ResolveQueryText(
        MemoryWorkflowExecutorSettings settings,
        WorkflowNodeInput input)
    {
        if (!string.IsNullOrWhiteSpace(settings.Query))
        {
            return settings.Query.Trim();
        }

        if (string.IsNullOrWhiteSpace(input.PayloadJson))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(input.PayloadJson);
            if (document.RootElement.ValueKind == JsonValueKind.String)
            {
                return document.RootElement.GetString()?.Trim() ?? string.Empty;
            }

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return string.Empty;
            }

            if (TryGetNonEmptyProperty(document.RootElement, "query", out var query) ||
                TryGetNonEmptyProperty(document.RootElement, "text", out query) ||
                TryGetNonEmptyProperty(document.RootElement, "prompt", out query))
            {
                return query;
            }
        }
        catch (JsonException)
        {
            return input.PayloadJson.Trim();
        }

        return string.Empty;
    }

    private static bool TryGetNonEmptyProperty(
        JsonElement element,
        string propertyName,
        out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString()?.Trim() ?? string.Empty;
        return value.Length > 0;
    }

    private static bool IsSourceScopeAllowed(
        MemoryWorkflowExecutorSettings settings,
        MemorySourceScope sourceScope)
    {
        var allowedScopes = ParseSourceScopes(settings.AllowedSourceScopes);
        return allowedScopes.Count == 0 ||
               allowedScopes.Contains(sourceScope);
    }

    private static MemoryProviderInstanceId? ResolveAssignmentProvider(
        WorkflowExecutorExecutionContext context,
        MemoryWorkflowExecutorSettings settings)
    {
        foreach (var scope in AssignmentResolutionOrder)
        {
            var key = ResolveAssignmentKey(context, scope);
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var assignment = settings.ProviderAssignments.FirstOrDefault(item =>
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
        WorkflowExecutorExecutionContext context,
        MemoryProviderAssignmentScope scope)
    {
        return scope switch
        {
            MemoryProviderAssignmentScope.Workflow => context.Definition.Id.Value.ToString("D"),
            MemoryProviderAssignmentScope.WorkflowNode => context.Node.Id.Value,
            _ => null
        };
    }

    private static IReadOnlyList<MemorySourceScope> ParseSourceScopes(IReadOnlyList<string> sourceScopes)
    {
        return sourceScopes
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(ParseSourceScope)
            .ToArray();
    }

    private static MemorySourceScope ParseSourceScope(string sourceScope)
    {
        return Enum.TryParse<MemorySourceScope>(sourceScope.Trim(), ignoreCase: true, out var parsed)
            ? parsed
            : throw new InvalidOperationException($"Unsupported memory source scope '{sourceScope}'.");
    }

    private static MemorySourceSnapshotId? TryParseFirstSourceSnapshotId(IReadOnlyList<string> sourceSnapshotIds)
    {
        var sourceSnapshotId = sourceSnapshotIds.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        return string.IsNullOrWhiteSpace(sourceSnapshotId)
            ? null
            : MemorySourceSnapshotId.Parse(sourceSnapshotId.Trim());
    }

    private static MemoryEconomicImpact? CreateEconomicImpact(MemoryWorkflowExecutorSettings settings)
    {
        if (!settings.Amount.HasValue)
        {
            return null;
        }

        var currency = string.IsNullOrWhiteSpace(settings.Currency)
            ? "USD"
            : settings.Currency.Trim().ToUpperInvariant();
        return new MemoryEconomicImpact(currency, settings.Amount.Value);
    }

    private MemoryLedgerRetentionPolicy CreateRetention()
    {
        return MemoryMafRetentionPolicyFactory.Create(timeProvider);
    }

    private static MemoryOperationCaller CreateWorkflowCaller(WorkflowExecutorExecutionContext context)
    {
        return MemoryOperationCaller.WorkflowExecutor(context.Node.Id.Value, CreateRequester(context));
    }

    private static MemoryLedgerRequester CreateRequester(WorkflowExecutorExecutionContext context)
    {
        return new MemoryLedgerRequester(
            context.Node.Id.Value,
            AgentId: null,
            AgentRole: "WorkflowExecutor",
            SessionId: context.RunId?.Value.ToString("D"),
            WorkflowId: context.Definition.Id.Value.ToString("D"),
            WorkflowNodeId: context.Node.Id.Value,
            ProcessId: null,
            ProcessStepId: null);
    }

    private static readonly MemoryProviderAssignmentScope[] AssignmentResolutionOrder =
    [
        MemoryProviderAssignmentScope.WorkflowNode,
        MemoryProviderAssignmentScope.Workflow
    ];
}

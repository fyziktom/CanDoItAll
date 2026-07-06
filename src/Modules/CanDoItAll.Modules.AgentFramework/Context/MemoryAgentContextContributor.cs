using System.Globalization;
using System.Text;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using MemorySourceProvenance = CanDoItAll.Memory.Abstractions.MemorySourceProvenance;

namespace CanDoItAll.Modules.AgentFramework;

public static class MemoryAgentContextContributionTraceKeys
{
    public const string Reason = "reason";
    public const string Status = "status";
    public const string RequiredCapability = "requiredCapability";
    public const string ProviderInstanceId = "providerInstanceId";
    public const string OperationId = "operationId";
    public const string StatusPath = "statusPath";
    public const string ContextPackId = "contextPackId";
    public const string SectionCount = "sectionCount";
    public const string WarningCount = "warningCount";
    public const string Confidence = "confidence";
    public const string FeedbackHandle = "feedbackHandle";
    public const string DispatchAttempted = "dispatchAttempted";
    public const string Diagnostic = "diagnostic";
}

public static class MemoryAgentContextContributionTraceReasons
{
    public const string Disabled = "memory-context-disabled";
    public const string EmptyQuery = "empty-query";
    public const string EmptyContextPack = "empty-context-pack";
    public const string AsyncAccepted = "async-operation-accepted";
    public const string NoProviderConfigured = "no-provider-configured";
    public const string NoEnabledProvider = "no-enabled-provider";
    public const string ProviderNotFound = "provider-not-found";
    public const string ProviderDisabled = "provider-disabled";
    public const string ProviderDenied = "provider-denied";
    public const string CapabilityDenied = "capability-denied";
    public const string CapabilityUnavailable = "capability-unavailable";
    public const string CapabilityMismatch = "capability-mismatch";
    public const string DriverUnavailable = "driver-unavailable";
    public const string TimedOut = "timed-out";
    public const string Failed = "memory-context-failed";
}

public sealed class MemoryAgentContextContributor(
    IMemoryOperationHandler operationHandler,
    TimeProvider timeProvider) : IAgentContextContributor
{
    public const string ContributorIdValue = "memory.context";

    private const int ContributorOrder = 50;

    private readonly IMemoryOperationHandler operationHandler = operationHandler;
    private readonly TimeProvider timeProvider = timeProvider;

    public AgentContextContributorDescriptor Descriptor { get; } = new(
        new AgentContextContributorId(ContributorIdValue),
        "Memory context",
        ContributorOrder);

    public async ValueTask<AgentContextContributionResult> ContributeAsync(
        AgentContextContributionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var access = AgentMemoryAccessMetadata.Normalize(
            AgentMemoryAccessMetadata.Read(request.Agent.ConfigurationJson));
        if (!access.CanUseContextContributions)
        {
            return AgentContextContributionResult.Skipped(CreateTrace(
                MemoryAgentContextContributionTraceReasons.Disabled,
                MemoryToolResultStatus.ToolDisabled,
                MemoryCapabilityIds.ContextQuerySync));
        }

        var query = BuildContextQuery(request);
        if (string.IsNullOrWhiteSpace(query))
        {
            return SkipOrFail(
                access,
                MemoryAgentContextContributionTraceReasons.EmptyQuery,
                "Memory context contribution requires a non-empty user query.",
                CreateTrace(
                    MemoryAgentContextContributionTraceReasons.EmptyQuery,
                    MemoryToolResultStatus.InvalidRequest,
                    MemoryCapabilityIds.ContextQuerySync));
        }

        var capability = access.AllowAsyncContextContributions
            ? MemoryCapabilityIds.ContextQueryAsync
            : MemoryCapabilityIds.ContextQuerySync;
        var policy = ResolvePolicy(request, access, capability);
        if (policy.Rejection is { } rejection)
        {
            var reason = ToReason(rejection.Status);
            return SkipOrFail(
                access,
                reason,
                rejection.Diagnostic,
                CreateTrace(reason, rejection.Status, capability, diagnostic: rejection.Diagnostic));
        }

        var queryRequest = new MemoryContextQueryRequest(
            query,
            [capability],
            MemorySourceProvenance.None);
        var handlerRequest = MemoryOperationRequestBuilder.Query(
            MemoryOperationCaller.ContextContributor(ContributorIdValue, CreateRequester(request)),
            policy.SelectionPolicy,
            queryRequest,
            CreateRetention());
        var result = await operationHandler.ExecuteQueryAsync(handlerRequest, cancellationToken).ConfigureAwait(false);
        return MapQueryResult(access, capability, result);
    }

    private AgentContextContributionResult MapQueryResult(
        AgentMemoryAccessSettings access,
        MemoryCapabilityId capability,
        MemoryOperationHandlerResult<MemoryContextPack> result)
    {
        var status = MemoryToolResultMapper.ToToolStatus(result.Status);
        var providerInstanceId = MemoryMafToolResultShaper.ResolveProviderInstanceId(result.Selection, result.OperationRecord);
        var trace = CreateTrace(
            ToReason(status),
            status,
            capability,
            providerInstanceId,
            result.OperationRecord?.OperationId.Value ?? result.AcceptedOperation?.OperationId.Value,
            result.Diagnostic,
            result.DriverDispatchAttempted);

        if (result.AcceptedOperation is { } accepted)
        {
            trace[MemoryAgentContextContributionTraceKeys.Reason] = MemoryAgentContextContributionTraceReasons.AsyncAccepted;
            trace[MemoryAgentContextContributionTraceKeys.OperationId] = accepted.OperationId.Value.ToString("D");
            trace[MemoryAgentContextContributionTraceKeys.StatusPath] = accepted.StatusPath;
            return SkipOrFail(
                access,
                MemoryAgentContextContributionTraceReasons.AsyncAccepted,
                "Memory context contribution was accepted asynchronously and is not available for this prompt.",
                trace);
        }

        if (status == MemoryToolResultStatus.Completed &&
            result.Output is { } output)
        {
            var contextText = RenderContextPack(output);
            trace[MemoryAgentContextContributionTraceKeys.ContextPackId] = output.ContextPackId.Value.ToString("D");
            trace[MemoryAgentContextContributionTraceKeys.SectionCount] = output.Sections.Count.ToString(CultureInfo.InvariantCulture);
            trace[MemoryAgentContextContributionTraceKeys.WarningCount] = output.Warnings.Count.ToString(CultureInfo.InvariantCulture);
            trace[MemoryAgentContextContributionTraceKeys.Confidence] = output.ProviderConfidence.ToString(CultureInfo.InvariantCulture);
            if (output.FeedbackHandle is { } feedbackHandle)
            {
                trace[MemoryAgentContextContributionTraceKeys.FeedbackHandle] = feedbackHandle.Value;
            }

            if (string.IsNullOrWhiteSpace(contextText))
            {
                trace[MemoryAgentContextContributionTraceKeys.Reason] = MemoryAgentContextContributionTraceReasons.EmptyContextPack;
                return SkipOrFail(
                    access,
                    MemoryAgentContextContributionTraceReasons.EmptyContextPack,
                    "Memory context provider returned an empty context pack.",
                    trace);
            }

            return AgentContextContributionResult.Provided(
                [new AgentContextMessage(AgentContextMessageRole.System, contextText)],
                trace);
        }

        return SkipOrFail(
            access,
            ToReason(status),
            string.IsNullOrWhiteSpace(result.Diagnostic)
                ? "Memory context provider did not return context."
                : result.Diagnostic,
            trace);
    }

    private MemoryMafProviderPolicyResolution ResolvePolicy(
        AgentContextContributionRequest request,
        AgentMemoryAccessSettings access,
        MemoryCapabilityId requiredCapability)
    {
        var matchedAssignmentProvider = ResolveAssignmentProvider(request, access);
        var assignments = access.ProviderAssignments
            .Select(MemoryMafProviderPolicyResolver.ToProviderAssignment)
            .ToArray();
        return MemoryMafProviderPolicyResolver.Resolve(new MemoryMafProviderPolicyRequest(
            requiredCapability,
            RequestedProviderInstanceId: null,
            access.PreferredProviderInstanceId,
            access.DefaultProviderInstanceId,
            access.AllowedProviderInstanceIds,
            access.AllowedCapabilityIds,
            access.DeniedCapabilityIds,
            assignments,
            matchedAssignmentProvider,
            ProviderRequired: false,
            "the agent's allowed context contribution policy",
            "the agent's allowed context contribution provider policy",
            "Memory context contribution requires an explicit, assigned, preferred, or default provider instance id."));
    }

    private static AgentContextContributionResult SkipOrFail(
        AgentMemoryAccessSettings access,
        string reason,
        string failureMessage,
        IReadOnlyDictionary<string, string> traceMetadata)
    {
        var trace = new Dictionary<string, string>(traceMetadata, StringComparer.Ordinal)
        {
            [MemoryAgentContextContributionTraceKeys.Reason] = reason
        };

        return access.RequireContextContributions
            ? AgentContextContributionResult.Failed(failureMessage, trace)
            : AgentContextContributionResult.Skipped(trace);
    }

    private static string BuildContextQuery(AgentContextContributionRequest request)
    {
        var userMessages = request.RequestMessages
            .Where(message => message.Role == AgentContextMessageRole.User)
            .Select(message => NormalizeQueryText(message.Text))
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .ToArray();
        return string.Join(Environment.NewLine, userMessages).Trim();
    }

    private static string NormalizeQueryText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Where(line => !IsPromptControlLine(line))
            .ToArray();
        return string.Join(Environment.NewLine, lines).Trim();
    }

    private static bool IsPromptControlLine(string line)
    {
        return line.StartsWith("Answer using ", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("If no memory context ", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Return concise JSON", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Use only ", StringComparison.OrdinalIgnoreCase);
    }

    private static string RenderContextPack(MemoryContextPack contextPack)
    {
        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(contextPack.Summary))
        {
            builder.AppendLine("Memory context pack:");
            builder.AppendLine(contextPack.Summary.Trim());
        }

        foreach (var section in contextPack.Sections)
        {
            if (string.IsNullOrWhiteSpace(section.Text))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.AppendLine("## " + (string.IsNullOrWhiteSpace(section.Title) ? "Memory" : section.Title.Trim()));
            if (section.Citations.Count > 0)
            {
                builder.AppendLine("Sources: " + string.Join("; ", section.Citations.Select(RenderCitation)));
            }

            builder.AppendLine(section.Text.Trim());
        }

        if (contextPack.Warnings.Count > 0)
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.AppendLine("Warnings: " + string.Join("; ", contextPack.Warnings.Select(warning => warning.Message)));
        }

        return builder.ToString().Trim();
    }

    private static string RenderCitation(MemoryCitation citation)
    {
        if (string.IsNullOrWhiteSpace(citation.Label))
        {
            return citation.SourceRef;
        }

        return $"{citation.Label} ({citation.SourceRef})";
    }

    private static MemoryProviderInstanceId? ResolveAssignmentProvider(
        AgentContextContributionRequest request,
        AgentMemoryAccessSettings access)
    {
        foreach (var scope in AssignmentResolutionOrder)
        {
            var key = ResolveAssignmentKey(request, scope);
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
        AgentContextContributionRequest request,
        MemoryProviderAssignmentScope scope)
    {
        return scope switch
        {
            MemoryProviderAssignmentScope.Agent => request.Agent.Id.ToString("D"),
            MemoryProviderAssignmentScope.AgentRole => request.Agent.Workload.ToString(),
            _ => null
        };
    }

    private MemoryLedgerRetentionPolicy CreateRetention()
    {
        return MemoryMafRetentionPolicyFactory.Create(timeProvider);
    }

    private static MemoryLedgerRequester CreateRequester(AgentContextContributionRequest request)
    {
        return new MemoryLedgerRequester(
            request.Agent.Id.ToString("D"),
            request.Agent.Id.ToString("D"),
            request.Agent.Workload.ToString(),
            SessionId: null,
            WorkflowId: null,
            WorkflowNodeId: null,
            ProcessId: null,
            ProcessStepId: null);
    }

    private static Dictionary<string, string> CreateTrace(
        string reason,
        MemoryToolResultStatus status,
        MemoryCapabilityId requiredCapability,
        string? providerInstanceId = null,
        Guid? operationId = null,
        string? diagnostic = null,
        bool dispatchAttempted = false)
    {
        var trace = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MemoryAgentContextContributionTraceKeys.Reason] = reason,
            [MemoryAgentContextContributionTraceKeys.Status] = status.ToString(),
            [MemoryAgentContextContributionTraceKeys.RequiredCapability] = requiredCapability.Value,
            [MemoryAgentContextContributionTraceKeys.DispatchAttempted] = dispatchAttempted.ToString()
        };
        if (!string.IsNullOrWhiteSpace(providerInstanceId))
        {
            trace[MemoryAgentContextContributionTraceKeys.ProviderInstanceId] = providerInstanceId;
        }

        if (operationId.HasValue)
        {
            trace[MemoryAgentContextContributionTraceKeys.OperationId] = operationId.Value.ToString("D");
        }

        if (!string.IsNullOrWhiteSpace(diagnostic))
        {
            trace[MemoryAgentContextContributionTraceKeys.Diagnostic] = diagnostic.Trim();
        }

        return trace;
    }

    private static string ToReason(MemoryToolResultStatus status)
    {
        return status switch
        {
            MemoryToolResultStatus.NoProviderConfigured => MemoryAgentContextContributionTraceReasons.NoProviderConfigured,
            MemoryToolResultStatus.NoEnabledProvider => MemoryAgentContextContributionTraceReasons.NoEnabledProvider,
            MemoryToolResultStatus.ProviderNotFound => MemoryAgentContextContributionTraceReasons.ProviderNotFound,
            MemoryToolResultStatus.ProviderDisabled => MemoryAgentContextContributionTraceReasons.ProviderDisabled,
            MemoryToolResultStatus.ProviderDenied => MemoryAgentContextContributionTraceReasons.ProviderDenied,
            MemoryToolResultStatus.CapabilityDenied => MemoryAgentContextContributionTraceReasons.CapabilityDenied,
            MemoryToolResultStatus.CapabilityUnavailable => MemoryAgentContextContributionTraceReasons.CapabilityUnavailable,
            MemoryToolResultStatus.CapabilityMismatch => MemoryAgentContextContributionTraceReasons.CapabilityMismatch,
            MemoryToolResultStatus.DriverUnavailable => MemoryAgentContextContributionTraceReasons.DriverUnavailable,
            MemoryToolResultStatus.TimedOut => MemoryAgentContextContributionTraceReasons.TimedOut,
            _ => MemoryAgentContextContributionTraceReasons.Failed
        };
    }

    private static readonly MemoryProviderAssignmentScope[] AssignmentResolutionOrder =
    [
        MemoryProviderAssignmentScope.Agent,
        MemoryProviderAssignmentScope.AgentRole
    ];

}

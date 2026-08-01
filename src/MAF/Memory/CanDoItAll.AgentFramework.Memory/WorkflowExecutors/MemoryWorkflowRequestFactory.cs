using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.AgentFramework.Memory;

internal sealed class MemoryWorkflowRequestFactory(TimeProvider timeProvider)
{
    private static readonly MemoryProviderAssignmentScope[] AssignmentResolutionOrder =
    [
        MemoryProviderAssignmentScope.WorkflowNode,
        MemoryProviderAssignmentScope.Workflow
    ];

    public MemoryMafProviderPolicyResolution ResolvePolicy(
        WorkflowExecutorExecutionContext context,
        MemoryWorkflowExecutorSettings settings,
        MemoryCapabilityId requiredCapability,
        string? requestedProviderId,
        bool providerRequired)
    {
        var assignmentRead = MemoryWorkflowAssignmentParser.Parse(settings.ProviderAssignments);
        if (assignmentRead.Diagnostic is not null)
        {
            return MemoryMafProviderPolicyResolution.Rejected(
                MemoryToolResultStatus.InvalidRequest,
                assignmentRead.Diagnostic);
        }

        var assignments = assignmentRead.Assignments;
        return MemoryMafProviderPolicyResolver.Resolve(new MemoryMafProviderPolicyRequest(
            requiredCapability,
            MemoryMafProviderPolicyResolver.ParseOptionalProviderId(requestedProviderId),
            PreferredProviderInstanceId: null,
            MemoryMafProviderPolicyResolver.ParseOptionalProviderId(settings.DefaultProviderInstanceId),
            ParseProviderIds(settings.AllowedProviderInstanceIds),
            MemoryMafProviderPolicyResolver.ParseCapabilityIds(settings.AllowedCapabilityIds),
            MemoryMafProviderPolicyResolver.ParseCapabilityIds(settings.DeniedCapabilityIds),
            assignments,
            ResolveAssignmentProvider(context, settings),
            providerRequired,
            "the workflow executor's allowed capability policy",
            "the workflow executor's allowed provider policy",
            "This memory workflow operation requires an explicit, assigned, or default provider instance id."));
    }

    public MemoryLedgerRetentionPolicy CreateRetention() =>
        MemoryMafRetentionPolicyFactory.Create(timeProvider);

    public static MemoryOperationCaller CreateWorkflowCaller(WorkflowExecutorExecutionContext context) =>
        MemoryOperationCaller.WorkflowExecutor(context.Node.Id.Value, CreateRequester(context));

    public static MemoryLedgerRequester CreateRequester(WorkflowExecutorExecutionContext context) =>
        new(
            context.Node.Id.Value,
            AgentId: null,
            AgentRole: "WorkflowExecutor",
            SessionId: context.RunId?.Value.ToString("D"),
            WorkflowId: context.Definition.Id.Value.ToString("D"),
            WorkflowNodeId: context.Node.Id.Value,
            ProcessId: null,
            ProcessStepId: null);

    private static MemoryProviderInstanceId? ResolveAssignmentProvider(
        WorkflowExecutorExecutionContext context,
        MemoryWorkflowExecutorSettings settings)
    {
        foreach (var scope in AssignmentResolutionOrder)
        {
            var key = scope switch
            {
                MemoryProviderAssignmentScope.Workflow => context.Definition.Id.Value.ToString("D"),
                MemoryProviderAssignmentScope.WorkflowNode => context.Node.Id.Value,
                _ => null
            };
            var assignment = settings.ProviderAssignments.FirstOrDefault(item =>
                item.Scope == scope &&
                string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
            var providerId = MemoryMafProviderPolicyResolver.ParseOptionalProviderId(assignment?.ProviderInstanceId);
            if (providerId is not null)
            {
                return providerId;
            }
        }

        return null;
    }

    private static IReadOnlyList<MemoryProviderInstanceId> ParseProviderIds(
        IReadOnlyList<string> providerIds) =>
        providerIds
            .Select(providerId => MemoryProviderInstanceId.Parse(RequireText(providerId, "allowlisted provider id")))
            .ToArray();

    private static string RequireText(string? value, string description)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Memory workflow {description} cannot be empty.")
            : value.Trim();
    }
}
